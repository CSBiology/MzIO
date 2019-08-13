namespace MzIO.Model


open System
open Newtonsoft.Json


/// Expansible description of a spectrum reference which also contains a refernece to the source file.
[<Sealed>]
[<JsonObject(MemberSerialization = MemberSerialization.OptIn)>]
type SpectrumReference [<JsonConstructor>] (sourceFileID: string , spectrumID: string) =
    
    let mutable sourceFileID' = sourceFileID
    let mutable spectrumID' =
        if String.IsNullOrWhiteSpace spectrumID then 
            raise (new System.ArgumentNullException ("spectrumID"))
        else
            spectrumID

    new(spectrumID) = new SpectrumReference ("sourceFileID", spectrumID)
    new()           = new SpectrumReference ("sourceFileID", "spectrumID")

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