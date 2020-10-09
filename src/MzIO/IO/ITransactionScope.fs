namespace MzIO.IO



open System


/// Basic interface for the different readers to share basic functions.
type ITransactionScope =

    inherit IDisposable

    abstract member Rollback : unit -> unit

    abstract member Commit   : unit -> unit
