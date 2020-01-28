namespace MzIO.Model


open System
open System.Collections.Generic
open MzIO.Model
open MzIO.Model.CvParam
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Newtonsoft.Json.Serialization


/// An abstract base class of a expansible description model item that can be referenced by an id and has an additional dataProcessingReference.
[<AbstractClass>]
type PeakList (id:string, dataProcessingReference:string) =

    inherit ModelItem(id)

    let mutable dataProcessingReference = dataProcessingReference

    new(id) = PeakList (id, null)
    new() = PeakList ("id")

    //member internal this.PeakList = base.ID

    [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
    member this.DataProcessingReference
         with get() = dataProcessingReference
         and set(value) = dataProcessingReference <- value


/// The primary class to reference which isolation window is defined. [PSI:MS]
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type IsolationWindow() =

    inherit DynamicObj()

/// An abstract base class of a expansible description ion selection method that contains the dynamic object and has an additional isolationWindow.
[<AbstractClass>]
[<JsonObject(MemberSerialization.OptIn)>]
type IonSelectionMethod() =

    inherit DynamicObj()

    abstract member IsolationWindow : IsolationWindow

/// The primary class to reference which type and energy level was used for activation.
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type Activation() =

    inherit DynamicObj()

/// The primary class to save the selected ions using controlled (cvParam) or uncontrolled vocabulary (userParam).
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type SelectedIon() =

    inherit DynamicObj()

/// The dynamic object container for all selected ions in the current spectrum.
[<Sealed>]
type SelectedIonList [<JsonConstructor>] () =

    inherit MzIO.Model.ObservableCollection<SelectedIon>()

/// The primary class to reference the method of precursor ion selection and activation.
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type Precursor (spectrumReference:SpectrumReference, isolationWindow:IsolationWindow, selectedIons:SelectedIonList, activation:Activation) =

    inherit IonSelectionMethod()

    let mutable spectrumReference' = spectrumReference
    
    new(spectrumReference) = new Precursor(spectrumReference, new IsolationWindow(), new SelectedIonList(), new Activation())
    [<JsonConstructor>]
    new() = new Precursor(new SpectrumReference(), new IsolationWindow(), new SelectedIonList(), new Activation())

    [<JsonProperty>]
    override this.IsolationWindow = isolationWindow

    [<JsonProperty>]
    member this.Activation = activation

    [<JsonProperty>]
    member this.SelectedIons = selectedIons

    [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
    member this.SpectrumReference
        with get() = spectrumReference'
        and set(value) = spectrumReference' <- value

/// The dynamic object container for all precursor ion isolations in the current spectrum.
[<Sealed>]
[<AllowNullLiteral>]
type PrecursorList [<JsonConstructor>] () =

    inherit MzIO.Model.ObservableCollection<Precursor>()

/// The primary class to reference the range of m/z values over which the instrument scans and aquires a spectrum. 
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type ScanWindow() =
    inherit DynamicObj()

/// The dynamic object container for all scanwindows of the current spectrum.
[<Sealed>]
type ScanWindowList [<JsonConstructor>] () =

    inherit MzIO.Model.ObservableCollection<ScanWindow>()

/// A class to reference the scan or acquisition from original raw file used to create this peak list, as specified in sourceFile. 
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type Scan [<JsonConstructor>] (spectrumReference: SpectrumReference, scanWindows:ScanWindowList) =

    inherit DynamicObj()

    let mutable spectrumReference' = spectrumReference

    new() = Scan (new SpectrumReference (), new ScanWindowList())

    [<JsonProperty>]
    member this.ScanWindows = scanWindows

    [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
    member this.SpectrumReference
        with get() = spectrumReference'
        and private set(value) = spectrumReference' <- value

/// The dynamic object container for descriptions of all scans of the current spectrum.
[<Sealed>]
[<AllowNullLiteral>]
type ScanList [<JsonConstructor>] () =

    inherit MzIO.Model.ObservableCollection<Scan>()

/// The primary class to reference the method of product ion selection and activation in a precursor ion selection scan.
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type Product(isolationWindow) =

    inherit IonSelectionMethod()

    new() = new Product(new IsolationWindow())

    [<JsonProperty>]
    override this.IsolationWindow = isolationWindow

/// The dynamic object container for all product ion isolations in the current spectrum.
[<Sealed>]
[<AllowNullLiteral>]
type ProductList [<JsonConstructor>] () =

    inherit MzIO.Model.ObservableCollection<Product>()

/// The class to reference the spectrum specific metadata without actual peak data.
/// Captures the settings of the isolation windows, information about precursor- and product ions 
/// and references the source files.
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type MassSpectrum [<JsonConstructor>] (id:string, dataProcessingReference:string, precursors:PrecursorList, scans:ScanList, products:ProductList,  sourceFileReference:string) =

    inherit PeakList(id, dataProcessingReference)

    let mutable sourceFileReference = sourceFileReference
    let mutable precursors = precursors
    let mutable scans = scans
    let mutable products = products

    new(id:string) = MassSpectrum(id, null, new PrecursorList(), new ScanList(), new ProductList(), null)

    new() = MassSpectrum("id")

    [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
    member this.Precursors
        with get() = precursors
        and set(value) = precursors <- value 

    [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
    member this.Scans
        with get() = scans
        and set(value) = scans <- value 

    [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
    member this.Products
        with get() = products
        and set(value) = products <- value 

    [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
    member this.SourceFileReference
        with get() = sourceFileReference
        and set(value) = sourceFileReference <- value            

/// Not implemented fully yet.
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type Chromatogram [<JsonConstructor>] (id: string, precursor:Precursor, product:Product) =

    inherit PeakList(id)

    new(id) = Chromatogram(id, new Precursor(), new Product())
    new() = Chromatogram("id", new Precursor(), new Product())

    [<JsonProperty>]
    member this.Precursor = precursor

    [<JsonProperty>]
    member this.Product = product
