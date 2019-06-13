namespace MzIO.IO


open System
open MzIO.Model


type IMzLiteIO =

    inherit IDisposable

    abstract member CreateDefaultModel  : unit -> MzLiteModel

    abstract member Model               : MzLiteModel

    abstract member SaveModel           : unit -> unit

    abstract member BeginTransaction    : unit -> ITransactionScope
