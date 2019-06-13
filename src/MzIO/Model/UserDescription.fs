namespace MzLiteFSharp.Model


open MzLiteFSharp.Model
open Newtonsoft.Json


[<Sealed>]
[<JsonObject(MemberSerialization = MemberSerialization.OptIn)>]
type UserDescription(name:string) =

    inherit NamedItem(name)

    member this.UserDescription = base.Name

    new() = UserDescription("name")

[<Sealed>]
type UserDescriptionCollection() =

    inherit ObservableNamedItemCollection<UserDescription>()
