namespace MzIO.Model


open System.Collections.Generic
open MzIO.Model
open MzIO.Model.CvParam
open Newtonsoft.Json


/// <summary>
/// Expansible description of a sample treatment.
/// </summary>
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type SampleTreatment() =

    inherit DynamicObj()

[<Sealed>]
type SampleTreatmentList [<JsonConstructor>] internal (dict:Dictionary<string, obj>) =

    inherit ObservableCollection<SampleTreatment>(dict)

    new() = new SampleTreatmentList(new Dictionary<string, obj>())

/// <summary>
/// Expansible description of a sample preparation.
/// </summary>
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type SamplePreparation() =
    
    inherit DynamicObj()

[<Sealed>]
type SamplePreparationList [<JsonConstructor>] internal (dict:Dictionary<string, obj>) =

    inherit ObservableCollection<SamplePreparation>(dict)

    new() = new SamplePreparationList(new Dictionary<string, obj>())

/// <summary>
/// Expansible description of a sample.
/// </summary>
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn, IsReference = true)>]
type Sample (id:string, name:string, treatments:SampleTreatmentList, preperations:SamplePreparationList) =

    inherit NamedModelItem(id, name)

    //let mutable treatments      = new SampleTreatmentList()

    //let mutable preperations    = new SamplePreparationList()

    [<JsonConstructor>]
    new(id, name) = new Sample(id, name, new SampleTreatmentList(), new SamplePreparationList())
    new() = Sample("id", "name", new SampleTreatmentList(), new SamplePreparationList())

    [<JsonProperty>]
    member this.Preparations    = preperations

    [<JsonProperty>]
    member this.Treatments      = treatments

/// <summary>
/// The model item container for samples.
/// </summary>
[<Sealed>]
type SampleList [<JsonConstructor>] internal (dict:Dictionary<string, obj>) =

    inherit ObservableModelItemCollection<Sample>(dict)

    new() = new SampleList(new Dictionary<string, obj>())
