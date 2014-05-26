//----------------------------------------------------------------------------
//
// Copyright (c) 2013-2014 Ryan Riley (@panesofglass)
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//----------------------------------------------------------------------------
namespace Taliesin

open System
open System.Collections.Generic
open System.IO
open System.Threading.Tasks
open Dyfrig

[<AutoOpen>]
/// F# extensions
module Extensions =
    type Microsoft.FSharp.Control.Async with
        static member AwaitTask(task: Task) =
            Async.AwaitTask(task.ContinueWith(Func<_,_>(fun _ -> ())))


/// Extensions to the Dyfrig `Environment` until the next version of Dyfrig is published.
module Dyfrig =
    
    // TODO: Update Dyfrig dependency to get this directly from env.GetRequestUri()
    type Environment with
        /// Reconstructs the base request URI from the component parts.
        member env.GetBaseUri() =
            if env.RequestHeaders.ContainsKey("Host") then
                env.RequestScheme + "://" +
                (env.RequestHeaders.["Host"].[0]) +
                if String.IsNullOrEmpty env.RequestPathBase then "/" else env.RequestPathBase
                |> Some
            else None

        /// Reconstructs the request URI from the component parts.
        member env.GetRequestUri() =
            if env.RequestHeaders.ContainsKey("Host") then
                env.RequestScheme + "://" +
                (env.RequestHeaders.["Host"].[0]) +
                env.RequestPathBase +
                env.RequestPath +
                if String.IsNullOrEmpty env.RequestQueryString then "" else "?" + env.RequestQueryString
                |> Some
            else None

/// Type alias for an OWIN environment dictionary.
type OwinEnv = IDictionary<string, obj>
/// Type alias for an OWIN `AppFunc`.
type OwinAppFunc = Func<OwinEnv, Task>
