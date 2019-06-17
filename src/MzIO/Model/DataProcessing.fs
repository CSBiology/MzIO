namespace MzIO.Model


open System.Collections.Generic
open MzIO.Model
open Newtonsoft.Json


/// <summary>
/// Expansible description of a data processing step and use of processing software.
/// </summary>
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type DataProcessingStep [<JsonConstructor>] ([<JsonProperty("Name")>] name: string, software: Software) =

    inherit NamedItem(name)

    let mutable software' = software

    new() = DataProcessingStep("name", new Software())

    //member this.DataProcessingStep([<JsonProperty("Name")>] name:string) = base.Name

    [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
    member this.Software
        with get() = software' 
            and set(value) = 
                if value = software' then ()
                else
                    this.NotifyPropertyChaning("Software")
                    software' <- value
                    this.NotifyPropertyChanged("Software")

/// <summary>
/// The container for data processing steps.
/// </summary>
[<Sealed>]
type DataProcessingStepList [<JsonConstructor>] () =

    inherit ObservableNamedItemCollection<DataProcessingStep>()

/// <summary>
/// Expansible description of a data processing.
/// Captures the processing steps applied and the use of data processing software.
/// </summary>    
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn, IsReference = true)>]
type DataProcessing ([<JsonProperty("ID")>]id: string, processingSteps:DataProcessingStepList) =

    inherit ModelItem(id)

    let processingSteps = processingSteps
    
    [<JsonConstructor>]
    new(id) = new DataProcessing(id, new DataProcessingStepList())
    new() = new DataProcessing("id")

    //member this.DataProcessing(id:string) = base.ID

    [<JsonProperty>]
    member this.ProcessingSteps = processingSteps

/// <summary>
/// The model item container for data processings.
/// </summary>

[<Sealed>]
type DataProcessingList [<JsonConstructor>] internal (dict:Dictionary<string, obj>) =
    
    inherit ObservableModelItemCollection<DataProcessing>(dict)

    new() = new DataProcessingList(new Dictionary<string, obj>())