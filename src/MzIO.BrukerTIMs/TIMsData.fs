namespace MzIO.BrukerTIMs

open System
open System.Runtime.InteropServices
open System.IO

module SQLite =

    open System.Data.SQLite

    let executeSQLiteQueryFloat(connection: SQLiteConnection, commandText: string) =
        try
            use command = new SQLiteCommand(commandText, connection)
            let reader = command.ExecuteReader()
            let rec loop (reader:SQLiteDataReader) acc =
                match reader.Read() with
                | true  -> loop reader ((reader.GetFloat(0))::acc)
                | false -> acc |> List.rev
            loop reader []
        with
        | :? SQLiteException as ex ->
            // Handle exceptions specific to SQLite
            failwithf "SQLite Exception: %s" ex.Message
        | ex ->
            // Handle other exceptions
            failwithf "Exception: %s" ex.Message

module Helper =
    
    type PressureCompensationStrategy =
    | NoPressureCompensation = 0
    | AnalyisGlobalPressureCompensation = 1
    | PerFramePressureCompensation = 2

    // Define a helper function to allocate unmanaged memory and copy data to it
    let copyToUnmanagedArrayFloat (data: float[]) =
        let ptr = Marshal.AllocHGlobal(data.Length * 8) // 8 bytes for a float
        Marshal.Copy(data,0,ptr,data.Length)
        // for i = 0 to data.Length - 1 do
        //     Marshal.WriteInt64(ptr, i * 8, BitConverter.DoubleToInt64Bits(data.[i]))
        ptr

    // Define a helper function to read a float from unmanaged memory
    let readFloat (ptr: IntPtr, index: int) =
        BitConverter.Int64BitsToDouble(Marshal.ReadInt64(ptr, index * 8))

    // Helper function to read a uint32 from unmanaged memory
    let readUInt32 (ptr: IntPtr, index: int) =
        uint32(Marshal.ReadInt32(ptr, index * 4)) // 4 bytes for a int32
        
module TIMs =

    open MzIO.BrukerTIMs.TIMsWrapper
    open System.Data.SQLite
    open Helper
    
    type TimsData(analysisDirectory: string, ?useRecalibratedState: bool, ?pressureCompensationStrategy: PressureCompensationStrategy) =

        let mutable handle = 0UL
        let mutable conn : SQLiteConnection = null
        let mutable initialFrameBufferSize = 128u

        do
            let useRecalibrated = defaultArg useRecalibratedState false
            let pressureStrategy = defaultArg pressureCompensationStrategy PressureCompensationStrategy.NoPressureCompensation

            handle <- tims_open_v2(analysisDirectory, (if useRecalibrated then 1u else 0u), uint32 pressureStrategy)

            //if handle = 0UL then
            //    _throwLastTimsDataError()

            conn <- new SQLiteConnection(Path.Combine("Data source="+analysisDirectory, "analysis.tdf"))
            conn.Open()

        member this.Handle
            with get() = handle
            and set(value) = handle <- value

        member this.InitialFrameBufferSize
            with get() = initialFrameBufferSize
            and set(value) = initialFrameBufferSize <- value

        member this.Close() =
            if handle <> 0UL then
                tims_close(handle)
                handle <- 0UL
            if not (conn = null) then
                conn.Close()
                conn <- null

        member this.CallConversionFunc(frameId: int64, inputArray: float[], func: (uint64 * int64 * IntPtr * IntPtr * uint32) -> uint32) =
            let inArray = copyToUnmanagedArrayFloat inputArray
            let cnt = inputArray.Length
            let outArray = Marshal.AllocHGlobal(int32 (8 * int cnt)) // 8 bytes for a float

            try
                let success =
                    func (this.Handle, frameId, inArray, outArray, (uint32 cnt))
                //if success = 0u then
                //    _throwLastTimsDataError this.dll
                let result = Array.init cnt (fun i -> readFloat(outArray, i))
                result
            finally
                Marshal.FreeHGlobal inArray
                Marshal.FreeHGlobal outArray

        member this.IndexToMz (frameId: int64, indices: float array) =
            this.CallConversionFunc(frameId, indices, tims_index_to_mz)
        
        member this.MzToIndex (frameId: int64, mzs: float array) =
            this.CallConversionFunc(frameId, mzs, tims_mz_to_index)

        member this.ScanNumToOneOverK0 (frameId: int64, scanNums: float array) =
            this.CallConversionFunc(frameId, scanNums, tims_scannum_to_oneoverk0)

        member this.OneOverK0ToScanNum (frameId: int64, mobilities: float array) =
            this.CallConversionFunc(frameId, mobilities, tims_oneoverk0_to_scannum)

        member this.ScanNumToVoltage (frameId: int64, scanNums: float array) =
            this.CallConversionFunc(frameId, scanNums, tims_scannum_to_voltage)

        member this.VoltageToScanNum (frameId: int64, voltages: float array) =
            this.CallConversionFunc(frameId, voltages, tims_voltage_to_scannum)


        interface IDisposable with
            member this.Dispose() =
                this.Close()
            
        member this.ReadScansDllBuffer (frameId: int64, scanBegin: uint32, scanEnd: uint32) =
            // Buffer-growing loop
            let rec growBuffer cnt =
                let buf = Marshal.AllocHGlobal(int cnt * 4) // Create an empty array
                let len = cnt * 4u // 4 bytes for a uint32
                let requiredLen = tims_read_scans_v2(this.Handle, frameId, scanBegin, scanEnd, buf, len)
                if requiredLen = 0u then
                    failwith "An error occurred."
                if requiredLen > uint32 len then
                    growBuffer (requiredLen / 4u + 1u)
                else
                    let result = Array.init (int cnt) (fun i -> readUInt32(buf, i))
                    Marshal.FreeHGlobal buf // Release unmanaged memory
                    result
            growBuffer this.InitialFrameBufferSize

        member this.ReadScans (frameId: int64, scanBegin: uint32, scanEnd: uint32): (uint32[]*uint32[])[]  =
            let buf = this.ReadScansDllBuffer(frameId, scanBegin, scanEnd)
            printfn "here"
            let mutable result = []
            let mutable d = int (scanEnd - scanBegin)
            for i in [(int scanBegin) .. (int scanEnd) - 1] do
                let nPeaks = buf.[i - int scanBegin]
                let indices = Array.sub buf d (int nPeaks)
                d <- d + int nPeaks
                let intensities = Array.sub buf d (int nPeaks)
                d <- d + int nPeaks
                result <- (indices, intensities) :: result

            result |> List.rev
            |> List.toArray