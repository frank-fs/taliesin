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

    let customersSpec =
        RouteNode((Customers, "customers", [GET; POST]),
            [ RouteLeaf(Customer, "{id}", [GET; PUT]) ])

    let spec =
        RouteNode((Root, "", [GET]),
            [
                RouteLeaf(About, "about", [GET])
                customersSpec
            ])

    let resourceManager = ResourceManager()
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
            resourceManager.[Root].Sent |> Event.add (fun env ->
                let result = Encoding.ASCII.GetString(out.ToArray())
                test <@ result = "Hello, root!" @>
            )
            client.OnNext(env)

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
            resourceManager.[About].Sent |> Event.add (fun env ->
                let result = Encoding.ASCII.GetString(out.ToArray())
                test <@ result = "Hello, about!" @>
            )
            client.OnNext(env)

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
            resourceManager.[About].Sent |> Event.add (fun env ->
                let result = Encoding.ASCII.GetString(out.ToArray())
                test <@ result = "Hello, customers!" @>
            )
            client.OnNext(env)

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
            resourceManager.[About].Sent |> Event.add (fun env ->
                let result = Encoding.ASCII.GetString(out.ToArray())
                test <@ result = "Created customer!" @>
            )
            client.OnNext(env)

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
            resourceManager.[About].Sent |> Event.add (fun env ->
                let result = Encoding.ASCII.GetString(out.ToArray())
                test <@ result = "Hello, customer!" @>
            )
            client.OnNext(env)

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
            resourceManager.[About].Sent |> Event.add (fun env ->
                let result = Encoding.ASCII.GetString(out.ToArray())
                test <@ result = "Updated customer!" @>
            )
            client.OnNext(env)
    ]

[<EntryPoint>]
let main args = defaultMainThisAssembly args
