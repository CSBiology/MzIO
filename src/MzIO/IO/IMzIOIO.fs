namespace MzIO.IO


open System
open MzIO.Model


///Interface for the readers and writers to share a set of basic functions that are associated with the MzIOModel.
type IMzIOIO =

    inherit IDisposable

    abstract member CreateDefaultModel  : unit -> MzIOModel

    abstract member Model               : MzIOModel

    abstract member SaveModel           : unit -> unit

    abstract member BeginTransaction    : unit -> ITransactionScope
