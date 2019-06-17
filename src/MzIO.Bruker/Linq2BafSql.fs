namespace MzIO.Bruker


open System
open System.Collections.Generic
open System.Data.Linq
open System.Data.Linq.Mapping
open System.Data.SQLite
open System.Linq


module Linq2BafSql =

    [<Sealed>]
    [<Table(Name = "Spectra")>]
    type BafSqlSpectrum() =
        
        let mutable id              = Unchecked.defaultof<UInt64 option>
        let mutable rt              = Unchecked.defaultof<double option>
        let mutable seg             = Unchecked.defaultof<UInt64 option>
        let mutable aqk             = Unchecked.defaultof<UInt64 option>
        let mutable parent          = Unchecked.defaultof<UInt64 option>
        let mutable mzAqRL          = Unchecked.defaultof<UInt64 option>
        let mutable mzAqRUpper      = Unchecked.defaultof<UInt64 option>
        let mutable sumInt          = Unchecked.defaultof<float option>
        let mutable maxInt          = Unchecked.defaultof<float option>
        let mutable tranForId       = Unchecked.defaultof<UInt64 option>
        let mutable profMzId        = Unchecked.defaultof<UInt64 option>
        let mutable profIntId       = Unchecked.defaultof<UInt64 option>
        let mutable lineIndexId     = Unchecked.defaultof<UInt64 option>
        let mutable lineMzId        = Unchecked.defaultof<UInt64 option>
        let mutable lineIntId       = Unchecked.defaultof<UInt64 option>
        let mutable lineIdxWithId   = Unchecked.defaultof<UInt64 option>
        let mutable linePeakAreaId  = Unchecked.defaultof<UInt64 option>
        let mutable lineSnrId       = Unchecked.defaultof<UInt64 option>

        [<Column(IsPrimaryKey = true)>]
        member this.Id 
            with get() = id 
            and  set(value) = id <- value
            
        [<Column>]                              
        member this.Rt
            with get() = rt 
            and  set(value) = rt <- value
        [<Column>]                              
        member this.Segment
            with get() = seg 
            and  set(value) = seg <- value
        [<Column>]                              
        member this.AcquisitionKey
            with get() = aqk 
            and  set(value) = aqk <- value
        [<Column>]                              
        member this.Parent
            with get() = parent 
            and  set(value) = parent <- value
        [<Column>]                              
        member this.MzAcqRangeLower
            with get() = mzAqRL 
            and  set(value) = mzAqRL <- value
        [<Column>]                              
        member this.MzAcqRangeUpper
            with get() = mzAqRUpper 
            and  set(value) = mzAqRUpper <- value
        [<Column>]                              
        member this.SumIntensity
            with get() = sumInt 
            and  set(value) = sumInt <- value
        [<Column>]                              
        member this.MaxIntensity
            with get() = maxInt 
            and  set(value) = maxInt <- value
        [<Column>]                              
        member this.TransformatorId
            with get() = tranForId 
            and  set(value) = tranForId <- value
        [<Column>]                              
        member this.ProfileMzId
            with get() = profMzId 
            and  set(value) = profMzId <- value
        [<Column>]                              
        member this.ProfileIntensityId
            with get() = profIntId 
            and  set(value) = profIntId <- value
        [<Column>]                               
        member this.LineIndexId
            with get() = lineIndexId 
            and  set(value) = lineIndexId <- value
        [<Column>]                              
        member this.LineMzId
            with get() = lineMzId 
            and  set(value) = lineMzId <- value
        [<Column>]                               
        member this.LineIntensityId     
            with get() = lineIntId 
            and  set(value) = lineIntId <- value
        [<Column>]                              
        member this.LineIndexWidthId
            with get() = lineIdxWithId 
            and  set(value) = lineIdxWithId <- value
        [<Column>]                              
        member this.LinePeakAreaId
            with get() = linePeakAreaId 
            and  set(value) = linePeakAreaId <- value
        [<Column>]                              
        member this.LineSnrId
            with get() = lineSnrId 
            and  set(value) = lineSnrId <- value

    [<Sealed>]
    [<Table(Name = "AcquisitionKeys")>]
    type BafSqlAcquisitionKey() =

        let mutable id          = Unchecked.defaultof<UInt64 option>
        let mutable poalrity    = Unchecked.defaultof<int option>
        let mutable scanMode    = Unchecked.defaultof<int option>
        let mutable aqMode      = Unchecked.defaultof<int option>
        let mutable msLevel     = Unchecked.defaultof<int option>

        [<Column(IsPrimaryKey = true)>]
        member this.Id
            with get() = id 
            and  set(value) = id <- value
        [<Column>]                      
        member this.Polarity
            with get() = poalrity 
            and  set(value) = poalrity <- value
        [<Column>]                      
        member this.ScanMode
            with get() = scanMode 
            and  set(value) = scanMode <- value
        [<Column>]                      
        member this.AcquisitionMode
            with get() = aqMode 
            and  set(value) = aqMode <- value
        [<Column>]                      
        member this.MsLevel
            with get() = msLevel 
            and  set(value) = msLevel <- value

    [<Sealed>]
    [<Table(Name = "PerSpectrumVariables")>]
    type BafSqlPerSpectrumVariable() =

        let mutable spec    = Unchecked.defaultof<UInt64 option>
        let mutable var     = Unchecked.defaultof<UInt64 option>
        let mutable value'  = Unchecked.defaultof<Decimal option>

        [<Column(IsPrimaryKey = true)>]
        member this.Spectrum
            with get() = spec 
            and  set(value) = spec <- value
        [<Column(IsPrimaryKey = true)>]
        member this.Variable
            with get() = var 
            and  set(value) = var <- value
        [<Column>]                      
        member this.Value
            with get() = value' 
            and  set(value) = value' <- value

    [<Sealed>]
    [<Table(Name = "SupportedVariables")>]
    type BafSqlSupportedVariable() =

        let mutable var             = Unchecked.defaultof<UInt64 option>
        let mutable permName        = Unchecked.defaultof<string>
        let mutable type'           = Unchecked.defaultof<UInt64 option>
        let mutable disGroupName    = Unchecked.defaultof<string>
        let mutable disName         = Unchecked.defaultof<string>
        let mutable disValueText    = Unchecked.defaultof<string>
        let mutable disFor          = Unchecked.defaultof<string>
        let mutable disDim          = Unchecked.defaultof<string>

        [<Column(IsPrimaryKey = true)>]
        member this.Variable
            with get() = var
            and  set(value) = var <- value
        [<Column>]                              
        member this.PermanentName
            with get() = permName
            and  set(value) = permName <- value
        [<Column>]                              
        member this.Type
            with get() = type'
            and  set(value) = type' <- value
        [<Column>]                              
        member this.DisplayGroupName
            with get() = disGroupName
            and  set(value) = disGroupName <- value
        [<Column>]                              
        member this.DisplayName
            with get() = disName
            and  set(value) = disName <- value
        [<Column>]                              
        member this.DisplayValueText
            with get() = disValueText
            and  set(value) = disValueText <- value
        [<Column>]                              
        member this.DisplayFormat
            with get() = disFor
            and  set(value) = disFor <- value
        [<Column>]                              
        member this.DisplayDimension
            with get() = disDim
            and  set(value) = disDim <- value


    [<Sealed>]
    [<Table(Name = "Steps")>]
    type BafSqlStep() =
        
        let mutable tarSpec = Unchecked.defaultof<UInt64 option>
        let mutable num     = Unchecked.defaultof<int option>
        let mutable isoType = Unchecked.defaultof<int option>
        let mutable reaType = Unchecked.defaultof<int option>
        let mutable msLvl   = Unchecked.defaultof<int option>
        let mutable mass    = Unchecked.defaultof<float option>

        [<Column>]
        member this.TargetSpectrum
            with get() = tarSpec
            and  set(value) = tarSpec <- value
        [<Column>]                          
        member this.Number
            with get() = num
            and  set(value) = num <- value
        [<Column>]                          
        member this.IsolationType
            with get() = isoType
            and  set(value) = isoType <- value
        [<Column>]                          
        member this.ReactionType
            with get() = reaType
            and  set(value) = reaType <- value
        [<Column>]                          
        member this.MsLevel
            with get() = msLvl
            and  set(value) = msLvl <- value
        [<Column>]                          
        member this.Mass
            with get() = mass
            and  set(value) = mass <- value

    [<Sealed>]
    type Linq2BafSql(sqlFilePath:string) =

        let mapping = new AttributeMappingSource()
        let mutable isDisposed = false
        let cn = new SQLiteConnection("Data Source=" + sqlFilePath + ";Version=3")
        let core = new DataContext(cn, mapping)
        let mutable changes =
            core.DeferredLoadingEnabled <- false
            core.ObjectTrackingEnabled <- false

        // opening a connection at this point leads to a 7 fold speed increase when looking up mass spectra.
        let cnOpen = core.Connection.Open()

        // increases speed of massspectra look up but is not compatible with a
        // function in baf2sql_c.dll (probably when creating the sqlite cache) because they are blocking the db access of each another
        // TODO: Examine which baf2sql_c method is causing this.    
        //System.Data.Common.DbTransaction tn = core.Connection.BeginTransaction();
        let checkInDexStepsID =
            try
                core.ExecuteQuery<int>("CREATE INDEX StepsID ON Steps (TargetSpectrum)")
            with
                | :? Exception ->
                    failwith "INDEX On TargedSpectrum in Steps table already exists, creation is skipped" 
        
        let checkIndexSpectrumID =
            try
                core.ExecuteQuery<int>("CREATE INDEX SpectrumID ON PerSpectrumVariables (Spectrum)")
            with
                | :? Exception ->
                    failwith "INDEX On SpectrumID in PerSpectrumVariables table already exists, creation is skipped"

        interface IDisposable with

            member this.Dispose() =
                if isDisposed then ()
                else
                    if core <> null then core.Dispose()
                isDisposed <- true

        member this.Core  = core

        member this.Spectra = core.GetTable<BafSqlSpectrum>()

        member this.AcquisitionKeys = core.GetTable<BafSqlAcquisitionKey>()

        member this.PerSpectrumVariables = core.GetTable<BafSqlPerSpectrumVariable>()

        member this.SupportedVariables = core.GetTable<BafSqlSupportedVariable>()

        member this.Steps = core.GetTable<BafSqlStep>()

        member this.GetBafSqlSpectrum(context, id) =
            CompiledQuery.Compile(fun db id -> db.GetTable<BafSqlSpectrum>().Where(fun x -> x.Id = id).SingleOrDefault())

        member this.GetBafSqlAcquisitionKey(context, id) =
            CompiledQuery.Compile(fun db id -> db.GetTable<BafSqlAcquisitionKey>().Where(fun x -> x.Id = id).SingleOrDefault())

        member this.GetBafSqlSteps(context, id) =
            CompiledQuery.Compile(fun db id -> db.GetTable<BafSqlStep>().Where(fun x -> x.TargetSpectrum = id).SingleOrDefault())

        //member this.GetPerSpectrumVariables(context, id) =
        //    CompiledQuery.Compile(fun db id -> db.GetTable<BafSqlPerSpectrumVariable>().Where(fun x -> x.Spectrum = id).SingleOrDefault())

        member this.GetPerSpectrumVariables(context, id) =
            CompiledQuery.Compile(fun db id -> db.GetTable<BafSqlPerSpectrumVariable>().Where(fun x -> 
                x.Spectrum = id && x.Variable.IsSome && x.Value.IsSome).SingleOrDefault())