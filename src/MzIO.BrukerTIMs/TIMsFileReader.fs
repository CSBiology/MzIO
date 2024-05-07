namespace MzIO.Bruker


open System
open System.Data
open System.Collections.Generic
open System.IO
open System.Threading.Tasks
open MzIO.Binary
open MzIO.IO
open MzIO.Json
open MzIO.Model
open System.Linq
open MzIO.MetaData
open MzIO.MetaData.ParamEditExtension
open MzIO.MetaData.PSIMSExtension
open MzIO.MetaData.UO
open MzIO.MetaData.UO.UO
open MzIO.Commons.Arrays
open System.Collections.ObjectModel
open System.Data.SQLite
open MzIO.BrukerTIMs.TIMs
open MzIO.BrukerTIMs.Helper
open MzIO.BrukerTIMs.SQLite


type TIMsFileReader(analysisDirectory: string) =
    
    let td = new TimsData(analysisDirectory)
    
    // Reads one frame
    member this.ReadMassSpectrum(frameID: int, ?scanStart: int, ?scanEnd: int) =

        let scanStart = defaultArg scanStart 0
        let scanEnd = defaultArg scanEnd ((getScanCount(td.Conn, frameID)) |> int)
    
        let ms = new MassSpectrum(sprintf "Frame=%i"frameID)

        // ms Level
        let msLevel = getMSLevel(td.Conn, frameID)
        ms.SetMsLevel(msLevel) |> ignore
        ms.SetProfileSpectrum() |> ignore
        if msLevel = 1 then
            ms.SetMS1Spectrum() |>ignore
        elif msLevel > 1 then
            ms.SetMSnSpectrum() |> ignore
        else
            failwith "unknown MS Level"

        // Retention Time
        let rt = getRetentionTime(td.Conn, frameID) |> float
        let scan = new Scan()
        scan.SetScanStartTime(rt).UO_Minute()|> ignore
        ms.Scans.Add(Guid.NewGuid().ToString(), scan)

        // Precursors
        if msLevel > 1 then
            
            let precursorID =
                let precursors = getPrecursorFromScanRange(td.Conn, frameID, scanStart, scanEnd)
                if precursors.Length <> 1 then failwith "only one precursor per ms2 spectrum is supported"
                precursors |> List.head
            
            let isolationMz =
                getIsolationMz(td.Conn, frameID, precursorID)
            let isolationWidth =
                getIsolationWidth(td.Conn, frameID, precursorID)
            let collisionEnergy =
                getCollisionEnergy(td.Conn, frameID, precursorID)
            let precursorCharge =
                getPrecursorChargeState(td.Conn, precursorID)
            let precursorScanNumber =
                getPrecursorScanNumber(td.Conn, precursorID)
            let precursorMonoIsoMass =
                getPrecursorMonoIsoMass(td.Conn, precursorID)
            let parentID =
                getPrecursorParentFrame(td.Conn, precursorID)
                
            
            let precursor = new Precursor()
            precursor.Activation.SetCollisionEnergy(float collisionEnergy) |> ignore
            precursor.IsolationWindow.SetIsolationWindowTargetMz(float isolationMz) |> ignore
            precursor.IsolationWindow.SetIsolationWindowLowerOffset(float isolationWidth*0.5) |> ignore
            precursor.IsolationWindow.SetIsolationWindowUpperOffset(float isolationWidth*0.5) |> ignore
            let selectedIon = new SelectedIon()
            selectedIon.SetSelectedPrecursorMz(float precursorMonoIsoMass) |> ignore
            selectedIon.SetUserParam("ScanNumber", precursorScanNumber) |> ignore
            selectedIon.SetUserParam("MsLevel", 1) |> ignore
            selectedIon.SetChargeState(precursorCharge) |> ignore
            precursor.SelectedIons.Add(Guid.NewGuid().ToString(), selectedIon)
            precursor.SpectrumReference <- new SpectrumReference(sprintf "Frame=%i"parentID)
            ms.Precursors.Add(Guid.NewGuid().ToString(), precursor) |> ignore
        
        ms
