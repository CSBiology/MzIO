namespace MzIO.Model


open System
open System.ComponentModel
open System.Collections.Generic
open System.Linq.Expressions
open MzIO.Model
open MzIO.Model.CvParam
open Newtonsoft.Json


/// <summary>
/// An abstract base class of a expansible description model item that can be referenced by an id.
/// </summary> 
[<AbstractClass>]
type ModelItem(id:string)  =
    
    inherit DynamicObj()

    let mutable id = 
        if String.IsNullOrWhiteSpace(id) then
            raise (ArgumentNullException("id"))
        else
            id

    let propertyChanged = new Event<_, _>()

    let propertyChanging = new Event<_, _>()

    new() = ModelItem("id")

    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member this.PropertyChanged = propertyChanged.Publish

    interface INotifyPropertyChanging with
        [<CLIEvent>]
        member this.PropertyChanging = propertyChanging.Publish

    member this.NotifyPropertyChanged (propertyName:string) =
        
        propertyChanged.Trigger(this, new PropertyChangedEventArgs(propertyName))
    
    member this.NotifyPropertyChaning (propertyName:string) =
        
        propertyChanging.Trigger(this, new PropertyChangingEventArgs(propertyName))

    [<JsonProperty(Required = Required.Always)>]
    member this.ID
        with get() = id
            and internal set(value) = 
                if value = id then ()
                else
                    this.NotifyPropertyChaning("ID")
                    this.ID <- value
                    this.NotifyPropertyChanged("ID")
    
    override this.GetHashCode() =
        this.ID.GetHashCode()

    override this.Equals(obj:Object) =
        if Expression.ReferenceEquals(this, obj)=true then true
        else 
            if not (this.GetType().Equals(obj.GetType())) then false
            else id=this.ID

/// <summary>
/// An abstract base class of a expansible description model item that can be referenced by an id and has an additional name.
/// </summary>
[<AbstractClass>]
type NamedModelItem(id:string, name:string) =

    inherit ModelItem(id)    

    let mutable name   = 
        if String.IsNullOrWhiteSpace(name) then
            raise (ArgumentNullException("name"))
        else
            name 

    new() = NamedModelItem("id", "name")

    //member this.name = name'

    [<JsonProperty(Required = Required.Always)>]
    member this.Name
        with get() = name
            and set(value) = 
                if value = name then ()
                else
                    this.NotifyPropertyChaning("Name")
                    this.Name <- value
                    this.NotifyPropertyChanged("Name")
    
/// <summary>
/// Base class of an observable collection of model items that can be accessed by their embedded ids.     
/// </summary>
type ObservableModelItemCollection<'T when 'T :> ModelItem> [<JsonConstructor>] internal (dict:Dictionary<string, obj>) =

    inherit DynamicObj(dict)

    new() =  ObservableModelItemCollection<'T>(new Dictionary<string, obj>())

    member this.GetKeyForItem(item:'T) =
        item.ID

type ObservableCollection<'T when 'T :> DynamicObj> [<JsonConstructor>] () =

    inherit DynamicObj()

    member this.Count() =
        this.GetProperties false
        |> Seq.length
