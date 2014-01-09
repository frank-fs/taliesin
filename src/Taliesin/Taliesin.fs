namespace Taliesin

open System
open System.Collections.Generic
open System.IO

/// An `HttpApplication` accepts a request and asynchronously produces a response.
type HttpApplication<'TRequest, 'TResponse> = 'TRequest -> Async<'TResponse>

/// Message type that associates an `HttpApplication with an HTTP method.
type HttpMethodHandler<'TRequest, 'TResponse> =
    | GET     of HttpApplication<'TRequest, 'TResponse>
    | HEAD    of HttpApplication<'TRequest, 'TResponse>
    | POST    of HttpApplication<'TRequest, 'TResponse>
    | PUT     of HttpApplication<'TRequest, 'TResponse>
    | PATCH   of HttpApplication<'TRequest, 'TResponse>
    | DELETE  of HttpApplication<'TRequest, 'TResponse>
    | TRACE   of HttpApplication<'TRequest, 'TResponse>
    | OPTIONS of HttpApplication<'TRequest, 'TResponse>
    | Custom  of string * HttpApplication<'TRequest, 'TResponse>
    with
    /// Gets the method name for the current `HttpMethodHandler`.
    member x.Method =
        match x with
        | GET       _ -> "GET"
        | HEAD      _ -> "HEAD"
        | POST      _ -> "POST"
        | PUT       _ -> "PUT"
        | PATCH     _ -> "PATCH"
        | DELETE    _ -> "DELETE"
        | TRACE     _ -> "TRACE"
        | OPTIONS   _ -> "OPTIONS"
        | Custom(m,_) -> m
    member x.Handler =
        match x with
        | GET       h -> h
        | HEAD      h -> h
        | POST      h -> h
        | PUT       h -> h
        | PATCH     h -> h
        | DELETE    h -> h
        | TRACE     h -> h
        | OPTIONS   h -> h
        | Custom(_,h) -> h

/// Alias `MailboxProcessor<'T>` as `Agent<'T>`.
type Agent<'T> = MailboxProcessor<'T>

/// Messages used by the HTTP resource agent.
type internal ResourceMessage<'TRequest, 'TResponse> =
    | Request of 'TRequest * Stream
    | SetHandler of HttpMethodHandler<'TRequest, 'TResponse>
    | Error of exn
    | Shutdown

/// An HTTP resource agent.
type Resource<'TRequest, 'TResponse>(uriTemplate, handlers: HttpMethodHandler<_,_> list, getRequestMethod, send, methodNotAllowedHandler) =
    let allowedMethods = handlers |> List.map (fun m -> m.Method)
    let onError = new Event<exn>()
    let agent = Agent<ResourceMessage<'TRequest, 'TResponse>>.Start(fun inbox ->
        let rec loop allowedMethods (handlers: HttpMethodHandler<_,_> list) = async {
            let! msg = inbox.Receive()
            match msg with
            | Request(request, out) ->
                let! response =
                    match handlers |> List.tryFind (fun h -> h.Method = getRequestMethod request) with
                    | Some h -> h.Handler request
                    | None -> methodNotAllowedHandler allowedMethods request
                do! send out response
                return! loop allowedMethods handlers
            | SetHandler(handler) ->
                let handlers' = handler::(handlers |> List.filter (fun h -> h.Method <> handler.Method))
                return! loop allowedMethods handlers'
            | Error exn ->
                onError.Trigger(exn)
                return! loop allowedMethods handlers
            | Shutdown -> ()
        }
            
        loop allowedMethods handlers
    )

    /// Connect the resource to the request event stream.
    /// This method applies a default filter to subscribe only to events
    /// matching the `Resource`'s `uriTemplate`.
    // NOTE: This should be internal if used in a type provider.
    abstract Connect : IObservable<'TRequest * Stream> * (string -> 'TRequest -> bool) -> IDisposable
    default x.Connect(observable, uriMatcher) =
        let uriMatcher = uriMatcher uriTemplate
        (observable |> Observable.filter (fun (request, _) -> uriMatcher request)).Subscribe(x)

    /// Sets the handler for the specified `HttpMethod`.
    /// Ideally, we would expose methods matching the allowed methods.
    member x.SetHandler(handler) =
        agent.Post <| SetHandler(handler)

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
type RouteDef<'TName, 'TRequest, 'TResponse> = 'TName * UriRouteTemplate * HttpMethodHandler<'TRequest, 'TResponse> list

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
type RouteSpec<'TRoute, 'TRequest, 'TResponse> =
    | RouteLeaf of RouteDef<'TRoute, 'TRequest, 'TResponse>
    | RouteNode of RouteDef<'TRoute, 'TRequest, 'TResponse> * RouteSpec<'TRoute, 'TRequest, 'TResponse> list

/// Manages traffic flow within the application to specific routes.
/// Connect resource handlers using:
///     let app = ResourceManager<HttpRequestMessage, HttpResponseMessage, Routes>(spec)
///     app.[Root].SetHandler(GET, (fun request -> async { return response }))
/// A type provider could make this much nicer, e.g.:
///     let app = ResourceManager<"path/to/spec/as/string">
///     app.Root.Get(fun request -> async { return response })
type ResourceManager<'TRequest, 'TResponse, 'TRoute when 'TRoute : equality>(getRequestMethod, send, methodNotAllowedHandler, uriMatcher) =
    // Should this also be an Agent<'T>?
    inherit Dictionary<'TRoute, Resource<'TRequest, 'TResponse>>(HashIdentity.Structural)

    let onRequest = new Event<'TRequest * Stream>()
    let onError = new Event<exn>()

    let apply manager resources subscriptions name uriTemplate (handlers: HttpMethodHandler<_,_> list) =
        let resource = new Resource<'TRequest, 'TResponse>(uriTemplate, handlers, getRequestMethod, send, methodNotAllowedHandler)
        let resources' = (name, resource) :: resources
        let subscriptions' = resource.Connect(manager, uriMatcher) :: subscriptions
        resources', subscriptions'

    let rec applyRouteSpec manager uriTemplate resources subscriptions = function
        | RouteNode((name, template, handlers), nestedRoutes) ->
            let uriTemplate' = uriTemplate + "/" + template
            let resources', subscriptions' = apply manager resources subscriptions name uriTemplate' handlers
            applyNestedRoutes manager uriTemplate' resources' subscriptions' nestedRoutes
        | RouteLeaf(name, template, handlers) ->
            let uriTemplate' = uriTemplate + "/" + template
            apply manager resources subscriptions name uriTemplate' handlers

    and applyNestedRoutes manager uriTemplate resources subscriptions routes =
        match routes with
        | [] -> resources, subscriptions
        | route::routes ->
            let resources', subscriptions' = applyRouteSpec manager uriTemplate resources subscriptions route
            match routes with
            | [] -> resources', subscriptions'
            | _ -> applyNestedRoutes manager uriTemplate resources' subscriptions' routes

    member x.Start(routeSpec: RouteSpec<_,_,_>) =
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

    interface IObservable<'TRequest * Stream> with
        member x.Subscribe(observer) = onRequest.Publish.Subscribe(observer)

    interface IObserver<'TRequest * Stream> with
        member x.OnNext(value) = onRequest.Trigger(value)
        member x.OnError(exn) = onError.Trigger(exn)
        member x.OnCompleted() = ()
