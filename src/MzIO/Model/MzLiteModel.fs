namespace MzLiteFSharp.Model


open MzLiteFSharp.Model
open Newtonsoft.Json


/// <summary>
/// Exposes the root class of the mz data model.
/// Captures the use of mass spectrometers, sample descriptions, the mz data generated 
/// and the processing of that data at the level of peak lists.
/// </summary>
[<JsonObject(MemberSerialization.OptIn)>]
type MzLiteModel
    (name:string, fileDescription:FileDescription, samples:SampleList, softwares:SoftwareList, 
     dataProcessings:DataProcessingList, instruments:InstrumentList, runs:RunList
    ) =

    inherit NamedItem(name)

    let mutable fileDescription' = fileDescription

    let mutable samples' = samples

    let mutable softwares' = softwares

    let mutable dataProcessings' = dataProcessings

    let mutable instruments' = instruments

    let mutable runs' = runs

    new(name, fileDescription, instruments) = 
        MzLiteModel(name, fileDescription, new SampleList(),  new SoftwareList(), new DataProcessingList(), 
                    instruments, new RunList())

    new(name:string, fileDescription:FileDescription) = 
        MzLiteModel(name, fileDescription, new SampleList(),  new SoftwareList(), new DataProcessingList(), 
                    new InstrumentList(), new RunList())

    [<JsonConstructor>]
    new(name) = 
        MzLiteModel(name, new FileDescription(), new SampleList(),  new SoftwareList(), new DataProcessingList(), 
                    new InstrumentList(), new RunList())

    new() = MzLiteModel("name", new FileDescription(), new SampleList(),  new SoftwareList(), new DataProcessingList(), 
                        new InstrumentList(), new RunList())

    [<JsonProperty(Required = Required.Always, ObjectCreationHandling = ObjectCreationHandling.Reuse)>]
    member this.FileDescription
        with get() = fileDescription'
        and set(value) = fileDescription' <- value

    [<JsonProperty>]
    member this.Samples
        with get() = samples'
        and set(value) = samples' <- value

    [<JsonProperty>]
    member this.Softwares
        with get() = softwares'
        and set(value) = softwares' <- value

    [<JsonProperty>]
    member this.DataProcessings
        with get() = dataProcessings'
        and set(value) = dataProcessings' <- value

    [<JsonProperty>]
    member this.Instruments
        with get() = instruments'
        and set(value) = instruments' <- value

    [<JsonProperty>]
    member this.Runs
        with get() = runs'
        and set(value) = runs' <- value

    //[<JsonProperty>]
    //member this.DynamicObject =
    //    (this :> DynamicObj).GetProperties false
