open System
open System.Collections.Generic
open System.IO
open System.Net
open System.Net.Http
open System.Text
open System.Threading.Tasks
open Dyfrig
open Fuchu
open Swensen.Unquote
open Taliesin

type Resources = Root | About | Customers | Customer
    with
    interface IUriRouteTemplate with
        member x.UriTemplate =
            match x with
            | Root -> ""
            | About -> "about"
            | Customers -> "customers"
            | Customer -> "{id}"

[<Tests>]
let tests =
    let makeHandler statusCode (content: byte[]) = OwinAppFunc(fun env ->
        let env = Environment.toEnvironment env
        env.ResponseStatusCode <- statusCode
        env.ResponseHeaders.Add("Content-Length", [| string content.Length |])
        env.ResponseHeaders.Add("Content-Type", [| "text/plain" |])
        env.ResponseBody.Write(content, 0, content.Length)
        let tcs = TaskCompletionSource<unit>()
        tcs.SetResult()
        tcs.Task :> Task)
    
    let makeHttpHandler statusCode (data: byte[]) =
        let handler (request: HttpRequestMessage) =
            let content = new ByteArrayContent(data)
            content.Headers.ContentLength <- Nullable data.LongLength
            content.Headers.ContentType <- Headers.MediaTypeHeaderValue("text/plain")
            new HttpResponseMessage(statusCode, Content = content, RequestMessage = request)
            |> async.Return
        Dyfrig.SystemNetHttpAdapter.fromAsyncSystemNetHttp handler

    let customerSpec =
        RouteLeaf(Customer,
                  [
                      GET(makeHandler 200 "Hello, customer!"B)
                      PUT(makeHttpHandler HttpStatusCode.NoContent "Updated customer!"B)
                  ])

    let customersSpec =
        RouteNode((Customers,
                   [
                       GET(makeHttpHandler HttpStatusCode.OK "Hello, customers!"B)
                       POST(makeHandler 201 "Created customer!"B)
                   ]),
                   [ customerSpec ])

    let spec =
        RouteNode((Root,
                   [GET(makeHandler 200 "Hello, root!"B)]),
                   [
                       RouteLeaf(About, [GET(makeHandler 200 "Hello, about!"B)])
                       customersSpec
                   ])

    testList "dyfrig" [
        testCase "valid GET /" <| fun _ ->
            let resourceManager = ResourceManager<Resources>()
            let subscription = resourceManager.Start(spec)
            let out = new MemoryStream()
            let headers = Dictionary<_,_>(StringComparer.Ordinal) :> IDictionary<_,_>
            headers.Add("Host", [|"localhost"|])
            let env = new Environment(
                            requestMethod = "GET",
                            requestScheme = "http",
                            requestPathBase = "",
                            requestPath = "/",
                            requestQueryString = "",
                            requestProtocol = "HTTP/1.1",
                            requestHeaders = headers,
                            responseBody = (out :> Stream))
            async {
                do! resourceManager.Invoke env |> Async.AwaitTask
                let result = Encoding.ASCII.GetString(out.ToArray())
                test <@ result = "Hello, root!" @>
            } |> Async.RunSynchronously

            subscription.Dispose()

        testCase "valid GET /about" <| fun _ ->
            let resourceManager = ResourceManager<Resources>()
            let subscription = resourceManager.Start(spec)
            let out = new MemoryStream()
            let headers = Dictionary<_,_>(StringComparer.Ordinal) :> IDictionary<_,_>
            headers.Add("Host", [|"localhost"|])
            let env = new Environment(
                            requestMethod = "GET",
                            requestScheme = "http",
                            requestPathBase = "",
                            requestPath = "/about",
                            requestQueryString = "",
                            requestProtocol = "HTTP/1.1",
                            requestHeaders = headers,
                            responseBody = (out :> Stream))
            async {
                do! resourceManager.Invoke env |> Async.AwaitTask
                let result = Encoding.ASCII.GetString(out.ToArray())
                test <@ result = "Hello, about!" @>
            } |> Async.RunSynchronously

            subscription.Dispose()

        testCase "valid GET /customers" <| fun _ ->
            let resourceManager = ResourceManager<Resources>()
            let subscription = resourceManager.Start(spec)
            let out = new MemoryStream()
            let headers = Dictionary<_,_>(StringComparer.Ordinal) :> IDictionary<_,_>
            headers.Add("Host", [|"localhost"|])
            let env = new Environment(
                            requestMethod = "GET",
                            requestScheme = "http",
                            requestPathBase = "",
                            requestPath = "/customers",
                            requestQueryString = "",
                            requestProtocol = "HTTP/1.1",
                            requestHeaders = headers,
                            responseBody = (out :> Stream))
            async {
                do! resourceManager.Invoke env |> Async.AwaitTask
                let result = Encoding.ASCII.GetString(out.ToArray())
                test <@ result = "Hello, customers!" @>
            } |> Async.RunSynchronously

            subscription.Dispose()

        testCase "valid POST /customers" <| fun _ ->
            let resourceManager = ResourceManager<Resources>()
            let subscription = resourceManager.Start(spec)
            let out = new MemoryStream()
            let headers = Dictionary<_,_>(StringComparer.Ordinal) :> IDictionary<_,_>
            headers.Add("Host", [|"localhost"|])
            let env = new Environment(
                            requestMethod = "POST",
                            requestScheme = "http",
                            requestPathBase = "",
                            requestPath = "/customers",
                            requestQueryString = "",
                            requestProtocol = "HTTP/1.1",
                            requestHeaders = headers,
                            responseBody = (out :> Stream))
            async {
                do! resourceManager.Invoke env |> Async.AwaitTask
                let result = Encoding.ASCII.GetString(out.ToArray())
                test <@ result = "Created customer!" @>
            } |> Async.RunSynchronously

            subscription.Dispose()

        testCase "valid GET /customers/1" <| fun _ ->
            let resourceManager = ResourceManager<Resources>()
            let subscription = resourceManager.Start(spec)
            let out = new MemoryStream()
            let headers = Dictionary<_,_>(StringComparer.Ordinal) :> IDictionary<_,_>
            headers.Add("Host", [|"localhost"|])
            let env = new Environment(
                            requestMethod = "GET",
                            requestScheme = "http",
                            requestPathBase = "",
                            requestPath = "/customers/1",
                            requestQueryString = "",
                            requestProtocol = "HTTP/1.1",
                            requestHeaders = headers,
                            responseBody = (out :> Stream))
            async {
                do! resourceManager.Invoke env |> Async.AwaitTask
                let result = Encoding.ASCII.GetString(out.ToArray())
                test <@ result = "Hello, customer!" @>
            } |> Async.RunSynchronously

            subscription.Dispose()

        testCase "valid PUT /customers/1" <| fun _ ->
            let resourceManager = ResourceManager<Resources>()
            let subscription = resourceManager.Start(spec)
            let out = new MemoryStream()
            let headers = Dictionary<_,_>(StringComparer.Ordinal) :> IDictionary<_,_>
            headers.Add("Host", [|"localhost"|])
            let env = new Environment(
                            requestMethod = "PUT",
                            requestScheme = "http",
                            requestPathBase = "",
                            requestPath = "/customers/1",
                            requestQueryString = "",
                            requestProtocol = "HTTP/1.1",
                            requestHeaders = headers,
                            responseBody = (out :> Stream))
            async {
                do! resourceManager.Invoke env |> Async.AwaitTask
                let result = Encoding.ASCII.GetString(out.ToArray())
                test <@ result = "Updated customer!" @>
            } |> Async.RunSynchronously

            subscription.Dispose()

        testCase "invalid DELETE /customers/1" <| fun _ ->
            let resourceManager = ResourceManager<Resources>()
            let subscription = resourceManager.Start(spec)
            let out = new MemoryStream()
            let headers = Dictionary<_,_>(StringComparer.Ordinal) :> IDictionary<_,_>
            headers.Add("Host", [|"localhost"|])
            let env = new Environment(
                            requestMethod = "DELETE",
                            requestScheme = "http",
                            requestPathBase = "",
                            requestPath = "/customers/1",
                            requestQueryString = "",
                            requestProtocol = "HTTP/1.1",
                            requestHeaders = headers,
                            responseBody = (out :> Stream))
            async {
                do! resourceManager.Invoke env |> Async.AwaitTask
                let allowedMethods = env.ResponseHeaders.["Allow"]
                test <@ allowedMethods = [|"GET";"PUT"|] @>
                let result = Encoding.ASCII.GetString(out.ToArray())
                test <@ result = "" @>
            } |> Async.RunSynchronously

            subscription.Dispose()
    ]

[<EntryPoint>]
let main args = defaultMainThisAssembly args
