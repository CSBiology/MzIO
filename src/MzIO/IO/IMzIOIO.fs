namespace MzIO.IO


open System
open MzIO.Model


type IMzIOIO =

    inherit IDisposable

    abstract member CreateDefaultModel  : unit -> MzIOModel

    abstract member Model               : MzIOModel

    abstract member SaveModel           : unit -> unit

    abstract member BeginTransaction    : unit -> ITransactionScope
