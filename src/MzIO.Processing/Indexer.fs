﻿namespace MzIO.Processing

open System

module Indexer =

    type IndexedItem<'a, 'b when 'a :> IComparable> = { //TODO 'b nicht generic
            Key      : 'a
            Item     : 'b
        }
    
    let createIndexItemBy key item = {
        Key=key; Item=item }
    
    let getItem (idxedItem: IndexedItem<'a,'b>) = 
        idxedItem.Item

    type IndexedItemCollection<'a,'b  when 'a :> IComparable> = IndexedItem<'a,'b > []

    type IndexedItemGenerator<'a,'b when 'a :> IComparable> = 'b -> IndexedItem<'a,'b>
    
    /// Returns a IndexedItemCollection sorted by the Index. The Index is generated by a function of type IndexedItemGenerator
    let sortedIdxItemCollection (gen: IndexedItemGenerator<'a,'b>) (data: seq<'b>) :IndexedItemCollection<'a,'b> =
        let arr = 
            data
            |> Seq.map gen
            |> Seq.toArray
            |> Array.sortBy (fun ii -> ii.Key)
        arr

    /// Applies the f1 to every element in the list but the last element. The last element is used as a input parameter for f2.
    /// Returns a new Collection  
    let rec mapConsBy (f1: ('a->'a->'b)) (f2: ('a->'b)) acc (data:list<'a>) =
        match data with
        | h1::h2::tail -> mapConsBy f1 f2 ((h1,(f1 h1 h2))::acc) (h2::tail)
        | h::[]        -> mapConsBy f1 f2 ((h,(f2 (h)))::acc) ([])
        | []           -> acc

    
    /// Walks a Array till the value of the Array element is greater or equal to the parameter 'upperborder'
    let rec walkTill upperBorder count acc (data: IndexedItemCollection<'a,'b>) = 
        if count = data.Length-1 then
                (data.[count].Item)::acc
        elif (data.[count].Key) >= upperBorder then
                acc
        else walkTill upperBorder (count+1) (data.[count].Item ::acc) data

    /// Returns Lists of IndexedItems.Items which are greater than the first value and smaller than the second value of the range parameter
    let getDataByRange  (range:('a*'a)) (data: IndexedItemCollection<'a,'b>)=
        let startIdx = 
                data |> Seq.tryFindIndex (fun indexItem -> indexItem.Key >= fst range )     
        walkTill (snd range) startIdx.Value [] data 
    
    /// 
    let splitByAdjacentRanges f1 (data: IndexedItemCollection<'a,'b>)  (range: ('a*'a) []) =
        let startIdx = Array.findIndex (fun x -> x.Key >= fst range.[0]) data
        let rec splitDataByRangeInnerF f1 rangeCount (dataCount:int) (acc: 'b list list) (data: IndexedItemCollection<'a,'b>) (range: ('a*'a) []) =
            if   rangeCount = range.Length then
                 acc
            else
                 let elementsInRange: 'b list = f1 (snd range.[rangeCount]) dataCount [] data
                 splitDataByRangeInnerF f1 (rangeCount+1) (dataCount+elementsInRange.Length) (elementsInRange::acc) data range
        splitDataByRangeInnerF f1 0 startIdx [] data range
                  
    ///
    let splitAdjacentByUpperBorder f1 (data: IndexedItemCollection<'a,'b>)  (upperVal:('a) []) =
        let rec splitDataByRangeInnerF f1 upperValCount (dataCount:int) (acc: 'b list list) (data: IndexedItemCollection<'a,'b>) (upperVal:('a) []) =
            if   upperValCount = upperVal.Length then
                 acc
            else
                 let elementsInRange: 'b list = f1 (upperVal.[upperValCount]) dataCount [] data
                 splitDataByRangeInnerF f1 (upperValCount+1) (dataCount+elementsInRange.Length) (elementsInRange::acc) data upperVal
        splitDataByRangeInnerF f1 0 0 [] data upperVal


