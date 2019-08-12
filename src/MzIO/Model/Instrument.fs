namespace MzIO.Model


open System.Collections.Generic
open MzIO.Model
open MzIO.Model.CvParam
open Newtonsoft.Json


/// <summary>
/// Expansible description of a mass spectrometer component.
/// </summary>
[<AbstractClassAttribute>]
[<JsonObject(MemberSerialization.OptIn)>]
type Component [<JsonConstructor>] () =

    inherit DynamicObj()

/// <summary>
/// A source component.
/// </summary>
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type SourceComponent [<JsonConstructor>] () =

    inherit Component()

/// <summary>
/// A mass analyzer (or mass filter) component.
/// </summary>
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type AnalyzerComponent [<JsonConstructor>] () =

    inherit Component()

/// <summary>
/// A detector component.
/// </summary>
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type DetectorComponent [<JsonConstructor>] () =

    inherit Component()

/// <summary>
/// The container for mass spectrometer components.
/// </summary>
[<Sealed>]
[<JsonObject(ItemTypeNameHandling = TypeNameHandling.All)>]
type ComponentList [<JsonConstructor>] () =

    inherit MzIO.Model.ObservableCollection<Component>()


/// <summary>
/// Expansible description of the hardware configuration of a mass spectrometer.
/// </summary>
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

/// <summary>
/// The model item container for instrument configurations.
/// </summary>
[<Sealed>]
type InstrumentList [<JsonConstructor>] internal (dict:Dictionary<string, obj>) =

    inherit MzIO.Model.ObservableModelItemCollection<Instrument>(dict)

    new() = new InstrumentList(new Dictionary<string, obj>())
