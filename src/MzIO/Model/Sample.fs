namespace MzIO.Model


open System.Collections.Generic
open MzIO.Model
open MzIO.Model.CvParam
open Newtonsoft.Json


/// Expansible description of a sample treatment.
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type SampleTreatment() =

    inherit DynamicObj()

/// The model item container for all sampletreatments of this experiment.
[<Sealed>]
type SampleTreatmentList [<JsonConstructor>] () =

    inherit ObservableCollection<SampleTreatment>()

/// Expansible description of a sample preparation.
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type SamplePreparation() =
    
    inherit DynamicObj()

/// The model item container for all sample preparations of this experiment.
[<Sealed>]
type SamplePreparationList [<JsonConstructor>] () =

    inherit ObservableCollection<SamplePreparation>()

/// Expansible description of a sample.
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn, IsReference = true)>]
type Sample (id:string, name:string, treatments:SampleTreatmentList, preperations:SamplePreparationList) =

    inherit NamedModelItem(id, name)

    [<JsonConstructor>]
    new(id, name) = new Sample(id, name, new SampleTreatmentList(), new SamplePreparationList())
    new() = Sample("id", "name", new SampleTreatmentList(), new SamplePreparationList())

    [<JsonProperty>]
    member this.Preparations    = preperations

    [<JsonProperty>]
    member this.Treatments      = treatments

/// The model item container for samples of this experiment.
[<Sealed>]
type SampleList [<JsonConstructor>] internal (dict:Dictionary<string, obj>) =

    inherit ObservableModelItemCollection<Sample>(dict)

    new() = new SampleList(new Dictionary<string, obj>())
