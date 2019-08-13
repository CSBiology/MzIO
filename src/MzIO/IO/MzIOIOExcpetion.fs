namespace MzIO.IO


open System


/// Contains MzIO specific exceptions.
type MzIOIOException() =

    inherit Exception()

    new(message:string) = new MzIOIOException(message)

    new(message:string, args:Object[]) = new MzIOIOException(String.Format(message, args))

    new(message:string, innerException:Exception) = new MzIOIOException(message, innerException)
