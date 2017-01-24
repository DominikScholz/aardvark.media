﻿namespace Scratch

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

[<AutoOpen>]
module PickStuff = 


    type MouseEvent = Down of MouseButtons | Move | Click of MouseButtons | Up of MouseButtons

    type PickOccurance = { 
        mouse : MouseEvent
        point : V3d 
     }


    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module PickOccurance =
        let position (p : PickOccurance) = p.point

    module Mouse =
        let move (p : PickOccurance) = p.mouse = Move
        let down (p : PickOccurance) = match p.mouse with | Down b -> true | _ -> false  
        let down' (button : MouseButtons) (p : PickOccurance) = match p.mouse with | Down b when b = button -> true | _ -> false 

    type PickOperation<'msg> = PickOccurance -> Option<'msg>

    module Pick =
        let ignore = []

    
    let on (p : PickOccurance -> bool) (r : V3d -> 'msg) : PickOperation<'msg> = 
        fun pickOcc -> 
            if p pickOcc then 
                Some (r (PickOccurance.position pickOcc)) 
            else None    
    
    type Hits<'msg> = list<float * list<PickOperation<'msg>>>
    
    type GlobalPick = { mouseEvent : MouseEvent; ray : Ray3d; hits : bool }
//    module GlobalPick =
//        let map (f : 'a -> 'b) (p : GlobalPick<'a>) =
//            { 
//                ray = p.ray
//                mouseEvent = p.mouseEvent
//                hits =  p.hits |> List.map (fun (d,p) -> (d,p |> List.map (fun pf -> fun p -> pf p |> Option.map f)))
//            }

    module Primitives =

        type Primitive = 
            | Sphere      of Sphere3d
            | Cone        of center : V3d * dir : V3d * height : float * radius : float
            | Cylinder    of center : V3d * dir : V3d * height : float * radius : float
            | Quad        of Quad3d 

        let hitPrimitive (p : Primitive) (trafo : Trafo3d) (ray : Ray3d) action =
            let mutable ha = RayHit3d.MaxRange
            match p with
                | Sphere s -> 
                    let transformed = trafo.Forward.TransformPos(s.Center)
                    let mutable ha = RayHit3d.MaxRange
                    if ray.HitsSphere(transformed,s.Radius,0.0,Double.PositiveInfinity, &ha) then
                        [ha.T, action]
                    else []
                | Cone(center,dir,height,radius) | Cylinder(center,dir,height,radius) -> 
                    let cylinder = Cylinder3d(trafo.Forward.TransformPos center,trafo.Forward.TransformPos (center+dir*height),radius)
                    let mutable ha = RayHit3d.MaxRange
                    if ray.Hits(cylinder,0.0,Double.MaxValue,&ha) then
                        [ha.T, action]
                    else []
                | Quad q -> 
                    let transformed = Quad3d(q.Points |> Seq.map trafo.Forward.TransformPos)
                    if ray.HitsPlane(Plane3d.ZPlane,0.0,Double.MaxValue,&ha) then [ha.T, action]
                    else []

    module List =
        let updateAt i f xs =
            let rec work current xs =
                match xs with 
                    | x::xs -> 
                        if i = current then f x :: xs
                        else x :: work (current+1) xs
                    | [] -> []
            work 0 xs


module AnotherSceneGraph =

    open Primitives

    type ISg<'msg> = inherit ISg

    let cylinder c d h r = Cylinder(c,d,h,r)

    type AbstractApplicator<'msg>(child : IMod<ISg<'msg>>) =
        interface IApplicator with
            member x.Child = child |> Mod.map (fun a -> a :> ISg)
        member x.Child = child
        new(s : ISg<'msg>) = AbstractApplicator<'msg>(Mod.constant s)

    type Group<'msg>(xs : aset<ISg<'msg>>) =
        interface ISg<'msg>
        interface IGroup with
            member x.Children = xs |> ASet.map (fun a -> a :> ISg)

        member x.Children = xs

        new(l : list<ISg<'msg>>) = Group<'msg>(ASet.ofList l)

    type Transform<'msg>(trafo : IMod<Trafo3d>, xs : list<ISg<'msg>>) =
        inherit Sg.TrafoApplicator(trafo, Group xs)
        interface ISg<'msg>

    type Colored<'msg>(color : IMod<C4b>, xs : list<ISg<'msg>>) =
        inherit AbstractApplicator<'msg>(Group xs)
        interface ISg<'msg>
        member x.Color = color

    type On<'msg>(picks : list<PickOperation<'msg>>, children : list<ISg<'msg>>) =
        inherit AbstractApplicator<'msg>(Group children)
        member x.PickOperations = picks
        interface ISg<'msg>

    type Leaf<'msg>(xs : Primitive) =
        interface ISg<'msg>
        member x.Primitive = xs

    type Conv<'msg>(applicator : ISg -> ISg, children : ISg<'msg>) =
        let child = Mod.constant (applicator children)
        interface ISg<'msg>
        interface IApplicator with
            member x.Child = child
        member x.SceneGraphChild = child
        member x.Children = children

    let private conv app xs = Conv<_>(app,xs) :> ISg<'msg>

    type Map<'a,'b>(f : 'a -> 'b, source : ISg<'a>) =
        interface ISg<'b>
        member x.F = f
        member x.Source = source

    open Aardvark.Base.Ag
    open Aardvark.SceneGraph.Semantics
    [<Semantic>]
    type LeafSemantics() =
        
        member x.InhColor(c : Colored<'msg>) =
            c.Child?InhColor <- c.Color

        member x.InhColor(r : Root<ISg>) =
            r.Child?InhColor <- Mod.constant C4b.White

        member x.RenderObjects(c : Conv<'msg>) : aset<IRenderObject> =
            ASet.bind (fun s -> s?RenderObjects()) c.SceneGraphChild 

        member x.RenderObjects(m : Map<_,_>) : aset<IRenderObject> =
            m.Source?RenderObjects()

        member x.RenderObjects(l : Leaf<'msg>) =
            match l.Primitive with
                | Sphere s -> 
                    Sg.sphere 5 (l?InhColor) (Mod.constant s.Radius) |> Sg.transform (Trafo3d.Translation s.Center)
                    |> Semantic.renderObjects
                | Cone(c,d,h,r) -> 
                    l?InhColor |> Mod.map (fun (color : C4b) -> IndexedGeometryPrimitives.solidCone c d h r 10 color |> Sg.ofIndexedGeometry) 
                    |> Sg.dynamic
                    |> Semantic.renderObjects
                | Cylinder(c,d,h,r) ->
                    l?InhColor |> Mod.map (fun (color : C4b) -> IndexedGeometryPrimitives.solidCylinder c d h r r 10 color |> Sg.ofIndexedGeometry) 
                    |> Sg.dynamic
                    |> Semantic.renderObjects
                | Quad p -> 
                    let vertices = p.Points |> Seq.map V3f |> Seq.toArray
                    let index = [| 0; 1; 2; 0; 2; 3 |]
                    let c : IMod<C4b> = l?InhColor
                    let colors = c |> Mod.map  (fun c -> Array.replicate vertices.Length c)
                    let normals = Array.replicate vertices.Length (p.Edge03.Cross(p.P2-p.P0)).Normalized
                    let ig = IndexedGeometry(IndexedGeometryMode.TriangleList, index, SymDict.ofList [DefaultSemantic.Positions, vertices :> Array; DefaultSemantic.Normals, normals :> System.Array], SymDict.empty)
                    ig
                     |> Sg.ofIndexedGeometry
                     |> Sg.vertexAttribute DefaultSemantic.Colors colors
                     |> Semantic.renderObjects
      
      

    type PickObject<'msg> =
        {
            trafo : IMod<Trafo3d>
            primitive : Primitive
            actions : list<PickOperation<'msg>>
        }  
        

    module PickObject =
        let map ( f : 'a -> 'b) (p : PickObject<'a>) : PickObject<'b> =
            {
                trafo = p.trafo
                actions = List.map (fun o -> fun kind -> Option.map f (o kind)) p.actions
                primitive = p.primitive
            }  


    [<AutoOpen>]
    module SgExt = 
        type ISg<'msg> with
            member x.PickObjects() : aset<PickObject<'msg>> = x?PickObjects()
            member x.PickOperations : list<PickOperation<'msg>> = x?PickOperations

    [<Semantic>]
    type PickingSemantics() =
    

        member x.PickOperations(o : Root<ISg<'msg>>) =
            o.Child?PickOperations <- List.empty<PickOperation<'msg>>

        member x.PickOperations(o : On<'msg>) =
            o.Child?PickOperations <- o.PickOperations

        member x.PickOperations(o : Conv<'msg>) =
            o.Children?PickOperations <- o.PickOperations

        member x.PickObjects(o : Map<'a,'b>) : aset<PickObject<'b>> =
            let pi : aset<PickObject<'a>> = o.Source.PickObjects()
            ASet.map (PickObject.map o.F) pi


        member x.PickObjects(g : Conv<'msg>) : aset<PickObject<'msg>> =
            g.Children?PickObjects()

        member x.PickObjects(g : Group<'msg>) : aset<PickObject<'msg>> =
            g.Children |> ASet.collect (fun s -> s.PickObjects())

        member x.PickObjects(s : Transform<'msg>) : aset<PickObject<'msg>> =
            s.Child |> ASet.bind (fun c -> c?PickObjects())
               
        member x.PickObjects(c : AbstractApplicator<'msg>) : aset<PickObject<'msg>> =
            c.Child |> ASet.bind (fun c -> c?PickObjects())
                   
        member x.PickObjects(l : Leaf<'msg>)  : aset<PickObject<'msg>> =
            match l.PickOperations with
                | [] -> ASet.empty
                | ops -> 
                    ASet.single {
                        trafo = l.ModelTrafo
                        primitive = l.Primitive
                        actions = ops
                    }



    module Scene =

        let transform t xs = Transform<'msg>(t,xs) :> ISg<'msg>
        let translate x y z xs = Transform<'msg>(Trafo3d.Translation(x,y,z) |> Mod.constant, xs) :> ISg<_>
        let translate' x y z c = Transform<'msg>(Trafo3d.Translation(x,y,z) |> Mod.constant, List.singleton c) :> ISg<_>
        let transform' t x = Transform<'msg>(t,[x]) :> ISg<'msg>
        let colored c xs = Colored<'msg>(c,xs) :> ISg<'msg>
        let colored' c x = Colored<'msg>(c,x |> List.singleton) :> ISg<'msg>
        let pick picks xs = On<'msg>(picks,xs) :> ISg<'msg>
        let group (xs : list<_>) = Group<'msg>(xs) :> ISg<'msg>
        let agroup (xs : aset<_>) = Group<'msg>(xs) :> ISg<'msg>
        let leaf x = Leaf<'msg>(x) :> ISg<'msg> 
        let render picks p = pick picks [leaf p]
        let map f (a : ISg<'a>) : ISg<'b> = Map<_,_>(f,a) :> ISg<'b>
        let uniform name value xs = conv (Sg.uniform name value) xs
        let effect effects xs = conv (Sg.effect effects) xs
        let viewTrafo viewTrafo xs = conv (Sg.viewTrafo viewTrafo) xs
        let projTrafo projTrafo xs = conv (Sg.projTrafo projTrafo) xs
        let camera camera xs = conv (Sg.camera camera) xs
        let camera' camera xs = conv (Sg.camera camera) (group xs)

    


module Elmish3DADaptive =

    open AnotherSceneGraph
    open Fablish

    module Ext =

        type Direction = Up | Down

        type Sub<'msg> = 
            | NoSub
            | TimeSub of TimeSpan * (DateTime -> 'msg) 
            | Many of list<Sub<'msg>>
            | MouseClick of (MouseButtons -> PixelPosition -> Option<'msg>)
            | Mouse of (Direction -> MouseButtons -> PixelPosition -> Option<'msg>)
            | MouseMove of (PixelPosition * PixelPosition -> 'msg)
                
        module Sub =
            let leaves s =
                match s with
                    | NoSub -> []
                    | TimeSub _ -> [s]
                    | Many xs -> xs
                    | MouseClick _ -> [s]
                    | Mouse _ -> [s]
                    | MouseMove _ -> [s]
            let mouseClicks s = s |> leaves |> List.choose (function | MouseClick f -> Some f | _ -> None)
            let mouseThings s = s |> leaves |> List.choose (function | Mouse f -> Some f | _ -> None)
            let moves s = s |> leaves |> List.choose (function | MouseMove m -> Some m | _ -> None)

        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Subscriptions =
            let none = fun _ -> NoSub


    type App<'model,'mmodel,'msg,'view> =
        {
            initial   : 'model
            update    :  Env<'msg> -> 'model  -> 'msg  -> 'model
            view      : 'mmodel    -> 'view

            subscriptions : 'model -> Ext.Sub<'msg>

            ofPickMsg : 'model     -> GlobalPick  -> list<'msg>
        }

    type Unpersist<'immut,'mut> =
        {
            unpersist : 'immut -> ReuseCache -> 'mut
            apply     : 'immut -> 'mut -> ReuseCache -> unit
        }

    let inline unpersist< ^immut, ^mut when ^immut : (member ToMod : ReuseCache -> ^mut) and ^mut : (member Apply : ^immut * ReuseCache -> unit) > () : Unpersist< ^immut,^ mut> = 
        let inline u  (immut : ^immut) (scope : ReuseCache) = (^immut : (member ToMod : ReuseCache -> ^mut) (immut,scope))
        let inline a  (immut : ^immut) (mut : ^mut) (scope : ReuseCache) = (^mut : (member Apply : ^immut * ReuseCache -> unit) (mut,immut,scope))
        { unpersist = u; apply = a }

    type Running<'model,'msg> =
        {
            send : 'msg -> 'model
            emitModel : 'model -> unit
            sg : ISg
        }

    let createAppAdaptive (keyboard : IKeyboard) (mouse : IMouse) (viewport : IMod<Box2i>) (camera : IMod<Camera>) (unpersist :  Unpersist<'model,'mmodel>) (onMessageO : Option<Fablish.CommonTypes.Callback<'model,'msg>>) (app : App<'model,'mmodel,'msg, ISg<'msg>>)  =

        let model = Mod.init app.initial

        let transformPixel (p : PixelPosition) =
            let p = PixelPosition(p.Position, Mod.force viewport)
            p

        let reuseCache = ReuseCache()
        let mmodel = unpersist.unpersist model.Value reuseCache

        let mutable onMessage = Unchecked.defaultof<_>

        let mutable moves = []
        let mutable mouseActions = []
        let mutable clicks = []

        let moveSub (oldP,newP) =
            moves |> List.map (fun f -> f (oldP,newP)) |> List.fold onMessage model.Value |> ignore

        let mouseActionsSub dir p =
            mouseActions |> List.choose (fun f -> f dir p (mouse.Position.GetValue())) |> List.fold onMessage model.Value |> ignore

        let mouseClicks p =
            clicks |> List.choose (fun f -> f p (mouse.Position.GetValue())) |> List.fold onMessage model.Value |> ignore

        mouse.Move.Values.Subscribe(moveSub) |> ignore
        mouse.Click.Values.Subscribe(mouseClicks) |> ignore
        mouse.Down.Values.Subscribe(fun p -> mouseActionsSub Ext.Direction.Down p) |> ignore
        mouse.Up.Values.Subscribe(fun p -> mouseActionsSub Ext.Direction.Up p) |> ignore

        let updateSubscriptions (m : 'model) =
            let subs = app.subscriptions m
            moves <- Ext.Sub.moves subs
            clicks <- Ext.Sub.mouseClicks subs
            mouseActions <- Ext.Sub.mouseThings subs
            ()

        let updateModel (m : 'model) =
            transact (fun () -> 
                model.Value <- m
                updateSubscriptions m |> ignore
                unpersist.apply m mmodel reuseCache
            )



        let mutable env = Unchecked.defaultof<_>

        let send msg =
            let m' = app.update env model.Value msg
            updateSubscriptions m'
            updateModel m'
            m'

        let emitEnv cmd = 
            match cmd with
                | NoCmd -> ()
                | Cmd cmd -> 
                    async {
                        let! msg = cmd
                        send msg |> ignore
                    } |> Async.Start

        env <- { run = emitEnv }

        onMessage <-
            match onMessageO with 
                | None -> (fun model msg -> let m' = app.update env model msg in updateModel m'; m')
                | Some v -> v

        let view = app.view mmodel
        let pickObjects = view.PickObjects()
        let pickReader = pickObjects.GetReader()

        let pick (r : Ray3d) : Hits<'msg> =
            pickReader.GetDelta() |> ignore
            let picks =
                pickReader.Content 
                 |> Seq.toList 
                 |> List.collect (fun p -> 
                        Primitives.hitPrimitive p.primitive (Mod.force p.trafo) r p.actions
                    )
            picks |> List.sortBy fst 

        let updatePickMsg (m : GlobalPick) (model : 'model) =
            app.ofPickMsg model m |> List.fold onMessage model

        let handleMouseEvent mouseEvent =
            let ray = mouse.Position |> Mod.force |> transformPixel |> Camera.pickRay (camera |> Mod.force)
            let picks = pick ray
            let mutable model = updatePickMsg { mouseEvent = mouseEvent; ray = ray; hits = List.isEmpty picks |> not }  model.Value
            match picks with
                | ((d,f)::_) -> 
                    for msg in f do
                        let occ : PickOccurance = { mouse = mouseEvent; point = ray.GetPointOnRay d }
                        match msg occ with
                            | Some r -> 
                                model <- onMessage model r
                            | _ -> ()
                | [] -> ()
            updateModel model 

        mouse.Move.Values.Subscribe(fun (oldP,newP) -> 
            handleMouseEvent MouseEvent.Move
        ) |> ignore

        mouse.Down.Values.Subscribe(fun p ->  
            handleMouseEvent (MouseEvent.Down p)
        ) |> ignore
 
        mouse.Click.Values.Subscribe(fun p ->  
            handleMouseEvent (MouseEvent.Click p)
        ) |> ignore

        mouse.Up.Values.Subscribe(fun p ->     
            handleMouseEvent (MouseEvent.Up p)
        ) |> ignore

        { send = send; sg = view :> ISg; emitModel = updateModel }

    let inline createAppAdaptiveD (keyboard : IKeyboard) (mouse : IMouse) (viewport : IMod<Box2i>) (camera : IMod<Camera>) (onMessage : Option<Fablish.CommonTypes.Callback<'model,'msg>>) (app : App<'model,'mmodel,'msg, ISg<'msg>>)=
        createAppAdaptive keyboard mouse viewport camera ( unpersist ()) onMessage app


