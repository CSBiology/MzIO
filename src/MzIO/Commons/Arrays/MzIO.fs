namespace MzIO.Commons.Arrays


open System.Linq
open System.Collections.Generic


/// Defines a simple one dimensional interface for array-like data structures.
type IMzIOArray<'T> =
    
    inherit IEnumerable<'T>

    abstract member Length  : int

    abstract member Item    : int -> 'T with get

/// Wrap IMzIOArray as arrays.
type ArrayWrapper<'T>(array:'T[]) =

    new () = ArrayWrapper<'T>([||])
            
    interface IEnumerable<'T> with
        member this.GetEnumerator() =
            array.AsEnumerable().GetEnumerator()
 
    interface System.Collections.IEnumerable with
        member this.GetEnumerator() = 
            array.GetEnumerator()

    interface IMzIOArray<'T> with
        member this.Length = 
            array.Length

        member this.Item with get idx = array.[idx]

/// Wrap IMzIOArray as lists.
type ListWrapper<'T>(list:IList<'T>) =

    new () = ListWrapper<'T>(null)
            
    interface IEnumerable<'T> with
        member this.GetEnumerator() =
            list.AsEnumerable().GetEnumerator()

    interface System.Collections.IEnumerable with
        member this.GetEnumerator() =
            list.ToArray().GetEnumerator()
 
    interface IMzIOArray<'T> with
        member this.Length = 
            list.Count

        member this.Item with get idx = list.[idx]


/// Contains methods to convert objects to arrays, lists and IMzIOArrays.
type MzIOArray =
    
    static member ToMzIOArray<'T>(source:'T[]) =
        ArrayWrapper<'T>(source)

    static member ToMzIOArray<'T>(source:IList<'T>) =
        ListWrapper<'T>(source)

    static member ToCLRArray<'T>(source:IMzIOArray<'T>) =
        Array.init source.Length (fun i -> source.[i])
        
    static member Empty<'T>() =
        ArrayWrapper<'T>([||])

/// Contains methods to convert arrays and lists directly to IMzIOArrays.
module MzIOArray =

    type ``[]``<'T> with
        /// Convert this array to IMzIOArray of type 'T.
        member this.ToMzIOArray<'T>() =
            ArrayWrapper<'T>(this)

    type IList<'T> with
        /// Convert this list to IMzIOArray of type 'T.
        member this.ToMzIOArray<'T>() =
            ListWrapper<'T>(this)

    type IMzIOArray<'T> with 
        /// Convert this IMzIOArray to array of type 'T.
        member this.ToCLRArray<'T>() =
            Array.init this.Length (fun i -> this.[i])

