namespace MzIO.Binary


open System
open System.IO
open System.IO.Compression
open NumpressHelper
open MzIO.Binary


type BinaryDataEncoder(?initialBufferSize: int) =

    let bufferSize = defaultArg initialBufferSize 1048576
    
    let mutable memoryStream' = new MemoryStream(bufferSize)

    interface IDisposable with
        member this.Dispose() =
            //printfn "Disposed"
            ()

    //member this.InitialBufferSize = 1048576

    member this.memoryStream
        with get() = memoryStream'
        and private set(value) = memoryStream' <- value

    ////////////////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
        ///////////////////////////////////////////////////////DIFFERENCE\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
    ////////////////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
    //Write float32 instead of float64 for float32.
    static member WriteValue(writer:BinaryWriter, binaryDataType:BinaryDataType, value:double) =
        match binaryDataType with
        | BinaryDataType.Int32      ->  writer.Write(int32 value)
        | BinaryDataType.Int64      ->  writer.Write(int64 value)
        | BinaryDataType.Float32    ->  writer.Write(single value)
        | BinaryDataType.Float64    ->  writer.Write(double value)
        | _     -> failwith (sprintf "%s%s" "BinaryDataType not supported: " (binaryDataType.ToString()))

    static member private NoCompression(memoryStream:Stream, binaryDataType:BinaryDataType, values:double[]) =

        let writer = new BinaryWriter(memoryStream, System.Text.Encoding.UTF8, true)

        for v in values do
            BinaryDataEncoder.WriteValue(writer, binaryDataType, v)

    static member private NoCompression(memoryStream:Stream, peakArray:Peak1DArray) =
        
        let writer = new BinaryWriter(memoryStream, System.Text.Encoding.UTF8, true)

        let (len:int32) = peakArray.Peaks.Length

        writer.Write(len)

        for pk in peakArray.Peaks do
            BinaryDataEncoder.WriteValue(writer, peakArray.IntensityDataType, pk.Intensity)
            BinaryDataEncoder.WriteValue(writer, peakArray.MzDataType, pk.Mz)


    static member private NoCompression(memoryStream:Stream, peakArray:Peak2DArray) =
        
        let writer = new BinaryWriter(memoryStream, System.Text.Encoding.UTF8, true)

        let len = peakArray.Peaks.Length

        writer.Write(len)

        for pk in peakArray.Peaks do
            BinaryDataEncoder.WriteValue(writer, peakArray.IntensityDataType, pk.Intensity)
            BinaryDataEncoder.WriteValue(writer, peakArray.MzDataType, pk.Mz)
            BinaryDataEncoder.WriteValue(writer, peakArray.RtDataType, pk.Rt)

    static member private DeflateStreamCompress (data: byte[]) =
        use mStream = new MemoryStream(data)
        (
         use outStream = new MemoryStream()
         use compress = new System.IO.Compression.DeflateStream (outStream, CompressionMode.Compress, true)      
         mStream.CopyTo(compress)
         compress.Close() 
         let byteArray = outStream.ToArray()
         byteArray
        )

    static member private DeflateStreamCompress (mStream: MemoryStream) =
        use outStream = new MemoryStream()
        (
             use compress = new System.IO.Compression.DeflateStream (outStream, CompressionMode.Compress, true)      
             mStream.CopyTo(compress)
             compress.Close() 
             let byteArray = outStream.ToArray()
             byteArray
        )

    static member private NumpressPicCompression(memoryStream:Stream, values:double[]) =
        let encData = NumpressEncodingHelpers.encodePIC values
        //let byteArray = [|[|encData.NumberEncodedBytes; encData.OriginalDataLength|];encData.Bytes|] |> Array.concat
        //new MemoryStream (byteArray)
        let writer = new BinaryWriter(memoryStream, System.Text.Encoding.UTF8, true)
        writer.Write(encData.NumberEncodedBytes)
        writer.Write(encData.OriginalDataLength)
        writer.Write(encData.Bytes)

    static member private NumpressLinCompression(memoryStream:Stream, values:double[]) =
        let encData = NumpressEncodingHelpers.encodeLin values
        let writer = new BinaryWriter(memoryStream, System.Text.Encoding.UTF8, true)
        writer.Write(encData.NumberEncodedBytes)
        writer.Write(encData.OriginalDataLength)
        writer.Write(encData.Bytes)

    static member private NumpressPicAndDeflateCompression(memoryStream:Stream, values:double[]) =
        let encData = NumpressEncodingHelpers.encodePIC values
        //let byteArray = [|[|encData.NumberEncodedBytes; encData.OriginalDataLength|];encData.Bytes|] |> Array.concat
        //new MemoryStream (byteArray)
        let deflateEncData = BinaryDataEncoder.DeflateStreamCompress encData.Bytes
        let writer = new BinaryWriter(memoryStream, System.Text.Encoding.UTF8, true)
        writer.Write(encData.NumberEncodedBytes)
        writer.Write(encData.OriginalDataLength)
        writer.Write(deflateEncData.Length)
        writer.Write(BinaryDataEncoder.DeflateStreamCompress encData.Bytes)

    static member private NumpressLinAndDeflateCompression(memoryStream:Stream, values:double[]) =
        let encData = NumpressEncodingHelpers.encodeLin values
        let deflateEncData = BinaryDataEncoder.DeflateStreamCompress encData.Bytes
        let writer = new BinaryWriter(memoryStream, System.Text.Encoding.UTF8, true)
        writer.Write(encData.NumberEncodedBytes)
        writer.Write(encData.OriginalDataLength)
        writer.Write(deflateEncData.Length)
        writer.Write(deflateEncData)

    static member private Numpress(memoryStream:Stream, peakArray:Peak1DArray) =
        
        let intensities = peakArray.Peaks |> Seq.map (fun peak -> peak.Intensity) |> Array.ofSeq

        let mzs         = peakArray.Peaks |> Seq.map (fun peak -> peak.Mz) |> Array.ofSeq
        
        BinaryDataEncoder.NumpressPicCompression(memoryStream, intensities)

        BinaryDataEncoder.NumpressLinCompression(memoryStream, mzs)

    static member private Numpress(memoryStream:Stream, peakArray:Peak2DArray) =
        
        let intensities = peakArray.Peaks |> Seq.map (fun peak -> peak.Intensity) |> Array.ofSeq

        let mzs         = peakArray.Peaks |> Seq.map (fun peak -> peak.Mz) |> Array.ofSeq

        let rts         = peakArray.Peaks |> Seq.map (fun peak -> peak.Rt) |> Array.ofSeq
        
        BinaryDataEncoder.NumpressPicCompression(memoryStream, intensities)

        BinaryDataEncoder.NumpressLinCompression(memoryStream, mzs)

        BinaryDataEncoder.NumpressLinCompression(memoryStream, rts)

    static member private NumpressDeflate(memoryStream:Stream, peakArray:Peak1DArray) =
        
        let intensities = peakArray.Peaks |> Seq.map (fun peak -> peak.Intensity) |> Array.ofSeq

        let mzs         = peakArray.Peaks |> Seq.map (fun peak -> peak.Mz) |> Array.ofSeq
        
        BinaryDataEncoder.NumpressPicAndDeflateCompression(memoryStream, intensities)

        BinaryDataEncoder.NumpressLinAndDeflateCompression(memoryStream, mzs)

    static member private NumpressDeflate(memoryStream:Stream, peakArray:Peak2DArray) =
        
        let intensities = peakArray.Peaks |> Seq.map (fun peak -> peak.Intensity) |> Array.ofSeq

        let mzs         = peakArray.Peaks |> Seq.map (fun peak -> peak.Mz) |> Array.ofSeq

        let rts         = peakArray.Peaks |> Seq.map (fun peak -> peak.Rt) |> Array.ofSeq
        
        BinaryDataEncoder.NumpressPicAndDeflateCompression(memoryStream, intensities)

        BinaryDataEncoder.NumpressLinAndDeflateCompression(memoryStream, mzs)

        BinaryDataEncoder.NumpressLinAndDeflateCompression(memoryStream, rts)

    static member private ZLib(memoryStream:Stream, binaryDataType:BinaryDataType, values:double[]) =

        let deflateStream = new DeflateStream(memoryStream, CompressionMode.Compress, true)

        BinaryDataEncoder.NoCompression(deflateStream, binaryDataType, values)

    static member private ZLib(memoryStream:Stream, peakArray:Peak1DArray) =

        let deflateStream = new DeflateStream(memoryStream, CompressionMode.Compress, true)

        BinaryDataEncoder.NoCompression(deflateStream, peakArray)

    static member private ZLib(memoryStream:Stream, peakArray:Peak2DArray) =

        let deflateStream = new DeflateStream(memoryStream, CompressionMode.Compress, true)

        BinaryDataEncoder.NoCompression(deflateStream, peakArray)

    static member private FloatToByteArray (floatArray: float[]) =
        let byteArray = Array.init (floatArray.Length*8) (fun x -> byte(0))
        Buffer.BlockCopy (floatArray, 0, byteArray, 0, byteArray.Length)
        byteArray

    static member private ZLib2(memoryStream:Stream, peakArray:Peak1DArray) =

        let intensities = BinaryDataEncoder.FloatToByteArray (peakArray.Peaks |> Seq.map (fun peak -> peak.Intensity) |> Array.ofSeq)
        let mz          = BinaryDataEncoder.FloatToByteArray (peakArray.Peaks |> Seq.map (fun peak -> peak.Mz) |> Array.ofSeq)
        let intDeflate  = BinaryDataEncoder.DeflateStreamCompress intensities
        let mzDeflate   = BinaryDataEncoder.DeflateStreamCompress mz
        let writer      = new BinaryWriter(memoryStream, System.Text.Encoding.UTF8, true)
        writer.Write(intDeflate.Length)
        writer.Write(intDeflate)
        writer.Write(mzDeflate.Length)
        writer.Write(mzDeflate)

    member this.Encode(peakArray:Peak1DArray) =
       
        this.memoryStream.Seek(int64 0, SeekOrigin.Begin) |> ignore

        match peakArray.CompressionType with
        | BinaryDataCompressionType.NoCompression   -> BinaryDataEncoder.NoCompression(this.memoryStream, peakArray)
        | BinaryDataCompressionType.ZLib            -> BinaryDataEncoder.ZLib2(this.memoryStream, peakArray)
        | BinaryDataCompressionType.NumPress        -> BinaryDataEncoder.Numpress(this.memoryStream, peakArray)
        | BinaryDataCompressionType.NumPressZLib    -> BinaryDataEncoder.NumpressDeflate(this.memoryStream, peakArray)
        | _ -> failwith (sprintf "%s%s" "Compression type not supported: " (peakArray.CompressionType.ToString()))

        //this.memoryStream.Seek(int64 0, SeekOrigin.Begin) |> ignore
        this.memoryStream.ToArray()

    member this.Encode(peakArray:Peak2DArray) =   

        this.memoryStream.Seek(int64 0, SeekOrigin.Begin) |> ignore

        match peakArray.CompressionType with
        | BinaryDataCompressionType.NoCompression   -> BinaryDataEncoder.NoCompression(this.memoryStream, peakArray)
        | BinaryDataCompressionType.ZLib            -> BinaryDataEncoder.ZLib(this.memoryStream, peakArray)
        | BinaryDataCompressionType.NumPress        -> BinaryDataEncoder.Numpress(this.memoryStream, peakArray)
        | BinaryDataCompressionType.NumPressZLib    -> BinaryDataEncoder.NumpressDeflate(this.memoryStream, peakArray)
        | _ -> failwith (sprintf "%s%s" "Compression type not supported: " (peakArray.CompressionType.ToString()))

        //this.memoryStream.Seek(int64 0, SeekOrigin.Begin) |> ignore
        this.memoryStream.ToArray()

    member this.EncodeBase64(values:double[], compressionType:BinaryDataCompressionType, binaryDataType:BinaryDataType)=

        this.memoryStream.Seek(int64 0, SeekOrigin.Begin) |> ignore

        match compressionType with
        | BinaryDataCompressionType.NoCompression   -> BinaryDataEncoder.NoCompression(this.memoryStream, binaryDataType, values)
        | BinaryDataCompressionType.ZLib            -> BinaryDataEncoder.ZLib(this.memoryStream, binaryDataType, values)
        | _ -> failwith (sprintf "%s%s" "Compression type not supported: " (compressionType.ToString()))

        //this.memoryStream.Seek(int64 0, SeekOrigin.Begin) |> ignore
        Convert.ToBase64String(this.memoryStream.ToArray())      