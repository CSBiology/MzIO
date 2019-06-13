namespace MzLiteFSharp.Model


open MzLiteFSharp.Model
open MzLiteFSharp.Model.CvParam
open Newtonsoft.Json


/// <summary>
/// Expansible description of a sample treatment.
/// </summary>
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type SampleTreatment() =

    inherit DynamicObj()

[<Sealed>]
type SampleTreatmentList [<JsonConstructor>] () =

    inherit ObservableCollection<SampleTreatment>()

/// <summary>
/// Expansible description of a sample preparation.
/// </summary>
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type SamplePreparation() =
    
    inherit DynamicObj()

[<Sealed>]
type SamplePreparationList [<JsonConstructor>] () =

    inherit ObservableCollection<SamplePreparation>()

/// <summary>
/// Expansible description of a sample.
/// </summary>
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn, IsReference = true)>]
type Sample [<JsonConstructor>] (id:string, name:string) =

    inherit NamedModelItem(id, name)

    let mutable treatments      = new SampleTreatmentList()

    let mutable preperations    = new SamplePreparationList()

    new() = Sample("id", "name")

    [<JsonProperty>]
    member this.Preparations    = preperations

    [<JsonProperty>]
    member this.Treatments      = treatments

/// <summary>
/// The model item container for samples.
/// </summary>
[<Sealed>]
type SampleList [<JsonConstructor>] () =

    inherit ObservableModelItemCollection<Sample>()
