namespace MzIO.Model


open MzIO.Model
open Newtonsoft.Json


/// <summary>
/// Base class of a ms run which is associated to a sample description.
/// </summary>
[<AbstractClass>]
type RunBase ( [<JsonProperty("ID")>] id:string, sample:Sample) =

    inherit ModelItem(id)

    let mutable sample = sample

    new(id:string) = RunBase(id, new Sample())
    
    new() = RunBase("id")

    [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
    member this.Sample
        with get() = sample
            and set(value) = 
                if value = sample then ()
                else
                    this.NotifyPropertyChaning("Sample")
                    sample <- value
                    this.NotifyPropertyChanged("Sample")

/// <summary>
/// Expansible description of a ms run in a mz data model.
/// Represents the entry point to the storage of peak lists that are result
/// of instrument scans or data processings.
/// </summary>
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn, IsReference = true)>]
type Run [<JsonConstructor>] (id:string, sample:Sample, defaultInstrument, defaultSpectrumProcessing, defaultChromatogramProcessing) =

    inherit RunBase(id, sample)

    let mutable defaultInstrument               = defaultInstrument
    
    let mutable defaultSpectrumProcessing       = defaultSpectrumProcessing

    let mutable defaultChromatogramProcessing   = defaultChromatogramProcessing

    new(id:string, sample, defaultInstrument) = Run(id, sample, defaultInstrument, new DataProcessing(), new DataProcessing())

    new(id:string, sample) = Run(id, sample, new Instrument(), new DataProcessing(), new DataProcessing())

    new(id:string, defaultInstrument) = Run(id, new Sample(), defaultInstrument, new DataProcessing(), new DataProcessing())

    new(id:string) = Run(id, new Sample(), new Instrument(), new DataProcessing(), new DataProcessing())

    new() = Run("id", new Sample(), new Instrument(), new DataProcessing(), new DataProcessing())

    //member this.Run = base.ID

    [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
    member this.DefaultInstrument
        with get() = defaultInstrument 
            and set(value) = 
                if value = defaultInstrument then ()
                else
                    this.NotifyPropertyChaning("DefaultInstrument")
                    defaultInstrument <- value
                    this.NotifyPropertyChanged("DefaultInstrument")

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

/// <summary>
/// Expansible description of a ms run in a project model.
/// Represents the entry point to the storage of peak lists that are result
/// of instrument scans or data processings.
/// </summary>
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn, IsReference = true)>]
type ProjectRun [<JsonConstructor>] ( [<JsonProperty("ID")>] id:string, sample:Sample) =
    
    inherit RunBase(id, sample)

    let mutable runReference = new RunReference()

    new(id) = ProjectRun(id, new Sample())

    new() = ProjectRun("id", new Sample())

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

/// <summary>
/// The model item container for ms runs.
/// </summary>
[<Sealed>]
type RunList [<JsonConstructor>] () =

    inherit ObservableModelItemCollection<Run>()


/// <summary>
/// The project item container for ms runs.
/// </summary>
[<Sealed>]
type ProjectRunList [<JsonConstructor>] () =

    inherit ObservableModelItemCollection<ProjectRun>()
