namespace MzIO.Model


open System
open System.ComponentModel
open System.Linq.Expressions
open MzIO.Model.CvParam
open Newtonsoft.Json


/// An abstract base class of expansible description items that can be identified a name.
[<AbstractClass>]
type NamedItem(name:string) =

    inherit DynamicObj()

    let mutable name = 
        if String.IsNullOrWhiteSpace(name) then
            raise (ArgumentNullException("name"))
        else
            name
    
    let propertyChanged = new Event<_, _>()

    let propertyChanging = new Event<_, _>()

    new() = NamedItem("name")

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
    member this.Name
        with get() = name
            and internal set(value) = 
                if value = name then ()
                else
                    this.NotifyPropertyChaning("Name")
                    name <- value
                    this.NotifyPropertyChanged("Name")
    
    override this.Equals(obj:Object) =
        if Expression.ReferenceEquals(this, obj)=true then true
        else 
            if this.GetType().Equals(obj.GetType()) then false
            else name=this.Name
    
    override this.GetHashCode() =
        name.GetHashCode()

/// Base class of an observable collection of items that can be accessed by name. 
[<AbstractClass>]
type ObservableNamedItemCollection<'T when 'T :> NamedItem>() =

    inherit DynamicObj()

    member this.GetKeyForItem(item:'T) =
        item.Name

/// A small module to expanse the use of dynamic object to add named and model items in a specific manner.
module Helper =

    type DynamicObj with
        member this.AddNamedItem<'T when 'T :> NamedItem>(item:'T) =
            this.Add(item.Name, item)

        member this.AddModelItem<'T when 'T :> ModelItem>(item:'T) =
            this.Add(item.ID, item)