open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Threading.Tasks
open Dyfrig
open Fuchu
open Swensen.Unquote
open Taliesin

type Resources = Root | About | Customers | Customer

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

    let customersSpec =
        RouteNode((Customers, "customers",
                    [
                        GET(makeHandler 200 "Hello, customers!"B)
                        POST(makeHandler 201 "Created customer!"B)
                    ]),
                    [ RouteLeaf(Customer, "{id}",
                                [
                                    GET(makeHandler 200 "Hello, customer!"B)
                                    PUT(makeHandler 204 "Updated customer!"B)
                                ])
                    ])

    let spec =
        RouteNode((Root, "", [GET(makeHandler 200 "Hello, root!"B)]),
            [
                RouteLeaf(About, "about", [GET(makeHandler 200 "Hello, about!"B)])
                customersSpec
            ])

    let resourceManager = ResourceManager<Resources>()
    let subscription = resourceManager.Start(spec)

    testList "dyfrig" [
        testCase "valid GET /" <| fun _ ->
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

        testCase "valid GET /about" <| fun _ ->
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

        testCase "valid GET /customers" <| fun _ ->
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

        testCase "valid POST /customers" <| fun _ ->
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

        testCase "valid GET /customers/1" <| fun _ ->
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

        testCase "valid PUT /customers/1" <| fun _ ->
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

        testCase "invalid DELETE /customers/1" <| fun _ ->
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
                let result = Encoding.ASCII.GetString(out.ToArray())
                test <@ result = "405 Method Not Allowed. Try one of GET PUT" @>
            } |> Async.RunSynchronously
    ]

[<EntryPoint>]
let main args = defaultMainThisAssembly args
