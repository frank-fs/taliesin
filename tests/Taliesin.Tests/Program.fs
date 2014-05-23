open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Threading.Tasks
open Dyfrig
open Fuchu
open Swensen.Unquote
open Taliesin

type Routes = Root | About | Customers | Customer

[<Tests>]
let tests =

    let spec =
        RouteNode((Root, "", [GET(fun _ -> async.Return "Hello, root!"B)]),
            [
                RouteLeaf(About, "about", [GET(fun _ -> async.Return "Hello, about!"B)])
                RouteNode((Customers, "customers", [GET(fun _ -> async.Return "Hello, customers!"B)]),
                    [
                        RouteLeaf(Customer, "{id}", [GET(fun _ -> async.Return "Hello, customer!"B)])
                    ])
            ])

    let resourceManager = Dyfrig.DyfrigResourceManager()
    let subscription = resourceManager.Start(spec)
    let client = resourceManager :> IObserver<_>

    testList "dyfrig" [
        testCase "valid GET /" <| fun _ ->
            let out = new MemoryStream()
            let env = new Environment(
                            requestMethod = "GET",
                            requestScheme = "http",
                            requestPathBase = "/",
                            requestPath = "",
                            requestQueryString = "",
                            requestProtocol = "HTTP/1.1",
                            requestHeaders = (Dictionary<_,_>(StringComparer.Ordinal) :> IDictionary<_,_>),
                            responseBody = (out :> Stream))
            client.OnNext(env, out :> Stream)
            async {
                do! Async.Sleep 500
                let bytes = out.ToArray()
                let result = Encoding.ASCII.GetString(bytes)
                test <@ result = "Hello, root!" @>
            }
            |> Async.RunSynchronously

//        testCase "valid GET /about" <| fun _ ->
//            let out = new MemoryStream()
//            let env = new Environment(
//                            requestMethod = "GET",
//                            requestScheme = "http",
//                            requestPathBase = "/",
//                            requestPath = "about",
//                            requestQueryString = "",
//                            requestProtocol = "HTTP/1.1",
//                            requestHeaders = (Dictionary<_,_>(StringComparer.Ordinal) :> IDictionary<_,_>),
//                            responseBody = (out :> Stream))
//            client.OnNext(env, out :> Stream)
//            async {
//                do! Async.Sleep 500
//                let bytes = out.ToArray()
//                let result = Encoding.ASCII.GetString(bytes)
//                test <@ result = "Hello, about!" @>
//            }
//            |> Async.RunSynchronously

//        testCase "valid GET /customers" <| fun _ ->
//            let out = new MemoryStream()
//            let env = new Environment(
//                            requestMethod = "GET",
//                            requestScheme = "http",
//                            requestPathBase = "/",
//                            requestPath = "customers",
//                            requestQueryString = "",
//                            requestProtocol = "HTTP/1.1",
//                            requestHeaders = (Dictionary<_,_>(StringComparer.Ordinal) :> IDictionary<_,_>),
//                            responseBody = (out :> Stream))
//            client.OnNext(env, out :> Stream)
//            async {
//                do! Async.Sleep 500
//                let bytes = out.ToArray()
//                let result = Encoding.ASCII.GetString(bytes)
//                test <@ result = "Hello, customers!" @>
//            }
//            |> Async.RunSynchronously
    ]

[<EntryPoint>]
let main args = defaultMainThisAssembly args
