namespace MzIO.IO

open System

type ITransactionScope =

    inherit IDisposable

    abstract member Rollback : unit -> unit

    abstract member Commit   : unit -> unit