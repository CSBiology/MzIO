namespace MzIO.Model


open System
open MzIO.Model
open MzIO.Model.CvParam
open System.Collections.ObjectModel
open Newtonsoft.Json


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


//Normally PsiMSExtension
///// <summary>
///// The primary or reference m/z about which the isolation window is defined. [PSI:MS]
///// </summary>
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type IsolationWindow() =

    inherit DynamicObj()

[<AbstractClass>]
[<JsonObject(MemberSerialization.OptIn)>]
type IonSelectionMethod() =

    inherit DynamicObj()

    abstract member IsolationWindow : IsolationWindow

[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type Activation() =

    inherit DynamicObj()

[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type SelectedIon() =

    inherit DynamicObj()

[<Sealed>]
type SelectedIonList [<JsonConstructor>] () =
    inherit ObservableCollection<SelectedIon>()
    //is this the correct way for "internal SelectedIonList() { }"?
    //member internal this.SelectedIonList = SelectedIonList ()


[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type Precursor (spectrumReference:SpectrumReference, isolationWindow:IsolationWindow, selectedIons:SelectedIonList, activation:Activation) =

    inherit IonSelectionMethod()

    let mutable spectrumReference' = spectrumReference
    
    //right way for default?
    new(spectrumReference) = new Precursor(spectrumReference, new IsolationWindow(), new SelectedIonList(), new Activation())
    [<JsonConstructor>]
    new() = new Precursor(new SpectrumReference(), new IsolationWindow(), new SelectedIonList(), new Activation())

    // original was spectrumReference' <- spectrum reference, but initializing Precursor() with spectrumReference does the same?
    //member this.Precursor spectrumReference = Precursor (spectrumReference)
    //member this.Precursor() = this.Precursor(SpectrumReference ())

    [<JsonProperty>]
    override this.IsolationWindow = isolationWindow

    [<JsonProperty>]
    member this.Activation = activation

    [<JsonProperty>]
    member this.SelectedIons = selectedIons

    [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
    member this.SpectrumReference
        with get() = spectrumReference'
        and private set(value) = spectrumReference' <- value

[<Sealed>]
type PrecursorList [<JsonConstructor>] () =
    inherit ObservableCollection<Precursor>()
    //member internal this.PrecursorList = PrecursorList ()

[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type ScanWindow() =
    inherit DynamicObj()

[<Sealed>]
type ScanWindowList [<JsonConstructor>] () =
    inherit ObservableCollection<ScanWindow>()
    //is this the correct way for "internal ScanWindowList() { }"?
    //member internal this.ScanWindowList = ScanWindowList ()

[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type Scan (spectrumReference: SpectrumReference) =

    inherit DynamicObj()

    let mutable spectrumReference' = spectrumReference

    //do they need to be mutable?
    let scanWindows = new ScanWindowList()

    //right way for default?
    [<JsonConstructor>]
    new() = Scan (new SpectrumReference ())

    // original was spectrumReference' <- spectrum reference, but initializing Precursor() with spectrumReference does the same?
    //member this.Scan spectrumReference = Precursor(spectrumReference)

    [<JsonProperty>]
    member this.ScanWindows = scanWindows

    [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
    member this.SpectrumReference
        with get() = spectrumReference'
        and private set(value) = spectrumReference' <- value

[<Sealed>]
type ScanList [<JsonConstructor>] () =
    inherit ObservableCollection<Scan>()
    //member internal this.PrecursorList = ScanList ()

[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type Product(isolationWindow) =

    inherit IonSelectionMethod()

    new() = new Product(new IsolationWindow())

    [<JsonProperty>]
    override this.IsolationWindow = isolationWindow

[<Sealed>]
type ProductList [<JsonConstructor>] () =

    inherit ObservableCollection<Product>()
    //member internal this.ProductList = ProductList ()

[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type MassSpectrum (id:string, precursors:PrecursorList, scans:ScanList, products:ProductList,  sourceFileReference:string) =

    //inherit with variables or default constructors?
    inherit PeakList(id)

    //let precursors = new PrecursorList()

    //let scans = new ScanList()

    //let products = new ProductList()

    let mutable sourceFileReference = sourceFileReference

    [<JsonConstructor>]
    new(id:string) = MassSpectrum(id, new PrecursorList(), new ScanList(), new ProductList(), null)

    new() = MassSpectrum("id")

    [<JsonProperty>]
    member this.Precursors = precursors

    [<JsonProperty>]
    member this.Scans = scans

    [<JsonProperty>]
    member this.Products = products

    [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
    member this.SourceFileReference
        with get() = sourceFileReference
        and private set(value) = sourceFileReference <- value            

[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type Chromatogram [<JsonConstructor>] (id: string) =

    inherit PeakList(id)

    let precursor = new Precursor()

    let product = new Product()

    new() = Chromatogram("id")

    member this.Chromatogram = base.ID

    [<JsonProperty>]
    member this.Product = product

    [<JsonProperty>]
    member this.Precursor = precursor
