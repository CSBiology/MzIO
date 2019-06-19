namespace MzIO.Bruker


open System
open System.Runtime.InteropServices
open System.Runtime.Serialization
open System.Text


[<Serializable()>] [<Sealed>]
type Baf2SqlException() =

    inherit Exception()

    new(message:string) = new Baf2SqlException(message)

    new(message:string, innerException:Exception) = new Baf2SqlException(message, innerException)

    new(info:SerializationInfo, context:StreamingContext) = new Baf2SqlException(info, context)


/// <summary>
/// Helper methods to use Bruker's BAF loading C-API DLL.
/// </summary>
module Baf2SqlWrapper =

    #if WIN32 

    [<DllImport(@"baf2sql_c_32", CallingConvention = CallingConvention.Cdecl)>]
    extern UInt32 baf2sql_get_sqlite_cache_filename(StringBuilder sql_filename_buf, UInt32 sql_filename_buflen, String baf_filename)
    
    [<DllImport(@"baf2sql_c_32", CallingConvention = CallingConvention.Cdecl)>]
    extern UInt64 baf2sql_array_open_storage(int ignore_calibrator_ami, String filename)
    
    [<DllImport(@"baf2sql_c_32", CallingConvention = CallingConvention.Cdecl)>]
    extern void baf2sql_array_close_storage(UInt64 handle)
    
    [<DllImport(@"baf2sql_c_32", CallingConvention = CallingConvention.Cdecl)>]
    extern void baf2sql_array_get_num_elements(UInt64 , UInt64 id, (*ref*) UInt64 num_elements)
    
    [<DllImport(@"baf2sql_c_32", CallingConvention = CallingConvention.Cdecl)>]
    extern int baf2sql_array_read_double(UInt64 handle, UInt64 id, double[] buf)
    
    [<DllImport(@"baf2sql_c_32", CallingConvention = CallingConvention.Cdecl)>]
    extern int baf2sql_array_read_float(UInt64 handle, UInt64 id, float[] buf)
    
    [<DllImport(@"baf2sql_c_32", CallingConvention = CallingConvention.Cdecl)>]
    extern int baf2sql_array_read_uint32(UInt64 handle, UInt64 id, UInt32[] buf)
    
    [<DllImport(@"baf2sql_c_32", CallingConvention = CallingConvention.Cdecl)>]
    extern UInt32 baf2sql_get_last_error_string(StringBuilder buf, UInt32 len)
    
    [<DllImport(@"baf2sql_c_32", CallingConvention = CallingConvention.Cdecl)>]
    extern void baf2sql_set_num_threads(UInt32 n)
    #else

    [<DllImport(@"baf2sql_c_64", CallingConvention = CallingConvention.Cdecl)>]
    extern UInt32 baf2sql_get_sqlite_cache_filename(StringBuilder sql_filename_buf, UInt32 sql_filename_buflen, String baf_filename)
    
    [<DllImport(@"baf2sql_c_64", CallingConvention = CallingConvention.Cdecl)>]
    extern UInt64 baf2sql_array_open_storage(int ignore_calibrator_ami, String filename)
    
    [<DllImport(@"baf2sql_c_64", CallingConvention = CallingConvention.Cdecl)>]
    extern void baf2sql_array_close_storage(UInt64 handle)
    
    [<DllImport(@"baf2sql_c_64", CallingConvention = CallingConvention.Cdecl)>]
    extern void baf2sql_array_get_num_elements(UInt64 , UInt64 id, (*ref*) UInt64 num_elements)
    
    [<DllImport(@"baf2sql_c_64", CallingConvention = CallingConvention.Cdecl)>]
    extern int baf2sql_array_read_double(UInt64 handle, UInt64 id, double[] buf)
    
    [<DllImport(@"baf2sql_c_64", CallingConvention = CallingConvention.Cdecl)>]
    extern int baf2sql_array_read_float(UInt64 handle, UInt64 id, float[] buf)
    
    [<DllImport(@"baf2sql_c_64", CallingConvention = CallingConvention.Cdecl)>]
    extern int baf2sql_array_read_uint32(UInt64 handle, UInt64 id, UInt32[] buf)
    
    [<DllImport(@"baf2sql_c_64", CallingConvention = CallingConvention.Cdecl)>]
    extern UInt32 baf2sql_get_last_error_string(StringBuilder buf, UInt32 len)
    
    [<DllImport(@"baf2sql_c_64", CallingConvention = CallingConvention.Cdecl)>]
    extern void baf2sql_set_num_threads(UInt32 n)

    #endif
    
    type Baf2SqlWrapper =

        /// <summary>
        /// Throw last error string as an exception.
        /// </summary>
        static member ThrowLastBaf2SqlError() =
            
            let buf = new StringBuilder("")
            let len = baf2sql_get_last_error_string(buf, Convert.ToUInt32(0))
            buf.EnsureCapacity(int len + 1) |> ignore
            baf2sql_get_last_error_string(buf, len) |> ignore
            failwith ((new Baf2SqlException(buf.ToString())).ToString())

        /// <summary>
        /// Find out the file name of the SQL cache corresponding to the specified BAF file.
        /// (If the SQL cache doesn't exist yet, it will be created.) */
        /// </summary>
        static member GetSQLiteCacheFilename(baf_filename:string) =
            
            let buf = new StringBuilder("");
            let mutable len = baf2sql_get_sqlite_cache_filename(buf, uint32 0, baf_filename);
            if (len = Convert.ToUInt32(0)) then 
                failwith ((Baf2SqlWrapper.ThrowLastBaf2SqlError()).ToString())
            else 
                buf.EnsureCapacity(int len + 1) |> ignore
                len <- baf2sql_get_sqlite_cache_filename(buf, len, baf_filename)
                if len = uint32 0 then
                    failwith ((Baf2SqlWrapper.ThrowLastBaf2SqlError()).ToString())
                else buf.ToString()

        /// <summary>
        /// Given the Id of one spectral component (e.g., a 'ProfileMzId' from the SQL cache),
        /// load the binary data from the BAF (returning a double array).
        /// </summary>
        static member GetBafDoubleArray(handle:UInt64, id:UInt64) =

            let mutable n = uint64 0
            baf2sql_array_get_num_elements(handle, id, n)
            let myArray = Array.zeroCreate<float> (int n)
            let rc = baf2sql_array_read_double(handle, id, myArray)
            if rc = 0 then 
                failwith ((Baf2SqlWrapper.ThrowLastBaf2SqlError()).ToString())
            else myArray

        /// <summary>
        /// Return array 'id', converting to float format.
        /// </summary>
        static member GetBafFloatArray(handle:UInt64, id:UInt64) =

            let mutable n = uint64 0
            baf2sql_array_get_num_elements(handle, id, n);
            let myArray = Array.zeroCreate<float> (int n) 
            let rc = baf2sql_array_read_float(handle, id, myArray)
            if rc = 0 then 
                failwith ((Baf2SqlWrapper.ThrowLastBaf2SqlError()).ToString())
            else myArray

        /// <summary>
        /// Return array 'id', converting to UInt32 format.
        /// </summary>
        static member GetBafUInt32Array(handle:UInt64, id:UInt64) =
            let mutable n = uint64 0
            baf2sql_array_get_num_elements(handle, id, n);

            let myArray = Array.zeroCreate<UInt32> (int n) 
            let rc = baf2sql_array_read_uint32(handle, id, myArray);
            if rc = 0 then 
                failwith ((Baf2SqlWrapper.ThrowLastBaf2SqlError()).ToString())
            else myArray

