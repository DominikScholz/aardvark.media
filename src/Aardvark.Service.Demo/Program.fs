﻿open Aardvark.Service
open System

open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Application
open Aardvark.Application.WinForms
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.SceneGraph
open Aardvark.UI
open Aardvark.Rendering.Text
open Demo.TestApp
open Demo.TestApp.Mutable

open Aardvark.SceneGraph
open Suave
open Suave.Operators
open Suave.Filters
open Suave.WebPart

open System.Windows.Forms
open UI.Composed
open PRo3DModels.Mutable



// elm intro
// Composition. how to
// how to make things composable (reusable components, higher order functions, guidlines model messages)
// testing (elm debugger)
// einbinden vong html javascript

(* TODO:

* plane pickshape
* Sg.shader  for ISg<'msg>
* multiple picks along pickray

*)


let kitchenSink argv =
    Xilium.CefGlue.ChromiumUtilities.unpackCef()
    Chromium.init argv

    Ag.initialize()
    Aardvark.Init()
    use app = new OpenGlApplication()
    let runtime = app.Runtime

    //let a = Viewer.KitchenSinkApp.start()
    //let a = Aardvark.UI.Numeric.start()
    //let a = TreeViewApp.start()
    //let a = AnnotationProperties.start()
    //let a = SimpleTestApp.start()
    //let a = SimpleCompositionViewer.start()
    //let a = OrbitCameraDemo.start()
    //let a = NavigationModeDemo.start()
    //let a = BoxSelectionDemo.start()
    let a = DragNDrop.App.start()

    WebPart.startServer 4321 [ 
        MutableApp.toWebPart runtime a
        Suave.Files.browseHome
    ]  

    use form = new Form(Width = 1024, Height = 768)
    use ctrl = new AardvarkCefBrowser()
    ctrl.Dock <- DockStyle.Fill
    form.Controls.Add ctrl
    ctrl.StartUrl <- "http://localhost:4321/"

    ctrl.ShowDevTools()

    Application.Run form
    System.Environment.Exit 0

let modelviewer args =
    Viewer.Viewer.run args
    System.Environment.Exit 0

[<EntryPoint; STAThread>]
let main args =
    
    kitchenSink args
    //modelviewer args


    0

