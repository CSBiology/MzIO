namespace MzIO.Model


open MzIO.Model
open Newtonsoft.Json


/// Exposes the root class of the mz data model.
/// Captures the use of mass spectrometers, sample descriptions, the mz data generated 
/// and the processing of that data at the level of peak lists.
[<JsonObject(MemberSerialization.OptIn)>]
type MzIOModel
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
        MzIOModel(name, fileDescription, new SampleList(),  new SoftwareList(), new DataProcessingList(), 
                    instruments, new RunList())

    new(name:string, fileDescription:FileDescription) = 
        MzIOModel(name, fileDescription, new SampleList(),  new SoftwareList(), new DataProcessingList(), 
                    new InstrumentList(), new RunList())

    [<JsonConstructor>]
    new(name) = 
        MzIOModel(name, new FileDescription(), new SampleList(),  new SoftwareList(), new DataProcessingList(), 
                    new InstrumentList(), new RunList())

    new() = MzIOModel("name", new FileDescription(), new SampleList(),  new SoftwareList(), new DataProcessingList(), 
                        new InstrumentList(), new RunList())

    //[<JsonProperty(Required = Required.Always, ObjectCreationHandling = ObjectCreationHandling.Reuse)>]
    [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
    member this.FileDescription
        with get() = fileDescription'
        and set(value) = fileDescription' <- value

    //[<JsonProperty(Required = Required.AllowNull, ObjectCreationHandling = ObjectCreationHandling.Auto)>]
    [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
    member this.Samples
        with get() = samples'
        and set(value) = samples' <- value

    //[<JsonProperty(Required = Required.AllowNull, ObjectCreationHandling = ObjectCreationHandling.Auto)>]
    [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
    member this.Softwares
        with get() = softwares'
        and set(value) = softwares' <- value

    //[<JsonProperty(Required = Required.AllowNull, ObjectCreationHandling = ObjectCreationHandling.Auto)>]
    [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
    member this.DataProcessings
        with get() = dataProcessings'
        and set(value) = dataProcessings' <- value

    //[<JsonProperty(Required = Required.AllowNull, ObjectCreationHandling = ObjectCreationHandling.Auto)>]
    [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
    member this.Instruments
        with get() = instruments'
        and set(value) = instruments' <- value

    [<JsonProperty>]
    member this.Runs
        with get() = runs'
        and set(value) = runs' <- value

