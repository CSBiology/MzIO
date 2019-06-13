namespace MzLiteFSharp.Model


open System
open System.ComponentModel
open System.Linq.Expressions
open MzLiteFSharp.Model.CvParam
open Newtonsoft.Json


/// <summary>
/// An abstract base class of expansible description items that can be identified a name.
/// </summary>
[<AbstractClass>]
type NamedItem(name:string) =

    inherit DynamicObj()

    let mutable name = 
        match name with
        | null  -> failwith (ArgumentNullException("name").ToString())
        | ""    -> failwith (ArgumentNullException("name").ToString())
        | " "   -> failwith (ArgumentNullException("name").ToString())
        |   _   -> name    
    
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
    
    //member private this.name = name'

    [<JsonProperty(Required = Required.Always)>]
    member this.Name
        with get() = name
            and internal set(value) = 
                if value = name then ()
                else
                    this.NotifyPropertyChaning("Name")
                    this.Name <- value
                    this.NotifyPropertyChanged("Name")
    
    override this.Equals(obj:Object) =
        if Expression.ReferenceEquals(this, obj)=true then true
        else 
            if this.GetType().Equals(obj.GetType()) then false
            else name=this.Name
    
    override this.GetHashCode() =
        name.GetHashCode()

/// <summary>
/// Base class of an observable collection of items that can be accessed by name. 
/// </summary>
[<AbstractClass>]
type ObservableNamedItemCollection<'T when 'T :> NamedItem>() =

    inherit DynamicObj()

    member this.GetKeyForItem(item:'T) =
        item.Name

module Helper =

    type DynamicObj with
        member this.AddNamedItem<'T when 'T :> NamedItem>(item:'T) =
            this.Add(item.Name, item)

        member this.AddModelItem<'T when 'T :> ModelItem>(item:'T) =
            this.Add(item.ID, item)