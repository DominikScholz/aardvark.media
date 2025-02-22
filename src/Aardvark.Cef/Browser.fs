﻿namespace Aardvark.Cef

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Cef.Internal

type Browser(signature : IFramebufferSignature, time : IMod<System.DateTime>, runtime : IRuntime, mipMaps : bool, size : IMod<V2i>) =
    let client = new Client(runtime, mipMaps, size)

    member x.ExecuteAsync(js : string) = client.ExecuteAsync js
    member x.Execute(js : string) = client.Execute js
    member x.GetViewportAsync(id : string) = client.GetViewportAsync id
    member x.GetViewport(id : string) = client.GetViewport id
    member x.SetFocus (v : bool) = client.SetFocus v
    member x.ReadPixel(pos : V2i) = client.ReadPixel pos
    
    member x.Keyboard = client.Keyboard
    member x.Mouse = client.Mouse
    member x.Events = client.Events
    member x.Texture = client.Texture
    member x.Version = client.Version
    member x.Size = client.Size
    member x.IsInitialized = client.IsInitialized
    
    member x.LoadUrlAsync (url : string) = client.LoadUrlAsync url
    member x.LoadUrl (url : string) = client.LoadUrl url
    member x.LoadHtmlAsync (code : string) = client.LoadHtmlAsync code
    member x.LoadHtml (code : string) = client.LoadHtml code

    member x.Runtime = runtime
    member x.FramebufferSignature = signature
    member x.Time = time
    member x.MipMaps = mipMaps

    new(ctrl : Aardvark.Application.IRenderControl) =
        new Browser(ctrl.FramebufferSignature, ctrl.Time, ctrl.Runtime, false, ctrl.Sizes)

    interface IDisposable with
        member x.Dispose() = (client :> IDisposable).Dispose()

module Chromium =
    let init'(windowless : bool) = Cef.init' windowless
    let init() = Cef.init()
    let shutdown() = Cef.shutdown()