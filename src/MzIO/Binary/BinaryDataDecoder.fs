namespace MzIO.Binary


open System
open System.IO
open System.IO.Compression
open NumpressHelper
open MzIO.Commons.Arrays
open MzIO.Binary


///Contains methods to convert bytes into peak arrays.
type BinaryDataDecoder() =

    /// Converts bytes into the given binary data type.
    static member private ReadValue(reader:BinaryReader, binaryDataType:BinaryDataType) =
        match binaryDataType with
        | BinaryDataType.Int32      ->  float (reader.ReadInt32())
        | BinaryDataType.Int64      ->  float (reader.ReadInt64())
        | BinaryDataType.Float32    ->  float (reader.ReadSingle())
        | BinaryDataType.Float64    ->  float (reader.ReadDouble())
        | _     -> failwith (sprintf "%s%s" "BinaryDataType not supported: " (binaryDataType.ToString()))

    /// Converts bytes directly into Peak1DArray because they musn't be decompromissed.
    static member private NoCompression(stream:Stream, peakArray:Peak1DArray) =
        let reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true)
        let len = reader.ReadInt32()
        let peaks = Array.init len (fun i -> Peak1D(BinaryDataDecoder.ReadValue(reader, peakArray.IntensityDataType), BinaryDataDecoder.ReadValue(reader, peakArray.MzDataType)))
        peakArray.Peaks <- MzIOArray.ToMzIOArray<Peak1D>(peaks)
        peakArray

    /// Converts bytes directly into Peak2DArray because they musn't be decompromissed.
    static member private NoCompression(stream:Stream, peakArray:Peak2DArray) =
        let reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true)
        let len = reader.ReadInt32()
        let peaks = Array.init len (fun i -> Peak2D(BinaryDataDecoder.ReadValue(reader, peakArray.IntensityDataType), BinaryDataDecoder.ReadValue(reader, peakArray.MzDataType), BinaryDataDecoder.ReadValue(reader, peakArray.RtDataType)))
        peakArray.Peaks <- MzIOArray.ToMzIOArray<Peak2D>(peaks)
        peakArray

    /// Decompress bytes based on zlib decompression method and convert to Peak1DArray.
    static member private ZLib(stream:Stream, peakArray:Peak1DArray) =

        let decompressStream = new DeflateStream(stream, CompressionMode.Decompress, true)

        BinaryDataDecoder.NoCompression(decompressStream, peakArray)

    /// Decompress bytes based on zlib decompression method and convert to Peak2DArray.
    static member private ZLib(stream:Stream, peakArray:Peak2DArray) =

        let decompressStream = new DeflateStream(stream, CompressionMode.Decompress, true)

        BinaryDataDecoder.NoCompression(decompressStream, peakArray)

    /// Convert byte array to float array.
    static member private ByteToFloatArray (byteArray: byte[]) =
        let floatArray = Array.init (byteArray.Length/8) (fun x -> 0.)
        Buffer.BlockCopy  (byteArray, 0, floatArray, 0, byteArray.Length)
        floatArray

    /// Decompress stream and convert to new byte array.
    static member private DeflateStreamDecompress (data: byte[]) =
        let outStream = new MemoryStream()
        let decompress = new System.IO.Compression.DeflateStream (new MemoryStream(data), CompressionMode.Decompress)
        decompress.CopyTo(outStream)
        let byteArray = outStream.ToArray()
        byteArray

    /// Decompress bytes based on zlib decompression method and convert to Peak1DArray.
    static member private ZLib2(stream:Stream, peakArray:Peak1DArray) =
        let reader                  = new BinaryReader(stream, System.Text.Encoding.UTF8, true)
        let intLength               = reader.ReadInt32()
        let intensities             = reader.ReadBytes(intLength)
        let mzLength                = reader.ReadInt32()
        let mz                      = reader.ReadBytes(mzLength)
        let intensityArray          = BinaryDataDecoder.ByteToFloatArray (BinaryDataDecoder.DeflateStreamDecompress intensities)
        let mzArray                 = BinaryDataDecoder.ByteToFloatArray (BinaryDataDecoder.DeflateStreamDecompress mz)
        Array.map2 (fun int mz -> new Peak1D(int, mz)) intensityArray mzArray
        |> fun peak1Ds -> peakArray.Peaks <- MzIOArray.ToMzIOArray<Peak1D>(peak1Ds)
        peakArray

    /// Decompress bytes based on numpress decompression method and convert to Peak1DArray.
    static member private Numpress1D(stream:Stream, peakArray: Peak1DArray) =
        let reader                  = new BinaryReader(stream, System.Text.Encoding.UTF8, true)
        //read information for decoding of intensities
        let numberEncodedBytesInt   = reader.ReadInt32()
        let originalDataLengthInt   = reader.ReadInt32()
        let byteArrayInt            = reader.ReadBytes(numberEncodedBytesInt + 5)
        //read information for decoding of m/z
        let numberEncodedBytesMz    = reader.ReadInt32()
        let originalDataLengthMz    = reader.ReadInt32()
        let byteArrayMz             = reader.ReadBytes(numberEncodedBytesMz + 5)
        let intensityArray          = NumpressDecodingHelpers.decodePIC (byteArrayInt, numberEncodedBytesInt, originalDataLengthInt)
        let mzArray                 = NumpressDecodingHelpers.decodeLin (byteArrayMz, numberEncodedBytesMz, originalDataLengthMz)

        Array.map2 (fun int mz -> new Peak1D(int, mz)) intensityArray mzArray
        |> fun peak1Ds -> peakArray.Peaks <- MzIOArray.ToMzIOArray<Peak1D>(peak1Ds)
        peakArray

    /// Decompress bytes based on numpress decompression method and convert to Peak2DArray.
    static member private Numpress2D(stream:Stream, peakArray: Peak2DArray) =
        let reader                  = new BinaryReader(stream, System.Text.Encoding.UTF8, true)
        //read information for decoding of intensities
        let numberEncodedBytesInt   = reader.ReadInt32()
        let originalDataLengthInt   = reader.ReadInt32()
        let byteArrayInt            = reader.ReadBytes(numberEncodedBytesInt + 5)
        //read information for decoding of m/z
        let numberEncodedBytesMz    = reader.ReadInt32()
        let originalDataLengthMz    = reader.ReadInt32()
        let byteArrayMz             = reader.ReadBytes(numberEncodedBytesMz + 5)
        //read information for decoding of retention time
        let numberEncodedBytesRt    = reader.ReadInt32()
        let originalDataLengthRt    = reader.ReadInt32()
        let byteArrayRt             = reader.ReadBytes(numberEncodedBytesRt + 5)
        let intensityArray          = NumpressDecodingHelpers.decodePIC (byteArrayInt, numberEncodedBytesInt, originalDataLengthInt)
        let mzArray                 = NumpressDecodingHelpers.decodeLin (byteArrayMz, numberEncodedBytesMz, originalDataLengthMz)
        let rtArray                 = NumpressDecodingHelpers.decodeLin (byteArrayRt, numberEncodedBytesRt, originalDataLengthRt)

        Array.map3 (fun int mz rt -> new Peak2D(int, mz, rt)) intensityArray mzArray rtArray
        |> fun peak2Ds -> peakArray.Peaks <- MzIOArray.ToMzIOArray<Peak2D>(peak2Ds)
        peakArray

    /// Decompress bytes based on numpress and zlib decompression method and convert to Peak1DArray.
    static member private NumpressDeflate1D(stream:Stream, peakArray: Peak1DArray) =
        let reader                  = new BinaryReader(stream, System.Text.Encoding.UTF8, true)
        //read information for decoding of intensities
        //numberEncodedBytes can maybe be omitted when taking the byteArrayDeflated.Length and substracting 5
        let numberEncodedBytesInt   = reader.ReadInt32()
        let originalDataLengthInt   = reader.ReadInt32()
        let compressedLengthInt     = reader.ReadInt32()
        let byteArrayInt            = reader.ReadBytes(compressedLengthInt)
        //read information for decoding of m/z
        let numberEncodedBytesMz    = reader.ReadInt32()
        let originalDataLengthMz    = reader.ReadInt32()
        let compressedLengthMz      = reader.ReadInt32()
        let byteArrayMz             = reader.ReadBytes(compressedLengthMz)
        let byteArrayIntDeflated    = BinaryDataDecoder.DeflateStreamDecompress(byteArrayInt)
        let byteArrayMzDeflated     = BinaryDataDecoder.DeflateStreamDecompress(byteArrayMz)
        let intensityArray          = NumpressDecodingHelpers.decodePIC (byteArrayIntDeflated, numberEncodedBytesInt, originalDataLengthInt)
        let mzArray                 = NumpressDecodingHelpers.decodeLin (byteArrayMzDeflated, numberEncodedBytesMz, originalDataLengthMz)

        Array.map2 (fun int mz -> new Peak1D(int, mz)) intensityArray mzArray
        |> fun peak1Ds -> peakArray.Peaks <- MzIOArray.ToMzIOArray<Peak1D>(peak1Ds)
        peakArray

    /// Decompress bytes based on numpress and zlib decompression method and convert to Peak2DArray.
    static member private NumpressDeflate2D(stream:Stream, peakArray: Peak2DArray) =
        let reader                  = new BinaryReader(stream, System.Text.Encoding.UTF8, true)
        //read information for decoding of intensities
        let numberEncodedBytesInt   = reader.ReadInt32()
        let originalDataLengthInt   = reader.ReadInt32()
        let compressedLengthInt     = reader.ReadInt32()
        let byteArrayInt            = reader.ReadBytes(compressedLengthInt)
        //read information for decoding of m/z
        let numberEncodedBytesMz    = reader.ReadInt32()
        let originalDataLengthMz    = reader.ReadInt32()
        let compressedLengthMz      = reader.ReadInt32()
        let byteArrayMz             = reader.ReadBytes(compressedLengthMz)
        //read information for decoding of retention time
        let numberEncodedBytesRt    = reader.ReadInt32()
        let originalDataLengthRt    = reader.ReadInt32()
        let compressedLengthRt      = reader.ReadInt32()
        let byteArrayRt             = reader.ReadBytes(compressedLengthRt)
        let byteArrayIntDeflated    = BinaryDataDecoder.DeflateStreamDecompress(byteArrayInt)
        let byteArrayMzDeflated     = BinaryDataDecoder.DeflateStreamDecompress(byteArrayMz)
        let byteArrayRtDeflated     = BinaryDataDecoder.DeflateStreamDecompress(byteArrayRt)
        let intensityArray          = NumpressDecodingHelpers.decodePIC (byteArrayIntDeflated, numberEncodedBytesInt, originalDataLengthInt)
        let mzArray                 = NumpressDecodingHelpers.decodeLin (byteArrayMzDeflated, numberEncodedBytesMz, originalDataLengthMz)
        let rtArray                 = NumpressDecodingHelpers.decodeLin (byteArrayRtDeflated, numberEncodedBytesRt, originalDataLengthRt)

        Array.map3 (fun int mz rt -> new Peak2D(int, mz, rt)) intensityArray mzArray rtArray
        |> fun peak2Ds -> peakArray.Peaks <- MzIOArray.ToMzIOArray<Peak2D>(peak2Ds)
        peakArray

    /// Convert stream to peaks and add to Peak1DArray.
    member this.Decode(stream:Stream, peakArray:Peak1DArray) =

        match peakArray.CompressionType with
        | BinaryDataCompressionType.NoCompression   ->  BinaryDataDecoder.NoCompression(stream, peakArray)
        | BinaryDataCompressionType.ZLib            ->  BinaryDataDecoder.ZLib2(stream, peakArray)
        | BinaryDataCompressionType.NumPress        ->  BinaryDataDecoder.Numpress1D(stream, peakArray)
        | BinaryDataCompressionType.NumPressZLib    ->  BinaryDataDecoder.NumpressDeflate1D(stream, peakArray)
        |   _   -> failwith (sprintf "%s%s" "Compression type not supported: " (peakArray.CompressionType.ToString()))

    /// Convert bytes to peaks and add to Peak1DArray.
    member this.Decode(peakArray:Peak1DArray, bytes:byte[]) =

        let memoryStream = new MemoryStream(bytes)

        this.Decode(memoryStream, peakArray)

    /// Convert bytes to peaks and add to Peak2DArray.
    member this.Decode(stream:Stream, peakArray:Peak2DArray) =

        match peakArray.CompressionType with
        | BinaryDataCompressionType.NoCompression   ->  BinaryDataDecoder.NoCompression(stream, peakArray)
        | BinaryDataCompressionType.ZLib            ->  BinaryDataDecoder.ZLib(stream, peakArray)
        | BinaryDataCompressionType.NumPress        ->  BinaryDataDecoder.Numpress2D(stream, peakArray)
        | BinaryDataCompressionType.NumPressZLib    ->  BinaryDataDecoder.NumpressDeflate2D(stream, peakArray)
        |   _   -> failwith (sprintf "%s%s" "Compression type not supported: " (peakArray.CompressionType.ToString()))

    /// Convert bytes to peaks and add to Peak2DArray.
    member this.Decode(peakArray:Peak2DArray, bytes:byte[]) =

        let memoryStream = new MemoryStream(bytes)

        this.Decode(memoryStream, peakArray)