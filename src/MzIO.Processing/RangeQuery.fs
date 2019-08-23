
namespace MzIO.Processing


/// Class used to save a value and its offset.
type RangeQuery(lockMz:float, offsetLow:float, offsetHeigh:float) =

    let lockValue   = lockMz

    let lowValue    = lockMz - offsetLow

    let highValue   = lockMz + offsetHeigh

    new(lockMz, offsetLowHigh) = RangeQuery(lockMz, offsetLowHigh, offsetLowHigh)
    new() = RangeQuery(0., 0., 0.)

    member this.LockValue
        with get()              = lockValue

    member this.LowValue
        with get()              = lowValue

    member this.HighValue
        with get()              = highValue