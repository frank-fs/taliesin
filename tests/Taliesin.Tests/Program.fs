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

    let spec =
        RouteNode((Root, "", [GET(fun _ -> async.Return "Hello, root!"B)]),
            [
                RouteLeaf(About, "about", [GET(fun _ -> async.Return "Hello, about!"B)])
                RouteNode((Customers, "customers",
                            [
                                GET(fun _ -> async.Return "Hello, customers!"B)
                                POST(fun _ -> async.Return "Created customer!"B)
                            ]),
                    [
                        RouteLeaf(Customer, "{id}",
                            [
                                GET(fun _ -> async.Return "Hello, customer!"B)
                                PUT(fun _ -> async.Return "Updated customer!"B)
                            ])
                    ])
            ])

    let resourceManager = Dyfrig.DyfrigResourceManager()
    let subscription = resourceManager.Start(spec)
    let client = resourceManager :> IObserver<_>

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
            resourceManager.[Root].Sending |> Event.add (fun resp ->
                let result = Encoding.ASCII.GetString(resp)
                test <@ result = "Hello, root!" @>
            )
            client.OnNext(env, out :> Stream)

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
                            requestHeaders = (Dictionary<_,_>(StringComparer.Ordinal) :> IDictionary<_,_>),
                            responseBody = (out :> Stream))
            resourceManager.[About].Sending |> Event.add (fun resp ->
                let result = Encoding.ASCII.GetString(resp)
                test <@ result = "Hello, about!" @>
            )
            client.OnNext(env, out :> Stream)

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
            resourceManager.[About].Sending |> Event.add (fun resp ->
                let result = Encoding.ASCII.GetString(resp)
                test <@ result = "Hello, customers!" @>
            )
            client.OnNext(env, out :> Stream)

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
            resourceManager.[About].Sending |> Event.add (fun resp ->
                let result = Encoding.ASCII.GetString(resp)
                test <@ result = "Created customer!" @>
            )
            client.OnNext(env, out :> Stream)

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
            resourceManager.[About].Sending |> Event.add (fun resp ->
                let result = Encoding.ASCII.GetString(resp)
                test <@ result = "Hello, customer!" @>
            )
            client.OnNext(env, out :> Stream)

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
            resourceManager.[About].Sending |> Event.add (fun resp ->
                let result = Encoding.ASCII.GetString(resp)
                test <@ result = "Updated customer!" @>
            )
            client.OnNext(env, out :> Stream)
    ]

[<EntryPoint>]
let main args = defaultMainThisAssembly args
