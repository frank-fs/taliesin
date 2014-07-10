taliesin
========

OWIN routing middleware using F# Agents

[![Build status](https://ci.appveyor.com/api/projects/status/b3erdf5knem9v0cy)](https://ci.appveyor.com/project/panesofglass/taliesin)

[![Build Status](https://travis-ci.org/frank-fs/taliesin.svg?branch=master)](https://travis-ci.org/frank-fs/taliesin)

Hello, world
--------
``` fsharp
namespace FSharpWeb4

open Owin
open Microsoft.Owin
open System
open System.Net
open System.Net.Http
open System.Web
open Dyfrig
open Taliesin

type Resources = Home
    with
    interface IUriRouteTemplate with
        member x.UriTemplate = "/"

module App =
    let makeHttpHandler statusCode (data: byte[]) =
        let handler (request: HttpRequestMessage) =
            let content = new ByteArrayContent(data)
            content.Headers.ContentLength <- Nullable data.LongLength
            content.Headers.ContentType <- Headers.MediaTypeHeaderValue("text/plain")
            new HttpResponseMessage(statusCode, Content = content, RequestMessage = request)
            |> async.Return
        Dyfrig.SystemNetHttpAdapter.fromAsyncSystemNetHttp handler

    let spec = RouteLeaf(Home, [GET(makeHttpHandler HttpStatusCode.OK "Hello from Taliesin!"B)])

type Startup() =
    member x.Configuration(app: IAppBuilder) =
        let taliesin = new ResourceManager<Resources>()
        let subscription = taliesin.Start(App.spec)
        app.Use(fun _ -> taliesin.Invoke) |> ignore

type Global() =
    inherit System.Web.HttpApplication() 

[<assembly:OwinStartup(typeof<Startup>)>]
do ()
```
