namespace MzLiteFSharp.IO


open System


type MzLiteIOException() =

    inherit Exception()

    new(message:string) = new MzLiteIOException(message)

    new(message:string, args:Object[]) = new MzLiteIOException(String.Format(message, args))

    new(message:string, innerException:Exception) = new MzLiteIOException(message, innerException)