﻿namespace Microsoft.FSharp.Core

open System
open System.Runtime.CompilerServices

module Validation =
    let puree = Choice1Of2

    let (<*>) f x = 
        match f,x with
        | Choice1Of2 f, Choice1Of2 x   -> Choice1Of2 (f x)
        | Choice2Of2 e, Choice1Of2 x   -> Choice2Of2 e
        | Choice1Of2 f, Choice2Of2 e   -> Choice2Of2 e
        | Choice2Of2 e1, Choice2Of2 e2 -> Choice2Of2 (e1 @ e2)

    let map f o =
        match o with
        | Choice1Of2 x -> f x |> Choice1Of2
        | Choice2Of2 x -> Choice2Of2 x

    let inline (<!>) f x = map f x
    let inline lift2 f a b = f <!> a <*> b

    let inline ( *>) a b = lift2 (fun _ z -> z) a b
    let inline ( <*) a b = lift2 (fun z _ -> z) a b

    let (>>=) m f = 
        match m with
        | Choice1Of2 x -> f x
        | Choice2Of2 x -> Choice2Of2 x

    let (>>.) m1 m2 = m1 >>= (fun _ -> m2)

    let inline flip f a b = f b a
    let inline cons a b = a::b
    let seqValidator f = 
        let zero = puree []
        Seq.map f >> Seq.fold (lift2 (flip cons)) zero

open Validation

[<Extension>]
type FSharpChoice =

    [<Extension>]
    static member Match (c, f1: Func<_,_>, f2: Func<_,_>) =
        match c with
        | Choice1Of2 x -> f1.Invoke x
        | Choice2Of2 y -> f2.Invoke y

    [<Extension>]
    static member Match (c, f1: Action<_>, f2: Action<_>) =
        match c with
        | Choice1Of2 x -> f1.Invoke x
        | Choice2Of2 y -> f2.Invoke y

    [<Extension>]
    static member Match (c, f1: Func<_,_>, f2: Func<_,_>, f3: Func<_,_>) =
        match c with
        | Choice1Of3 x -> f1.Invoke x
        | Choice2Of3 x -> f2.Invoke x
        | Choice3Of3 x -> f3.Invoke x

    [<Extension>]
    static member Match (c, f1: Action<_>, f2: Action<_>, f3: Action<_>) =
        match c with
        | Choice1Of3 x -> f1.Invoke x
        | Choice2Of3 x -> f2.Invoke x
        | Choice3Of3 x -> f3.Invoke x

    [<Extension>]
    static member SelectMany (o, f: Func<_,_>) =
        match o with
        | Choice1Of2 x -> f.Invoke x
        | Choice2Of2 x -> Choice2Of2 x // error

    [<Extension>]
    static member SelectMany (o, f: Func<_,_>, mapper: Func<_,_,_>) =
        let r = FSharpChoice.SelectMany(o, f)
        match o,r with
        | Choice1Of2 x, Choice1Of2 y -> mapper.Invoke(x,y) |> Choice1Of2
        | Choice2Of2 x, _ -> Choice2Of2 x
        | _, Choice2Of2 x -> Choice2Of2 x

    [<Extension>]
    static member Select (o, f: Func<_,_>) = map f.Invoke o

    [<Extension>]
    static member Ap (f: Choice<Func<_,_>, _>, x) =
        match f,x with
        | Choice1Of2 f, Choice1Of2 x -> Choice1Of2 (f.Invoke x)
        | Choice2Of2 e, _            -> Choice2Of2 e
        | _           , Choice2Of2 e -> Choice2Of2 e


    // validation

    static member Error (x: string) = Choice2Of2 [x]
    static member Errors x : Choice<_, string list> = Choice2Of2 x
    static member Ok x : Choice<_, string list> = Choice1Of2 x

    static member Validator (p: _ Predicate, errorMsg: string) =
        let v x = 
            if p.Invoke x
                then FSharpChoice.Ok x
                else FSharpChoice.Error errorMsg
        Func<_,_>(v)

    [<Extension>]
    static member ApVG (f: Choice<Func<_,_>, _>, x, mappend) =
        match f,x with
        | Choice1Of2 f, Choice1Of2 x   -> Choice1Of2 (f.Invoke x)
        | Choice2Of2 e, Choice1Of2 x   -> Choice2Of2 e
        | Choice1Of2 f, Choice2Of2 e   -> Choice2Of2 e
        | Choice2Of2 e1, Choice2Of2 e2 -> Choice2Of2 (mappend e1 e2)

    [<Extension>]
    static member ApV (f: Choice<Func<_,_>, _>, x) =
        FSharpChoice.ApVG(f, x, (@))

    [<Extension>]
    static member PureValidate x : Choice<_, string list> = Choice1Of2 x

    static member EnumerableValidator (f: Func<'a, Choice<'a, string list>>) : Func<'a seq, Choice<'a seq, string list>> =
        let zero = Choice2Of2 []
        let cons a b = a::b
        let ff coll =
            coll
            |> Seq.map f.Invoke
            |> Seq.fold (fun c e -> cons <!> e <*> c) zero
            |> map (fun x -> x :> _ seq)
        Func<_,_>(ff)

    // constructors

    static member New1Of2<'a,'b> (a: 'a) : Choice<'a,'b> = Choice1Of2 a
    static member New2Of2<'a,'b> (b: 'b) : Choice<'a,'b> = Choice2Of2 b

    static member New1Of3<'a,'b,'c> (a: 'a) : Choice<'a,'b,'c> = Choice1Of3 a
    static member New2Of3<'a,'b,'c> (a: 'b) : Choice<'a,'b,'c> = Choice2Of3 a
    static member New3Of3<'a,'b,'c> (a: 'c) : Choice<'a,'b,'c> = Choice3Of3 a
        