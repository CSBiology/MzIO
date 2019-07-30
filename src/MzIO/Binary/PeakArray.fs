namespace MzIO.Binary

open System
open MzIO.Commons.Arrays
open Newtonsoft.Json
open MzIO.Model.CvParam

[<AbstractClass>]
type Peak(intensity:float) =

    //let mutable intensity' = intensity

    new () = Peak(0.)

    member this.Intensity
        with get() = intensity
        //and private set(value) = intensity' <- value

type Peak1D(intensity:float, mz:float) =

    inherit Peak(intensity)

    //let mutable mz' = mz

    new () = Peak1D(0., 0.)

    member this.Mz
        with get() = mz
        //and private set(value) = mz' <- value

    override this.ToString() =
        String.Format("intensity={0}, mz={1}", this.Intensity, this.Mz)

type Peak2D(intensity:float, mz:float, rt:float) =

    inherit Peak1D(intensity, mz)

    //let mutable rt' = rt

    new () = Peak2D(0., 0., 0.)

    member this.Rt
        with get() = rt
        //and private set(value) = rt <- value

    override this.ToString() =
        String.Format("intensity={0}, mz={1}, rt={2}", this.Intensity, this.Mz, this.Rt)

type BinaryDataType =
    |Float32    = 0     //MS:1000521
    |Float64    = 1     //MS:1000523
    |Int32      = 2     //MS:1000519
    |Int64      = 3     //MS:1000522

type BinaryDataCompressionType =
    |NoCompression   = 0 //MS:1000576
    |ZLib            = 1 //MS:1000574
    |NumPress        = 2
    |NumPressZLib    = 3
    |NumPressPic     = 4
    |NumPressLin     = 5

        ////////////////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
///////////////////////////////////////////////////////DIFFERENCE\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
        ////////////////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\

//Inherit dynamicObject instead of ParamContainer.
//TPeak must only be capable to upcast to Peak instead of being Peak.
//Trouble representing peaks, use member val becasue using abstract type leads to problem with classifying following classes as abstract
//because peaks has to beimplemented.

[<AbstractClass>]
type PeakArray<'TPeak when 'TPeak :> Peak>() =

    inherit DynamicObj()

    //let peaks = ArrayWrapper<'TPeak>() :> IMzIOArray<'TPeak>

    //let mutable intensityDataType' = intensityDataType

    //let mutable compressionType'   = compressionType

    //let mutable peaks'             = peaks

    //new () = PeakArray(BinaryDataType.Float32, BinaryDataCompressionType.NoCompression)

    [<JsonProperty(Required = Required.Always)>]
    abstract member IntensityDataType : BinaryDataType with get, set
        //with get() = intensityDataType'
        //and private set(value) = intensityDataType' <- value

    [<JsonProperty(Required = Required.Always)>]
    abstract member CompressionType : BinaryDataCompressionType with get, set
        //with get() = compressionType'
        //and private set(value) = compressionType' <- value

    [<JsonIgnore>]
    abstract member Peaks : IMzIOArray<'TPeak> with get, set
        //with get() = peaks'
        //and private set(value) = peaks' <- value

[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type Peak1DArray(compressionDataType:BinaryDataCompressionType, intensityDataType:BinaryDataType, mzDataType:BinaryDataType, peaks:IMzIOArray<Peak1D>) =

    inherit PeakArray<Peak1D>()

    let mutable peaks               = peaks

    let mutable intensityDataType   = intensityDataType

    let mutable mzDataType          = mzDataType

    let mutable compressionDataType = compressionDataType

    new(compressionDataType, intensityDataType, mzDataType) = Peak1DArray(compressionDataType, intensityDataType, mzDataType, ArrayWrapper<Peak1D>([||]))

    new () = Peak1DArray(BinaryDataCompressionType.NoCompression, BinaryDataType.Float64, BinaryDataType.Float64, ArrayWrapper<Peak1D>([||]))

    [<JsonProperty(Required = Required.Always)>]
    override this.CompressionType
        with get() = compressionDataType
        and set(value) = compressionDataType <- value

    [<JsonProperty(Required = Required.Always)>]
    override this.IntensityDataType
        with get() = intensityDataType
        and set(value) = intensityDataType <- value

    [<JsonProperty(Required = Required.Always)>]
    member this.MzDataType
        with get() = mzDataType
        and set(value) = mzDataType <- value

    [<JsonIgnore>]
    override this.Peaks
        with get() = peaks
        and set(value) = peaks <- value

[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type Peak2DArray(compressionDataType:BinaryDataCompressionType, intensityDataType:BinaryDataType, mzDataType:BinaryDataType, rtDataType:BinaryDataType, peaks:IMzIOArray<Peak2D>) =

    inherit PeakArray<Peak2D>()

    let mutable peaks               = peaks

    let mutable compressionDataType = compressionDataType

    let mutable intensityDataType   = intensityDataType

    let mutable mzDataType          = mzDataType

    let mutable rtDataType          = rtDataType

    new(compressionDataType, intensityDataType, mzDataType, rtDataType) = Peak2DArray(compressionDataType, intensityDataType, mzDataType, rtDataType, ArrayWrapper<Peak2D>([||]))

    new () = Peak2DArray(BinaryDataCompressionType.NoCompression, BinaryDataType.Float64, BinaryDataType.Float64, BinaryDataType.Float64, ArrayWrapper<Peak2D>([||]))

    [<JsonProperty(Required = Required.Always)>]
    override this.CompressionType
        with get() = compressionDataType
        and set(value) = compressionDataType <- value

    [<JsonProperty(Required = Required.Always)>]
    override this.IntensityDataType
        with get() = intensityDataType
        and set(value) = intensityDataType <- value

    [<JsonProperty(Required = Required.Always)>]
    member this.MzDataType
        with get() = mzDataType
        and set(value) = mzDataType <- value

    [<JsonProperty(Required = Required.Always)>]
    member this.RtDataType
        with get() = rtDataType
        and set(value) = rtDataType <- value

    [<JsonIgnore>]
    override this.Peaks
        with get() = peaks
        and set(value) = peaks <- value