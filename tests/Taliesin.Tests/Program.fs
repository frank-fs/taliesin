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

module internal DyfrigConfig =
    let requestMethod (env: Environment) = env.RequestMethod
    let send (out: Stream) (response: byte[]) = out.AsyncWrite(response)
    let notAllowed allowed env =
        allowed
        |> List.reduce (fun a b -> a + " " + b)
        |> sprintf "405 Method Not Allowed. Try one of %s"
        |> System.Text.Encoding.ASCII.GetBytes
        |> async.Return
    let uriMatcher uriTemplate (env: Environment) =
        // TODO: Account for URI template patterns
        uriTemplate = env.RequestPathBase + env.RequestPath

type DyfrigResourceManager() =
    inherit ResourceManager<Environment,byte[],Routes>(
        DyfrigConfig.requestMethod,
        DyfrigConfig.send,
        DyfrigConfig.notAllowed,
        DyfrigConfig.uriMatcher)

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

    let resourceManager = DyfrigResourceManager()
    let subscription = resourceManager.Start(spec)
    let client = resourceManager :> IObserver<_>

    testList "dyfrig" [
        testCase "valid GET /" <| fun _ ->
            use out = new MemoryStream()
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

        testCase "valid GET /about" <| fun _ ->
            use out = new MemoryStream()
            let env = new Environment(
                            requestMethod = "GET",
                            requestScheme = "http",
                            requestPathBase = "/",
                            requestPath = "about",
                            requestQueryString = "",
                            requestProtocol = "HTTP/1.1",
                            requestHeaders = (Dictionary<_,_>(StringComparer.Ordinal) :> IDictionary<_,_>),
                            responseBody = (out :> Stream))
            client.OnNext(env, out :> Stream)
            async {
                do! Async.Sleep 500
                let bytes = out.ToArray()
                let result = Encoding.ASCII.GetString(bytes)
                test <@ result = "Hello, about!" @>
            }
            |> Async.RunSynchronously

        testCase "valid GET /customers" <| fun _ ->
            use out = new MemoryStream()
            let env = new Environment(
                            requestMethod = "GET",
                            requestScheme = "http",
                            requestPathBase = "/",
                            requestPath = "customers",
                            requestQueryString = "",
                            requestProtocol = "HTTP/1.1",
                            requestHeaders = (Dictionary<_,_>(StringComparer.Ordinal) :> IDictionary<_,_>),
                            responseBody = (out :> Stream))
            client.OnNext(env, out :> Stream)
            async {
                do! Async.Sleep 500
                let bytes = out.ToArray()
                let result = Encoding.ASCII.GetString(bytes)
                test <@ result = "Hello, customers!" @>
            }
            |> Async.RunSynchronously
    ]

[<EntryPoint>]
let main args = defaultMainThisAssembly args
