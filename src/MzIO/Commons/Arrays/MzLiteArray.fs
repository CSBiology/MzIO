namespace MzIO.Commons.Arrays


open System.Linq
open System.Collections.Generic


/// <summary>
/// Defines a simple one dimensional interface for array-like data structures.
/// </summary>
/// <typeparam name="T"></typeparam>
        ////////////////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
///////////////////////////////////////////////////////DIFFERENCE\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
        ////////////////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
//Explicit interface implementation instead of implicit.
//Change idx to item.
type IMzLiteArray<'T> =
    
    inherit IEnumerable<'T>

    abstract member Length  : int

    abstract member Item    : int -> 'T with get

        ////////////////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
///////////////////////////////////////////////////////DIFFERENCE\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
        ////////////////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
//Explicit interface implementation instead of implicit.
//Change idx to item.
type ArrayWrapper<'T>(array:'T[]) =

    //let mutable array' = array

    new () = ArrayWrapper<'T>([||])
            
    //IEnumerable<'T> members 
    interface IEnumerable<'T> with
        member this.GetEnumerator() =
            array.AsEnumerable().GetEnumerator()

    //IEnumerable members 
    interface System.Collections.IEnumerable with
        member this.GetEnumerator() = 
            array.GetEnumerator()

    //IMzLiteArray<'T> members 
    interface IMzLiteArray<'T> with
        member this.Length = 
            array.Length

        member this.Item with get idx = array.[idx]

        ////////////////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
///////////////////////////////////////////////////////DIFFERENCE\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
        ////////////////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
//Explicit interface implementation instead of implicit.
//Change idx to item.
type ListWrapper<'T>(list:IList<'T>) =

    new () = ListWrapper<'T>(null)
            
    //member this.list = list

    //IEnumerable<'T> members 
    interface IEnumerable<'T> with
        member this.GetEnumerator() =
            list.AsEnumerable().GetEnumerator()

    //IEnumerable members 
    interface System.Collections.IEnumerable with
        member this.GetEnumerator() =
            list.ToArray().GetEnumerator()

    //IMzLiteArray<'T> members 
    interface IMzLiteArray<'T> with
        member this.Length = 
            list.Count

        member this.Item with get idx = list.[idx]


        ////////////////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
///////////////////////////////////////////////////////DIFFERENCE\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
        ////////////////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
//ArrayWrapper && ListWrapper lack default constructor which uses the array 
//on which the function is called.
type MzLiteArray =
    
    static member ToMzLiteArray<'T>(source:'T[]) =
        ArrayWrapper<'T>(source)

    static member ToMzLiteArray<'T>(source:IList<'T>) =
        ListWrapper<'T>(source)

    static member ToCLRArray<'T>(source:IMzLiteArray<'T>) =
        //let tmp = Array.create source.Length (source.[0])
        //for i=0 to source.Length-1 do
        //    tmp.[i] <- source.[i]
        //tmp
        Array.init source.Length (fun i -> source.[i])
        
    static member Empty<'T>() =
        ArrayWrapper<'T>([||])

module MzLiteArray =

    type ``[]``<'T> with

        member this.ToMzLiteArray<'T>() =
            ArrayWrapper<'T>(this)

    type IList<'T> with

        member this.ToMzLiteArray<'T>() =
            ListWrapper<'T>(this)

    type IMzLiteArray<'T> with 

        member this.ToCLRArray<'T>() =
            //let tmp = Array.create this.Length (this.[0])
            //for i=0 to this.Length-1 do
            //    tmp.[i] <- this.[i]
            //tmp
            Array.init this.Length (fun i -> this.[i])

