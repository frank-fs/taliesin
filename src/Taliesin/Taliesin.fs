namespace Taliesin

open System
open System.Collections.Generic
open System.IO
open System.Threading.Tasks
open Dyfrig

[<AutoOpen>]
module Extensions =
    type Microsoft.FSharp.Control.Async with
        static member AwaitTask(task: Task) =
            Async.AwaitTask(task.ContinueWith(Func<_,_>(fun _ -> ())))

module Dyfrig =
    
    // TODO: Update Dyfrig dependency to get this directly from env.GetRequestUri()
    type Environment with
        member env.GetBaseUri() =
            env.RequestScheme + "://" +
            (env.RequestHeaders.["Host"].[0]) +
            if String.IsNullOrEmpty env.RequestPathBase then "/" else env.RequestPathBase

        member env.GetRequestUri() =
            env.RequestScheme + "://" +
            (env.RequestHeaders.["Host"].[0]) +
            env.RequestPathBase +
            env.RequestPath +
            if String.IsNullOrEmpty env.RequestQueryString then "" else "?" + env.RequestQueryString


type OwinEnv = IDictionary<string, obj>
type OwinAppFunc = Func<OwinEnv, Task>

type HttpMethod =
    | GET
    | HEAD
    | POST
    | PUT
    | PATCH
    | DELETE
    | TRACE
    | OPTIONS
    | Custom of string
    with
    override x.ToString() =
        match x with
        | GET     _ -> "GET"
        | HEAD    _ -> "HEAD"
        | POST    _ -> "POST"
        | PUT     _ -> "PUT"
        | PATCH   _ -> "PATCH"
        | DELETE  _ -> "DELETE"
        | TRACE   _ -> "TRACE"
        | OPTIONS _ -> "OPTIONS"
        | Custom(m) -> m

/// Message type that associates an `HttpApplication with an HTTP method.
type internal HttpMethodHandler = HttpMethodHandler of HttpMethod * OwinAppFunc

/// Alias `MailboxProcessor<'T>` as `Agent<'T>`.
type Agent<'T> = MailboxProcessor<'T>

/// Messages used by the HTTP resource agent.
type internal ResourceMessage =
    | Request of OwinEnv
    | SetHandler of HttpMethod * OwinAppFunc
    | Error of exn
    | Shutdown

/// An HTTP resource agent.
type Resource(uriTemplate, allowedMethods: HttpMethod list, methodNotAllowedHandler) =
    let onError = new Event<exn>()
    let onSending = new Event<OwinEnv>()
    let onSent = new Event<OwinEnv>()
    let agent = Agent<ResourceMessage>.Start(fun inbox ->
        let rec loop allowedMethods (handlers: HttpMethodHandler list) = async {
            let! msg = inbox.Receive()
            match msg with
            | Request(env) ->
                let env = Environment.toEnvironment env
                let owinEnv = env :> OwinEnv 
                let foundHandler =
                    handlers
                    |> List.tryFind (fun (HttpMethodHandler(m, _)) -> m.ToString() = env.RequestMethod)
                let selectedHandler =
                    match foundHandler with
                    | Some(HttpMethodHandler(_, h)) -> h
                    | None -> methodNotAllowedHandler allowedMethods
                onSending.Trigger(owinEnv)
                do! selectedHandler.Invoke owinEnv |> Async.AwaitTask
                onSent.Trigger(owinEnv)
                return! loop allowedMethods handlers
            | SetHandler(httpMethod, handler) ->
                let foundMethod = allowedMethods |> List.tryFind ((=) httpMethod)
                let handlers' =
                    match foundMethod with
                    | Some _ ->
                        let handler = HttpMethodHandler(httpMethod, handler)
                        let otherHandlers = 
                            handlers |> List.filter (fun (HttpMethodHandler(m,_)) -> m <> httpMethod)
                        handler :: otherHandlers
                    | None -> handlers
                return! loop allowedMethods handlers'
            | Error exn ->
                onError.Trigger(exn)
                return! loop allowedMethods handlers
            | Shutdown -> ()
        }
            
        loop allowedMethods []
    )

    /// Connect the resource to the request event stream.
    /// This method applies a default filter to subscribe only to events
    /// matching the `Resource`'s `uriTemplate`.
    // NOTE: This should be internal if used in a type provider.
    abstract Connect : IObservable<OwinEnv> * (string -> OwinEnv -> bool) -> IDisposable
    default x.Connect(observable, uriMatcher) =
        let uriMatcher = uriMatcher uriTemplate
        (Observable.filter uriMatcher observable).Subscribe(x)

    /// Sets the handler for the specified `HttpMethod`.
    /// Ideally, we would expose methods matching the allowed methods.
    member x.SetHandler(handler) =
        agent.Post <| SetHandler(handler)

    /// Stops the resource agent.
    member x.Shutdown() = agent.Post Shutdown

    /// Provide stream of `exn` for logging purposes.
    [<CLIEvent>]
    member x.Error = onError.Publish
    /// Provide stream of environments before handling the response.
    [<CLIEvent>]
    member x.Sending = onSending.Publish
    /// Provide stream of environments after handling the request.
    [<CLIEvent>]
    member x.Sent = onSent.Publish

    /// Implement `IObserver` to allow the `Resource` to subscribe to the request event stream.
    interface IObserver<OwinEnv> with
        member x.OnNext(value) = agent.Post <| Request value
        member x.OnError(exn) = agent.Post <| Error exn
        member x.OnCompleted() = agent.Post Shutdown


/// Type alias for URI templates
type UriRouteTemplate = string

/// Defines the route for a specific resource
type RouteDef<'TName> = 'TName * UriRouteTemplate * HttpMethod list

/// Defines the tree type for specifying resource routes
/// Example:
///     type Routes = Root | About | Customers | Customer
///     let spec =
///         RouteNode((Home, "", [GET]),
///         [
///             RouteLeaf(About, "about", [GET])
///             RouteNode((Customers, "customers", [GET; POST]),
///             [
///                 RouteLeaf(Customer, "{id}", [GET; PUT; DELETE])
///             ])
///         ])            
type RouteSpec<'TRoute> =
    | RouteLeaf of RouteDef<'TRoute>
    | RouteNode of RouteDef<'TRoute> * RouteSpec<'TRoute> list

/// Default implementations of the 405 handler and URI matcher
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal ResourceManager =
    open Dyfrig

    let notAllowed (allowedMethods: HttpMethod list) =
        Func<_,_>(fun env ->
            let env = Environment.toEnvironment env
            env.ResponseStatusCode <- 405
            let bytes =
                allowedMethods
                |> List.fold (fun a b -> a + " " + b.ToString()) ""
                |> sprintf "405 Method Not Allowed. Try one of %s"
                |> System.Text.Encoding.ASCII.GetBytes
            async {
                do! env.ResponseBody.AsyncWrite(bytes)
            } |> Async.StartAsTask :> Task)

    let uriMatcher uriTemplate env =
        let env = Environment.toEnvironment env
        // TODO: Do this with F# rather than System.ServiceModel. This could easily use a Regex pattern.
        let template = UriTemplate(uriTemplate)
        let baseUri = Uri(env.GetBaseUri())
        let requestUri = Uri(env.GetRequestUri())
        let result = template.Match(baseUri, requestUri)
        // TODO: Return the match result as well as true/false, as we can retrieve parameter values this way.
        if result = null then false else
        // TODO: Investigate ways to avoid mutation.
        env.Add("taliesin.UriTemplateMatch", result) |> ignore
        true

/// Manages traffic flow within the application to specific routes.
/// Connect resource handlers using:
///     let app = ResourceManager<HttpRequestMessage, HttpResponseMessage, Routes>(spec)
///     app.[Root].SetHandler(GET, (fun request -> async { return response }))
/// A type provider could make this much nicer, e.g.:
///     let app = ResourceManager<"path/to/spec/as/string">
///     app.Root.Get(fun request -> async { return response })
type ResourceManager<'TRoute when 'TRoute : equality>(?uriMatcher, ?methodNotAllowedHandler) =
    // Should this also be an Agent<'T>?
    inherit Dictionary<'TRoute, Resource>(HashIdentity.Structural)

    let uriMatcher = defaultArg uriMatcher ResourceManager.uriMatcher
    let methodNotAllowedHandler = defaultArg methodNotAllowedHandler ResourceManager.notAllowed

    let onRequest = new Event<OwinEnv>()
    let onError = new Event<exn>()

    let apply manager resources subscriptions name uriTemplate allowedMethods =
        let resource = new Resource(uriTemplate, allowedMethods, methodNotAllowedHandler)
        let resources' = (name, resource) :: resources
        let subscriptions' = resource.Connect(manager, uriMatcher) :: subscriptions
        resources', subscriptions'

    let concatUriTemplate baseTemplate template =
        if String.IsNullOrEmpty baseTemplate then template else baseTemplate + "/" + template

    let rec applyRouteSpec manager uriTemplate resources subscriptions = function
        | RouteNode((name, template, httpMethods), nestedRoutes) ->
            let uriTemplate' = concatUriTemplate uriTemplate template
            let resources', subscriptions' = apply manager resources subscriptions name uriTemplate' httpMethods
            applyNestedRoutes manager uriTemplate' resources' subscriptions' nestedRoutes
        | RouteLeaf(name, template, httpMethods) ->
            let uriTemplate' = concatUriTemplate uriTemplate template
            apply manager resources subscriptions name uriTemplate' httpMethods

    and applyNestedRoutes manager uriTemplate resources subscriptions routes =
        match routes with
        | [] -> resources, subscriptions
        | route::routes ->
            let resources', subscriptions' = applyRouteSpec manager uriTemplate resources subscriptions route
            match routes with
            | [] -> resources', subscriptions'
            | _ -> applyNestedRoutes manager uriTemplate resources' subscriptions' routes

    member x.Start(routeSpec: RouteSpec<_>) =
        // TODO: This should probably manage a supervising agent of its own.
        let resources, subscriptions = applyRouteSpec x "" [] [] routeSpec
        for name, resource in resources do x.Add(name, resource)
        { new IDisposable with
            member __.Dispose() =
                // Dispose all current event subscriptions.
                for (disposable: IDisposable) in subscriptions do disposable.Dispose()
                // Shutdown all resource agents.
                for resource in x.Values do resource.Shutdown()
        }

    [<CLIEvent>]
    member x.Error = onError.Publish

    interface IObservable<OwinEnv> with
        member x.Subscribe(observer) = onRequest.Publish.Subscribe(observer)

    interface IObserver<OwinEnv> with
        member x.OnNext(value) = onRequest.Trigger(value)
        member x.OnError(exn) = onError.Trigger(exn)
        member x.OnCompleted() = ()


module Owin =
    let router<'TRoute when 'TRoute : equality> routeSpec : OwinApp =
        let manager = ResourceManager<'TRoute>()
        let subscription = manager.Start routeSpec
        let client = manager :> IObserver<_>
        fun env -> async {
            // When and how to dispose the subscription?
            client.OnNext(env)
            // NOTE: This is not a valid OWIN application. The Async block will complete before the request is handled.
        }
