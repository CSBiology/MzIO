namespace MzIO.MetaData


open System
open System.Globalization
open MzIO.Model.CvParam


/// Contains classes with methods to edit params.
module ParamEditExtension =
    
    /// Interface of a class to manipulate the unit of a cv or user param.
    type IHasUnit<'TPC when 'TPC :> DynamicObj> =

        abstract member ParamContainer  : 'TPC

        abstract member Param           : IParamBase<IConvertible>

        abstract member SetUnit         : string -> 'TPC

        abstract member NoUnit          : unit -> 'TPC

    /// Contains functions to change unit of cv or user param that was used to init the type.
    [<Sealed>]
    type HasUnit<'TPC when 'TPC :> DynamicObj> internal (pc:'TPC, p:IParamBase<IConvertible>) =

        inherit DynamicObj()

        let mutable paramContainer' = pc

        let mutable param'          = p

        interface IHasUnit<'TPC> with

            member this.ParamContainer  = paramContainer'

            member this.Param           = param'

            /// Sets a specific unit accession for the param.
            member this.SetUnit(unitAccession:string) =
                if (String.IsNullOrWhiteSpace(unitAccession)) then 
                    raise (ArgumentNullException("unitAccession","unitAccession may not be null or empty."))
                else
                    let paramValue = 
                        match tryGetValue param' with
                        | Some value -> new CvParam<IConvertible>(param'.ID, ParamValue.WithCvUnitAccession(value, unitAccession)) :> IParamBase<IConvertible>
                        | None       -> new CvParam<IConvertible>(param'.ID, ParamValue.WithCvUnitAccession(Unchecked.defaultof<IConvertible>, unitAccession)) :> IParamBase<IConvertible>
                    paramContainer'.SetValue(param'.ID, paramValue)
                    paramContainer'

            /// Remove all unit accessions of a param.
            member this.NoUnit() =
                let paramValue = 
                    match tryGetValue param' with
                    | Some value -> new CvParam<IConvertible>(param'.ID, ParamValue.CvValue(value)) :> IParamBase<IConvertible>
                    | None       -> new CvParam<IConvertible>(param'.ID) :> IParamBase<IConvertible>
                paramContainer'.SetValue(param'.ID, paramValue)
                paramContainer'

    
    type DynamicObj with

        /// Set value of existing cv param in dynamic object or add it if it doesn't exist already.
        member this.SetCvParam(accession:string, value:IConvertible) =

            let mutable param' =
                if (String.IsNullOrWhiteSpace(accession)) then 
                    raise (ArgumentNullException("accession","accession may not be null or empty."))
                else
                    let paramValue = ParamValue.CvValue(value)
                    let mutable param = new CvParam<IConvertible>(accession, paramValue)
                    if this.TryGetTypedValue<CvParam<IConvertible>>(accession).IsNone then
                        this.AddCvParam(param)
                    else ()
                    param :> IParamBase<IConvertible>
            new HasUnit<_>(this, param')

        /// Set value of existing cv param without value in dynamic object or add it if it doesn't exist already.
        member this.SetCvParam(accession:string) =

            let mutable param' =
                if (String.IsNullOrWhiteSpace(accession)) then 
                    raise (ArgumentNullException("accession","accession may not be null or empty."))
                else
                    let mutable param = new CvParam<IConvertible>(accession)
                    if this.TryGetTypedValue<CvParam<IConvertible>>(accession).IsNone then
                        this.AddCvParam(param)
                    else ()
                    param
            new HasUnit<_>(this, param' :> IParamBase<IConvertible>)

        /// Checks whether cv param with given accession exists in dynamic object or not.
        member this.HasCvParam(accession:string) =
            
           if (String.IsNullOrWhiteSpace(accession)) then 
                    raise (ArgumentNullException("accession","accession may not be null or empty."))
                else
                    this.TryGetItemByKey(accession, this)

        /// Set value of existing user param in dynamic object or add it if it doesn't exist already.
        member this.SetUserParam(name:string, value:IConvertible) =

            let mutable param' =
                if (String.IsNullOrWhiteSpace(name)) then 
                    raise (ArgumentNullException("name","name may not be null or empty."))
                else
                    let paramValue = ParamValue.CvValue(value)
                    let mutable param = new UserParam<IConvertible>(name, paramValue)
                    if this.TryGetTypedValue<UserParam<IConvertible>>(name).IsNone then
                        this.AddUserParam(param)
                    else ()
                    param
            new HasUnit<_>(this, param')

        /// Set value of existing user param without value in dynamic object or add it if it doesn't exist already.
        member this.SetUserParam(name:string) =

            let mutable param' =
                if (String.IsNullOrWhiteSpace(name)) then 
                    raise (ArgumentNullException("name","name may not be null or empty."))
                else
                    let mutable param = new UserParam<IConvertible>(name)
                    if this.TryGetTypedValue<UserParam<IConvertible>>(name).IsNone then
                        this.AddUserParam(param)
                    else ()
                    param
            new HasUnit<_>(this, param' :> IParamBase<IConvertible>)

        /// Checks whether user param with given accession exists in dynamic object or not.
        member this.HasUserParam(name:string) =
            
           if (String.IsNullOrWhiteSpace(name)) then 
                    raise (ArgumentNullException("name","name may not be null or empty."))
                else 
                    this.TryGetItemByKey(name, this)


        /// Has functionality to get cv or user param.
        member this.TryGetParam(accession:string) =
            if (String.IsNullOrWhiteSpace(accession)) then 
                    raise (ArgumentNullException("accession","accession may not be null or empty."))
                else
                    this.TryGetItemByKey(accession, this)

    type IParamBase<'T when 'T :> IConvertible> with

        /// Checks whether it has value or not.
        member this.HasValue() =
            if (tryGetValue this).IsSome then true
            else false

        /// Checks whether it has a unit or not.
        member this.HasUnit() =            
            if (tryGetCvUnitAccession this).IsSome then true
            else false

        /// Checks whether it has a specific unit or not.
        member this.HasUnit(unitAccession:string) =   
            let tmp = (tryGetCvUnitAccession this)
            if tmp.IsSome then tmp.Value = unitAccession
            else false

        /// Returns value and if it has no value returns a default value, depending on the type of the value.
        member this.GetValueOrDefault() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then tmp.Value
            else Unchecked.defaultof<'T>

        /// Returns value as a string or default value for string.
        member this.GetStringOrDefault() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then tmp.Value.ToString()
            else Unchecked.defaultof<string>

        /// Returns value as a bool or default value for bool.
        member this.GetBooleanOrDefault() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                try Convert.ToBoolean(tmp.Value)
                with 
                    | :? System.InvalidCastException -> Unchecked.defaultof<bool>
            else Unchecked.defaultof<bool>

        /// Returns value as a byte or default value for byte.
        member this.GetByteOrDefault() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                try Convert.ToByte(tmp.Value)
                with 
                    | :? System.InvalidCastException -> Unchecked.defaultof<Byte>
            else Unchecked.defaultof<Byte>

        /// Returns value as a char or default value for char.
        member this.GetCharOrDefault() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                try Convert.ToChar(tmp.Value)
                with 
                    | :? System.InvalidCastException -> Unchecked.defaultof<Char>
            else Unchecked.defaultof<Char>

        /// Returns value as a double or default value for double.
        member this.GetDoubleOrDefault() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                try Convert.ToDouble(tmp.Value)
                with 
                    | :? System.InvalidCastException -> Unchecked.defaultof<double>
            else Unchecked.defaultof<double>

        /// Returns value as a int32 or default value for int32.
        member this.GetInt32OrDefault() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                try Convert.ToInt32(tmp.Value)
                with 
                    | :? System.InvalidCastException -> Unchecked.defaultof<int32>
            else Unchecked.defaultof<int32>

        /// Returns value as a int64 or default value for int64.
        member this.GetInt64OrDefault() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                try Convert.ToInt64(tmp.Value)
                with 
                    | :? System.InvalidCastException -> Unchecked.defaultof<int64>
            else Unchecked.defaultof<int64>

        /// Returns value as a single or default value for single.
        member this.GetSingleOrDefault() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                try Convert.ToSingle(tmp.Value)
                with 
                    | :? System.InvalidCastException -> Unchecked.defaultof<single>
            else Unchecked.defaultof<single>

        /// Returns value as string or fails.
        member this.GetString() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                tmp.Value.ToString(new CultureInfo("en-US"))
            else
                raise (InvalidOperationException("Param value not set."))

        /// Returns value as bool or fails.
        member this.GetBoolean() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                tmp.Value.ToBoolean(new CultureInfo("en-US"))
            else
                raise (InvalidOperationException("Param value not set."))

        /// Returns value as byte or fails.
        member this.GetByte() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                tmp.Value.ToByte(new CultureInfo("en-US"))
            else
                raise (InvalidOperationException("Param value not set."))

        /// Returns value as char or fails.
        member this.GetChar() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                tmp.Value.ToChar(new CultureInfo("en-US"))
            else
                raise (InvalidOperationException("Param value not set."))

        /// Returns value as int32 or fails.
        member this.GetInt32() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                tmp.Value.ToInt32(new CultureInfo("en-US"))
            else
                raise (InvalidOperationException("Param value not set."))

        /// Returns value as int64 or fails.
        member this.GetInt64() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                tmp.Value.ToInt64(new CultureInfo("en-US"))
            else
                raise (InvalidOperationException("Param value not set."))

        /// Returns value as single or fails.
        member this.GetSingle() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                tmp.Value.ToSingle(new CultureInfo("en-US"))
            else
                raise (InvalidOperationException("Param value not set."))

        /// Returns value as double or fails.
        member this.GetDouble() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                tmp.Value.ToDouble(new CultureInfo("en-US"))
            else
                raise (InvalidOperationException("Param value not set."))
