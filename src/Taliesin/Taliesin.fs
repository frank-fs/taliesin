namespace Taliesin

open System
open System.Collections.Generic
open System.IO

type HttpApplication<'TRequest, 'TResponse> = 'TRequest -> Async<'TResponse>

type HttpMethod =
    | Get
    | Head
    | Post
    | Put
    | Patch
    | Delete
    | Trace
    | Options
    | ExtensionHttpMethod of string

type HttpAction<'TRequest, 'TResponse> = HttpMethod * HttpApplication<'TRequest, 'TResponse>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module HttpAction =
    let get h : HttpAction<_,_> = Get, h
    let head h : HttpAction<_,_> = Head, h
    let put h : HttpAction<_,_> = Put, h
    let post h : HttpAction<_,_> = Post, h
    let delete h : HttpAction<_,_> = Delete, h
    let trace h : HttpAction<_,_> = Trace, h
    let options h : HttpAction<_,_> = Options, h

/// Alias `MailboxProcessor<'T>` as `Agent<'T>`.
type Agent<'T> = MailboxProcessor<'T>

/// Messages used by the HTTP resource agent.
type internal ResourceMessage<'TRequest, 'TResponse> =
    | Request of 'TRequest * Stream
    | SetHandler of HttpAction<'TRequest, 'TResponse>
    | Error of exn
    | Shutdown

/// An HTTP resource agent.
type Resource<'TRequest, 'TResponse> private (uriTemplate, allowedMethods, handlers, getRequestMethod, send, methodNotAllowedHandler) =
    let onError = new Event<exn>()
    let agent = Agent<ResourceMessage<'TRequest, 'TResponse>>.Start(fun inbox ->
        let rec loop handlers = async {
            let! msg = inbox.Receive()
            match msg with
            | Request(request, out) ->
                let! response =
                    match handlers |> List.tryFind (fun (m, _) -> m = getRequestMethod request) with
                    | Some (_, h) -> h request
                    | None -> methodNotAllowedHandler allowedMethods request
                do! send out response
                return! loop handlers
            | SetHandler(httpMethod, handler) ->
                let handlers' =
                    match allowedMethods |> List.tryFind (fun m -> m = httpMethod) with
                    | None -> handlers
                    | Some _ -> (httpMethod, handler)::(List.filter (fun (m,h) -> m <> httpMethod) handlers)
                return! loop handlers'
            | Error exn ->
                onError.Trigger(exn)
                return! loop handlers
            | Shutdown -> ()
        }
            
        loop handlers
    )

    new (uriTemplate, handlers, getRequestMethod, methodNotAllowedHandler) =
        let allowedMethods = handlers |> List.map fst
        Resource(uriTemplate, allowedMethods, handlers, getRequestMethod, methodNotAllowedHandler)

    new (uriTemplate, allowedMethods, getRequestMethod, methodNotAllowedHandler) =
        Resource(uriTemplate, allowedMethods, [], getRequestMethod, methodNotAllowedHandler)

    /// Connect the resource to the request event stream.
    /// This method applies a default filter to subscribe only to events
    /// matching the `Resource`'s `uriTemplate`.
    // NOTE: This should be internal if used in a type provider.
    abstract Connect : IObservable<'TRequest * Stream> * (string -> 'TRequest -> bool) -> IDisposable
    default x.Connect(observable, uriMatcher) =
        (observable
         |> Observable.filter (fun (request, _) -> uriMatcher uriTemplate request)
        ).Subscribe(x)

    /// Sets the handler for the specified `HttpMethod`.
    /// Ideally, we would expose methods matching the allowed methods.
    member x.SetHandler(httpMethod, handler) =
        agent.Post <| SetHandler(httpMethod, handler)

    /// Stops the resource agent.
    member x.Shutdown() = agent.Post Shutdown

    /// Provide stream of `exn` for logging purposes.
    [<CLIEvent>]
    member x.Error = onError.Publish

    /// Implement `IObserver` to allow the `Resource` to subscribe to the request event stream.
    interface IObserver<'TRequest * Stream> with
        member x.OnNext(value) = agent.Post <| Request value
        member x.OnError(exn) = agent.Post <| Error exn
        member x.OnCompleted() = agent.Post Shutdown


/// Type alias for URI templates
type UriRouteTemplate = string

/// Defines the route for a specific resource
type RouteDef<'T> = 'T * UriRouteTemplate * HttpMethod list

/// Defines the tree type for specifying resource routes
/// Example:
///     type Routes = Root | About | Customers | Customer
///     let spec =
///         RouteNode((Home, "", [HttpMethod.Get]),
///                   [ RouteLeaf((About, "about", [HttpMethod.Get]))
///                     RouteNode((Customers, "customers", [HttpMethod.Get; HttpMethod.Post]),
///                               [ RouteLeaf((Customer, "{id}", [HttpMethod.Get; HttpMethod.Put; HttpMethod.Delete]))
///                               ])
///                   ])            
type RouteSpec<'T> =
    | RouteLeaf of RouteDef<'T>
    | RouteNode of RouteDef<'T> * RouteSpec<'T> list

/// Manages traffic flow within the application to specific routes.
/// Connect resource handlers using:
///     let app = ResourceManager<Routes>(spec)
///     app.[Root].SetHandler(HttpMethod.Get, (fun request -> async { return response }))
/// A type provider could make this much nicer, e.g.:
///     let app = ResourceManager<"path/to/spec/as/string">
///     app.Root.Get(fun request -> async { return response })
type ResourceManager<'TRequest, 'TResponse, 'TRoute when 'TRoute : equality>(routeSpec: RouteSpec<'TRoute>, getRequestMethod, send, methodNotAllowedHandler, uriMatcher) as x =
    // Should this also be an Agent<'T>?
    inherit Dictionary<'TRoute, Resource<'TRequest, 'TResponse>>(HashIdentity.Structural)

    let onRequest = new Event<'TRequest * Stream>()
    let onError = new Event<exn>()

    let apply resources subscriptions name uriTemplate (allowedMethods: HttpMethod list) =
        let resource = new Resource<'TRequest, 'TResponse>(uriTemplate, allowedMethods, getRequestMethod, send, methodNotAllowedHandler)
        let resources' = (name, resource) :: resources
        let subscriptions' = resource.Connect(x, uriMatcher) :: subscriptions
        resources', subscriptions'

    let rec applyRouteSpec uriTemplate resources subscriptions = function
        | RouteNode((name, template, allowedMethods), nestedRoutes) ->
            let uriTemplate' = uriTemplate + "/" + template
            let resources', subscriptions' = apply resources subscriptions name uriTemplate' allowedMethods
            applyNestedRoutes uriTemplate' resources' subscriptions' nestedRoutes
        | RouteLeaf(name, template, allowedMethods) ->
            let uriTemplate' = uriTemplate + "/" + template
            apply resources subscriptions name uriTemplate' allowedMethods
    and applyNestedRoutes uriTemplate resources subscriptions routes =
        match routes with
        | [] -> resources, subscriptions
        | route::routes ->
            let resources', subscriptions' = applyRouteSpec uriTemplate resources subscriptions route
            match routes with
            | [] -> resources', subscriptions'
            | _ -> applyNestedRoutes uriTemplate resources' subscriptions' routes

    let resources, subscriptions = applyRouteSpec "" [] [] routeSpec
    do for name, resource in resources do x.Add(name, resource)

    member x.Dispose() =
        // Dispose all current event subscriptions.
        for disposable in subscriptions do disposable.Dispose()
        // Shutdown all resource agents.
        for resource in x.Values do resource.Shutdown()

    [<CLIEvent>]
    member x.Error = onError.Publish

    interface IObservable<'TRequest * Stream> with
        member x.Subscribe(observer) = onRequest.Publish.Subscribe(observer)

    interface IObserver<'TRequest * Stream> with
        member x.OnNext(value) = onRequest.Trigger(value)
        member x.OnError(exn) = onError.Trigger(exn)
        member x.OnCompleted() = () // dispose the resources

    interface IDisposable with
        member x.Dispose() = x.Dispose()
