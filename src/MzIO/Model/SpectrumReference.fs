namespace MzLiteFSharp.Model


open System
open Newtonsoft.Json


[<Sealed>]
[<JsonObject(MemberSerialization = MemberSerialization.OptIn)>]
type SpectrumReference [<JsonConstructor>] (sourceFileID: string , spectrumID: string) =
    
    let mutable sourceFileID' = sourceFileID
    let mutable spectrumID' = spectrumID
    new() = SpectrumReference ("sourceFileID", "spectrumID")
    //spectrumReference with new variables or take type variables?
    member this.SpectrumReference (sourceFileID: string, spectrumID: string) =
        if spectrumID = null then
             raise (new System.ArgumentNullException ("spectrumID"))
        else sourceFileID' <- sourceFileID
             spectrumID' <- spectrumID
    member this.SpectrumReference (spectrumID: string) = this.SpectrumReference (null, spectrumID)
    [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
    member this.SourceFileID  
        with get()              = sourceFileID'
        and private set(value)  = sourceFileID' <- value
    [<JsonProperty(Required = Required.Always)>]
    member this.SpectrumID
        with get()              = spectrumID'
        and private set(value)  = spectrumID' <- value
    member this.IsExternal = this.SourceFileID <> null
    override this.Equals (obj: System.Object) =
        if Object.ReferenceEquals (this, obj) then true
        else
            //let other = if (obj :? SpectrumReference) then obj :?> SpectrumReference else null
            //let other = 
            //    match obj with 
            //    | :? SpectrumReference as spectrumReference -> spectrumReference
            //    | _ -> null
            // try
            // obj :?> SpectrumReference
            // with
            //     | :? System.InvalidCastException -> null
            if (obj :? SpectrumReference) then
                let other: SpectrumReference = obj :?> SpectrumReference    
                if this.IsExternal then
                    spectrumID'.Equals(other.SpectrumID) && sourceFileID'.Equals(other.SourceFileID)
                else
                    spectrumID'.Equals(other.SpectrumID)
            else false
    override this.GetHashCode() =
        if this.IsExternal then
            Tuple.Create(spectrumID', sourceFileID').GetHashCode()
        else
            spectrumID'.GetHashCode()