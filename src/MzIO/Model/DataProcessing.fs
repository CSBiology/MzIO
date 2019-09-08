namespace MzIO.Model


open System.Collections.Generic
open MzIO.Model
open Newtonsoft.Json


/// Expansible description of a data processing step and use of processing software.
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type DataProcessingStep [<JsonConstructor>] ([<JsonProperty("Name")>] name: string, software: Software) =

    inherit NamedItem(name)

    let mutable software' = software

    new() = DataProcessingStep("name", new Software())

    [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
    member this.Software
        with get() = software' 
            and set(value) = 
                if value = software' then ()
                else
                    this.NotifyPropertyChaning("Software")
                    software' <- value
                    this.NotifyPropertyChanged("Software")

/// The named item container for all data processing steps in the current spectrum.
[<Sealed>]
type DataProcessingStepList [<JsonConstructor>] () =

    inherit ObservableNamedItemCollection<DataProcessingStep>()

/// Expansible description of a data processing.
/// Captures the processing steps applied and the use of data processing software.   
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn, IsReference = true)>]
type DataProcessing ([<JsonProperty("ID")>]id: string, processingSteps:DataProcessingStepList) =

    inherit ModelItem(id)

    let processingSteps = processingSteps
    
    [<JsonConstructor>]
    new(id) = new DataProcessing(id, new DataProcessingStepList())
    new() = new DataProcessing("id")

    [<JsonProperty>]
    member this.ProcessingSteps = processingSteps

/// The model item container for all data processings of this experiment.
[<Sealed>]
type DataProcessingList [<JsonConstructor>] () =
    
    inherit ObservableModelItemCollection<DataProcessing>()
