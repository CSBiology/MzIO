namespace MzIO.Model


open System.Collections.Generic
open MzIO.Model
open MzIO.Model.CvParam
open Newtonsoft.Json


/// Expansible description of a mass spectrometer component.
[<AbstractClassAttribute>]
[<JsonObject(MemberSerialization.OptIn)>]
type Component [<JsonConstructor>] () =

    inherit DynamicObj()

/// A source component.
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type SourceComponent [<JsonConstructor>] () =

    inherit Component()

/// A mass analyzer (or mass filter) component.
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type AnalyzerComponent [<JsonConstructor>] () =

    inherit Component()

/// A detector component.
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type DetectorComponent [<JsonConstructor>] () =

    inherit Component()

/// The container for all mass spectrometer components of this experiment.
[<Sealed>]
[<JsonObject(ItemTypeNameHandling = TypeNameHandling.All)>]
type ComponentList [<JsonConstructor>] () =

    inherit MzIO.Model.ObservableCollection<Component>()

/// Expansible description of the hardware configuration of a mass spectrometer.
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn, IsReference = true)>]
type Instrument([<JsonProperty("ID")>] id:string, software, components) =

    inherit ModelItem(id)

    let mutable softWare'   = software

    let mutable components' = components

    [<JsonConstructor>]
    new(id) = new Instrument(id, new Software(), new ComponentList())
    new() = new Instrument("id", new Software(), new ComponentList())

    [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
    member this.Software
        with get() = softWare' 
            and set(value) = 
                if value = softWare' then ()
                else
                    this.NotifyPropertyChaning("Software")
                    softWare' <- value
                    this.NotifyPropertyChanged("Software")
    
    [<JsonProperty>]
    member this.Components = components'

/// The model item container for all instrument configurations of this experiment.
[<Sealed>]
type InstrumentList [<JsonConstructor>] () =

    inherit MzIO.Model.ObservableModelItemCollection<Instrument>()
