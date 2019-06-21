namespace MzIO.Model


open MzIO.Model
open Newtonsoft.Json


[<Sealed>]
type MzIOProject [<JsonConstructor>] ([<JsonProperty("Name")>] name:string, sourceFiles:SourceFileList, samples:SampleList, runs:ProjectRunList) =

    inherit NamedItem(name)

    let mutable sourceFiles'    = sourceFiles

    let mutable samples'        = samples

    let mutable runs'           = runs

    //member this.MzIOProject   = base.Name

    [<JsonProperty>]
    member this.SourceFiles     = sourceFiles'

    [<JsonProperty>]
    member this.Samples         = samples'

    [<JsonProperty>]
    member this.Runs            = runs'
