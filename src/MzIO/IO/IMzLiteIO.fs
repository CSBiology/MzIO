namespace MzLiteFSharp.IO


open System
open MzLiteFSharp.Model


type IMzLiteIO =

    inherit IDisposable

    abstract member CreateDefaultModel  : unit -> MzLiteModel

    abstract member Model               : MzLiteModel

    abstract member SaveModel           : unit -> unit

    abstract member BeginTransaction    : unit -> ITransactionScope
