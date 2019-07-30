namespace MzIO.Model


open System.Collections.Generic
open MzIO.Model
open Newtonsoft.Json

/// <summary>
/// Expansible description of a processing software.
/// </summary>
[<JsonObject(MemberSerialization.OptIn, IsReference = true)>]
type Software [<JsonConstructor>] (id:string) =
    
    inherit ModelItem(id)

    new() = Software("id")

///// <summary>
///// The model item container for processing software.
///// </summary>
[<Sealed>]
type SoftwareList [<JsonConstructor>] internal (dict:Dictionary<string, obj>) =

    inherit MzIO.Model.ObservableModelItemCollection<Software>(dict)

    new() = new SoftwareList(new Dictionary<string, obj>())

