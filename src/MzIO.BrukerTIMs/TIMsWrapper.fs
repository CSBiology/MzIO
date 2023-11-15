namespace MzIO.BrukerTIMs

open System
open System.Runtime.InteropServices
open System.Text
open System.IO
open System.Data.SQLite

module TIMsWrapper =

    type ChromatogramJob =
        val mutable id : int64
        val mutable time_begin : float
        val mutable time_end : float
        val mutable mz_min : float
        val mutable mz_max : float
        val mutable ook0_min : float
        val mutable ook0_max : float

    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type MSMS_SPECTRUM_FUNCTOR = delegate of int64 * uint32 * float[] * float[] -> unit
    
    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type MSMS_PROFILE_SPECTRUM_FUNCTOR = delegate of int64 * uint32 * int32[] -> unit

    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type CHROMATOGRAM_JOB_GENERATOR = delegate of ChromatogramJob[] * IntPtr -> uint32

    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type CHROMATOGRAM_TRACE_SINK = delegate of int64 * uint32 * int64[] * uint64[] * IntPtr -> uint32
    
    [<DllImport(@"timsdata.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern uint64 tims_open_v2(string analysis_directory, uint32 use_recalibrated_state, uint32 pressure_compensation_strategy)

    [<DllImport(@"timsdata.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern void tims_close(uint64 handle)

    [<DllImport(@"timsdata.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern uint32 tims_get_last_error_string(StringBuilder error_string, uint32 error_string_length)

    [<DllImport(@"timsdata.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern uint32 tims_has_recalibrated_state(uint64 handle)

    [<DllImport(@"timsdata.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern uint32 tims_read_scans_v2(uint64 handle, int64 frame_id, uint32 scan_begin, uint32 scan_end, IntPtr buffer, uint32 buffer_length)

    [<DllImport(@"timsdata.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern uint32 tims_read_pasef_msms(uint64 handle, IntPtr precursor_list, uint32 num_precursors, MSMS_SPECTRUM_FUNCTOR callback)

    [<DllImport(@"timsdata.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern uint32 tims_read_pasef_msms_for_frame(uint64 handle, int64 frame_id, MSMS_SPECTRUM_FUNCTOR callback)
    
    [<DllImport(@"timsdata.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern uint32 tims_read_pasef_profile_msms(uint64 handle, IntPtr precursor_list, uint32 num_precursors, MSMS_PROFILE_SPECTRUM_FUNCTOR callback)

    [<DllImport(@"timsdata.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern uint32 tims_read_pasef_profile_msms_for_frame(uint64 handle, int64 frame_id, MSMS_PROFILE_SPECTRUM_FUNCTOR callback)

    [<DllImport(@"timsdata.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern uint32 tims_extract_centroided_spectrum_for_frame_v2(uint64 handle, int64 frame_id, uint32 scan_begin, uint32 scan_end, MSMS_SPECTRUM_FUNCTOR callback, IntPtr context)

    [<DllImport(@"timsdata.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern uint32 tims_extract_centroided_spectrum_for_frame_ext(uint64 handle, int64 frame_id, uint32 scan_begin, uint32 scan_end, double peak_picker_resolution, MSMS_SPECTRUM_FUNCTOR callback, IntPtr context)

    [<DllImport(@"timsdata.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern uint32 tims_extract_profile_for_frame(uint64 handle, int64 frame_id, uint32 scan_begin, uint32 scan_end, MSMS_PROFILE_SPECTRUM_FUNCTOR callback, IntPtr context)

    [<DllImport(@"timsdata.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern uint32 tims_extract_chromatograms(uint64 handle, CHROMATOGRAM_JOB_GENERATOR job_generator, CHROMATOGRAM_TRACE_SINK trace_sink, IntPtr user_data)

    [<DllImport(@"timsdata.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern uint32 tims_index_to_mz(uint64 handle, int64 frame_id, IntPtr indices, IntPtr mzs, uint32 count)

    [<DllImport(@"timsdata.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern uint32 tims_mz_to_index(uint64 handle, int64 frame_id, IntPtr mzs, IntPtr indices, uint32 count)

    [<DllImport(@"timsdata.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern uint32 tims_scannum_to_oneoverk0(uint64 handle, int64 frame_id, IntPtr scan_nums, IntPtr mobilities, uint32 count)

    [<DllImport(@"timsdata.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern uint32 tims_oneoverk0_to_scannum(uint64 handle, int64 frame_id, IntPtr mobilities, IntPtr scan_nums, uint32 count)

    [<DllImport(@"timsdata.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern uint32 tims_scannum_to_voltage(uint64 handle, int64 frame_id, IntPtr scan_nums, IntPtr voltages, uint32 count)

    [<DllImport(@"timsdata.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern uint32 tims_voltage_to_scannum(uint64 handle, int64 frame_id, IntPtr voltages, IntPtr scan_nums, uint32 count)

    [<DllImport(@"timsdata.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern double tims_oneoverk0_to_ccs_for_mz(double ook0, int32 charge, double mz)

    [<DllImport(@"timsdata.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern double tims_ccs_to_oneoverk0_for_mz(double ccs, int32 charge, double mz)