namespace MzIO.Model


open System.ComponentModel
open MzIO.Model
open MzIO.Model.CvParam
open Newtonsoft.Json


/// <summary>
/// This summarizes the different types of spectra that can be expected in the file. 
/// This is expected to aid processing software in skipping files that do not contain appropriate spectrum types for it. 
/// It should also describe the nativeID format used in the file by referring to an appropriate CV term.
/// </summary>
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type FileContent() =

    inherit DynamicObj()

[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type Contact() =
    
    inherit DynamicObj()

/// <summary>
/// Information pertaining to the entire mzML file 
/// (i.e. not specific to any part of the data set) is stored here.
/// </summary>
[<Sealed>]
[<JsonObject(MemberSerialization.OptIn)>]
type FileDescription [<JsonConstructor>] (contact:Contact, fileContent:FileContent, sourceFiles:SourceFileList) =

    let mutable fileContent' = fileContent

    let mutable sourceFiles' = sourceFiles

    let mutable contact'     = contact

    let propertyChanged = new Event<_, _>()

    let propertyChanging = new Event<_, _>()

    new() = FileDescription(new Contact(), new FileContent(), new SourceFileList())    

    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member this.PropertyChanged = propertyChanged.Publish

    interface INotifyPropertyChanging with
        [<CLIEvent>]
        member this.PropertyChanging = propertyChanging.Publish

    [<JsonProperty>]
    member this.SourceFiles = sourceFiles'

    [<JsonProperty(Required = Required.Always, ObjectCreationHandling = ObjectCreationHandling.Reuse)>]
    member this.FileContent = fileContent'

    member this.NotifyPropertyChanged (propertyName:string) =
        
        propertyChanged.Trigger(this, new PropertyChangedEventArgs(propertyName))
    
    member this.NotifyPropertyChaning (propertyName:string) =
        
        propertyChanging.Trigger(this, new PropertyChangingEventArgs(propertyName))
    
    [<JsonProperty>]
    member this.Contact
        with get() = contact' 
            and set(value) = 
                if value = contact' then ()
                else
                    this.NotifyPropertyChaning("Contact")
                    contact' <- value
                    this.NotifyPropertyChanged("Contact")
