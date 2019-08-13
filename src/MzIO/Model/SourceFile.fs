namespace MzIO.Model


open System
open System.Collections.Generic
open MzIO.Model
open Newtonsoft.Json


/// Expansible description of a source file.
[<JsonObject(MemberSerialization.OptIn, IsReference = true)>]
type SourceFile [<JsonConstructor>] ( [<JsonProperty("ID")>] id:string, [<JsonProperty("Name")>] name:string, [<JsonProperty("Location")>] location:string) =

    inherit NamedModelItem(id, name)

    let mutable location' = 
        if String.IsNullOrWhiteSpace(location) then 
            raise (ArgumentNullException("location"))
        else
            location

    new(id, name) = SourceFile(id, name, "location")
    new(id) = SourceFile(id, "name", "location")
    new() = SourceFile("id", "name", "location")

    [<JsonProperty(Required = Required.Always)>]
    member this.Location
        with get() = location' 
            and set(value) = 
                if value = location' then ()
                else
                    this.NotifyPropertyChaning("Location")
                    location' <- value
                    this.NotifyPropertyChanged("Location")

/// The model item container for all source files of this experiment.
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type SourceFileList [<JsonConstructor>] internal (dict:Dictionary<string, obj>) =

    inherit ObservableModelItemCollection<SourceFile>(dict)

    new() = new SourceFileList(new Dictionary<string, obj>())
