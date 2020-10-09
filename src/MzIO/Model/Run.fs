namespace MzIO.Model


open System.Collections.Generic
open MzIO.Model
open Newtonsoft.Json


/// Base class of a ms run which is associated to a sample description.
[<AbstractClass>]
type RunBase ( [<JsonProperty("ID")>] id:string, sampleID:string) =

    inherit ModelItem(id)

    let mutable sampleID = sampleID

    [<JsonConstructor>]
    new(id:string) = RunBase(id, null)
    
    new() = RunBase("id")

    [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
    member this.SampleID
        with get() = sampleID
            and set(value) = 
                if value = sampleID then ()
                else
                    this.NotifyPropertyChaning("SampleID")
                    sampleID <- value
                    this.NotifyPropertyChanged("SampleID")

/// Expansible description of a ms run in a mz data model.
/// Represents the entry point to the storage of peak lists that are result
/// of instrument scans or data processings.
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn, IsReference = true)>]
type Run [<JsonConstructor>] (id:string, sampleID:string, defaultInstrumentID:string, defaultSpectrumProcessing, defaultChromatogramProcessing) =

    inherit RunBase(id, sampleID)

    let mutable defaultInstrumentID             = defaultInstrumentID
    
    let mutable defaultSpectrumProcessing       = defaultSpectrumProcessing

    let mutable defaultChromatogramProcessing   = defaultChromatogramProcessing

    new (id:string, defaultInstrument, defaultSpectrumProcessing, defaultChromatogramProcessing) = Run(id, null, defaultInstrument, defaultSpectrumProcessing, defaultChromatogramProcessing)

    new(id:string, sample, defaultInstrument) = Run(id, sample, defaultInstrument, new DataProcessing(), new DataProcessing())

    //new(id:string, sample) = Run(id, sample, new Instrument(), new DataProcessing(), new DataProcessing())

    new(id:string, defaultInstrument) = Run(id, null, defaultInstrument, new DataProcessing(), new DataProcessing())

    //new(id:string) = Run(id, null, new Instrument(), new DataProcessing(), new DataProcessing())

    new() = Run("id", null, null, new DataProcessing(), new DataProcessing())

    [<JsonProperty(Required = Required.Always, ObjectCreationHandling = ObjectCreationHandling.Reuse)>]
    member this.DefaultInstrumentID
        with get() = defaultInstrumentID 
            and set(value) = 
                if value = defaultInstrumentID then ()
                else
                    this.NotifyPropertyChaning("DefaultInstrumentID")
                    defaultInstrumentID <- value
                    this.NotifyPropertyChanged("DefaultInstrumentID")

    [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
    member this.DefaultSpectrumProcessing
        with get() = defaultSpectrumProcessing 
            and set(value) = 
                if value = defaultSpectrumProcessing then ()
                else
                    this.NotifyPropertyChaning("DefaultSpectrumProcessing")
                    defaultSpectrumProcessing <- value
                    this.NotifyPropertyChanged("DefaultSpectrumProcessing")

    [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
    member this.DefaultChromatogramProcessing
        with get() = defaultChromatogramProcessing 
            and set(value) = 
                if value = defaultChromatogramProcessing then ()
                else
                    this.NotifyPropertyChaning("DefaultChromatogramProcessing")
                    defaultChromatogramProcessing <- value
                    this.NotifyPropertyChanged("DefaultChromatogramProcessing")

/// Expansible description of a ms run in a project model.
/// Represents the entry point to the storage of peak lists that are result
/// of instrument scans or data processings.
/// Not implemented fully yet.
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn, IsReference = true)>]
type ProjectRun [<JsonConstructor>] ( [<JsonProperty("ID")>] id:string, sampleID:string) =
    
    inherit RunBase(id, sampleID)

    let mutable runReference = new RunReference()

    new(id) = ProjectRun(id, null)

    new() = ProjectRun("id", null)

    member this.RunBase = base.ID

    [<JsonProperty(NullValueHandling=NullValueHandling.Ignore)>]
    member this.RunReference
        with get() = runReference 
            and set(value) = 
                if value = runReference then ()
                else
                    this.NotifyPropertyChaning("RunReference")
                    runReference <- value
                    this.NotifyPropertyChanged("RunReference")

/// The model item container for all ms runs of this experiment.
[<Sealed>]
type RunList [<JsonConstructor>] () =

    inherit ObservableModelItemCollection<Run>()



/// The project item container for ms runs.
/// Not implemented fully yet.
[<Sealed>]
type ProjectRunList [<JsonConstructor>] () =

    inherit ObservableModelItemCollection<ProjectRun>()
