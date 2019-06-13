namespace MzIO.Model


open MzIO.Model
open Newtonsoft.Json


[<Sealed>]
type MzLiteProject [<JsonConstructor>] ([<JsonProperty("Name")>] name:string, sourceFiles:SourceFileList, samples:SampleList, runs:ProjectRunList) =

    inherit NamedItem(name)

    let mutable sourceFiles'    = sourceFiles

    let mutable samples'        = samples

    let mutable runs'           = runs

    //member this.MzLiteProject   = base.Name

    [<JsonProperty>]
    member this.SourceFiles     = sourceFiles'

    [<JsonProperty>]
    member this.Samples         = samples'

    [<JsonProperty>]
    member this.Runs            = runs'
