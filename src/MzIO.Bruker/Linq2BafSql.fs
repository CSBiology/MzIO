namespace MzIO.Bruker


open System
open System.Collections.Generic
open System.Data.Linq
open System.Data.Linq.Mapping
open System.Data.SQLite
open System.Linq


module Linq2BafSql =

    [<Table(Name = "Spectra")>]
    type BafSqlSpectrum =
        {
            [<Column(IsPrimaryKey = true)>]
            Id                  : Nullable<UInt64>  //{ get; set; }
            [<Column>]                              
            Rt                  : Nullable<double>  //{ get; set; }
            [<Column>]                              
            Segment             : Nullable<UInt64>  //{ get; set; }
            [<Column>]                              
            AcquisitionKey      : Nullable<UInt64>  //{ get; set; }
            [<Column>]                              
            Parent              : Nullable<UInt64>  //{ get; set; }
            [<Column>]                              
            MzAcqRangeLower     : Nullable<UInt64>  //{ get; set; }
            [<Column>]                              
            MzAcqRangeUpper     : Nullable<UInt64>  //{ get; set; }
            [<Column>]                              
            SumIntensity        : Nullable<double>  //{ get; set; }
            [<Column>]                              
            MaxIntensity        : Nullable<double>  //{ get; set; }
            [<Column>]                              
            TransformatorId     : Nullable<UInt64>  //{ get; set; }
            [<Column>]                              
            ProfileMzId         : Nullable<UInt64>  //{ get; set; }
            [<Column>]                              
            ProfileIntensityId  : Nullable<UInt64>  //{ get; set; }
            [<Column>]                               
            LineIndexId         : Nullable<UInt64>  //{ get; set; }
            [<Column>]                              
            LineMzId            : Nullable<UInt64>  //{ get; set; }
            [<Column>]                               
            LineIntensityId     : Nullable<UInt64>  //{ get; set; }
            [<Column>]                              
            LineIndexWidthId    : Nullable<UInt64>  //{ get; set; }
            [<Column>]                              
            LinePeakAreaId      : Nullable<UInt64>  //{ get; set; }
            [<Column>]                              
            LineSnrId           : Nullable<UInt64>  //{ get; set; }
        }

    let private createBafSqlSpectrum id rt segment aqKey parent mzAqRLower mzAQRUpper szmInt maxInt tForId 
        proMzId proInt lineIdxId lineMzId lineIntId lineIdxWithId linePeakAreaId lineSnrId =
        {
            BafSqlSpectrum.Id                   = id
            BafSqlSpectrum.Rt                   = rt
            BafSqlSpectrum.Segment              = segment
            BafSqlSpectrum.AcquisitionKey       = aqKey
            BafSqlSpectrum.Parent               = parent
            BafSqlSpectrum.MzAcqRangeLower      = mzAqRLower
            BafSqlSpectrum.MzAcqRangeUpper      = mzAQRUpper
            BafSqlSpectrum.SumIntensity         = szmInt
            BafSqlSpectrum.MaxIntensity         = maxInt
            BafSqlSpectrum.TransformatorId      = tForId
            BafSqlSpectrum.ProfileMzId          = proMzId
            BafSqlSpectrum.ProfileIntensityId   = proInt
            BafSqlSpectrum.LineIndexId          = lineIdxId
            BafSqlSpectrum.LineMzId             = lineMzId
            BafSqlSpectrum.LineIntensityId      = lineIntId
            BafSqlSpectrum.LineIndexWidthId     = lineIdxWithId
            BafSqlSpectrum.LinePeakAreaId       = linePeakAreaId
            BafSqlSpectrum.LineSnrId            = lineSnrId
        }

    [<Table(Name = "AcquisitionKeys")>]
    type BafSqlAcquisitionKey =
        {
            [<Column(IsPrimaryKey = true)>]
            Id              : Nullable<UInt64>  //{ get; set; }
            [<Column>]                      
            Polarity        : Nullable<int>     //{ get; set; }
            [<Column>]                      
            ScanMode        : Nullable<int>     //{ get; set; }
            [<Column>]                      
            AcquisitionMode : Nullable<int>     //{ get; set; }
            [<Column>]                      
            MsLevel         : Nullable<int>     //{ get; set; }
        }   

    let private createBafSqlAcquisitionKey id polarity scanMode aqMode msLvl =
        {
            BafSqlAcquisitionKey.Id              = id
            BafSqlAcquisitionKey.Polarity        = polarity
            BafSqlAcquisitionKey.ScanMode        = scanMode
            BafSqlAcquisitionKey.AcquisitionMode = aqMode
            BafSqlAcquisitionKey.MsLevel         = msLvl
        } 

    [<Table(Name = "PerSpectrumVariables")>]
    type BafSqlPerSpectrumVariable =
        {
            [<Column(IsPrimaryKey = true)>]
            Spectrum    : Nullable<UInt64>  //{ get; set; }
            [<Column(IsPrimaryKey = true)>]
            Variable    : Nullable<UInt64>  //{ get; set; }
            [<Column>]                      
            Value       : Nullable<decimal> //{ get; set; }
        } 

    let private createBafSqlPerSpectrumVariable spectrum variable value =
        {
            BafSqlPerSpectrumVariable.Spectrum  = spectrum
            BafSqlPerSpectrumVariable.Variable  = variable
            BafSqlPerSpectrumVariable.Value     = value
        }

    [<Table(Name = "SupportedVariables")>]
    type BafSqlSupportedVariable =
        {
            [<Column(IsPrimaryKey = true)>]
            Variable            : Nullable<UInt64>  //{ get; set; }
            [<Column>]                              
            PermanentName       : string            //{ get; set; }
            [<Column>]                              
            Type                : Nullable<UInt64>  //{ get; set; }
            [<Column>]                              
            DisplayGroupName    : string            //{ get; set; }
            [<Column>]                              
            DisplayName         : string            //{ get; set; }
            [<Column>]                              
            DisplayValueText    : string            //{ get; set; }
            [<Column>]                              
            DisplayFormat       : string            //{ get; set; }
            [<Column>]                              
            DisplayDimension    : string            //{ get; set; }
        }

    let private createBafSqlSupportedVariable var permanentName type' dispGroupName dispName dispValueText dispFormat dispDimension =
        {
            BafSqlSupportedVariable.Variable            = var
            BafSqlSupportedVariable.PermanentName       = permanentName
            BafSqlSupportedVariable.Type                = type'
            BafSqlSupportedVariable.DisplayGroupName    = dispGroupName
            BafSqlSupportedVariable.DisplayName         = dispName
            BafSqlSupportedVariable.DisplayValueText    = dispValueText
            BafSqlSupportedVariable.DisplayFormat       = dispFormat
            BafSqlSupportedVariable.DisplayDimension    = dispDimension
        }

    [<Table(Name = "Steps")>]
    type BafSqlStep =
        {
            [<Column>]
            TargetSpectrum : Nullable<UInt64>   //{ get; set; }
            [<Column>]                          
            Number : Nullable<int>              //{ get; set; }
            [<Column>]                          
            IsolationType : Nullable<int>       //{ get; set; }
            [<Column>]                          
            ReactionType : Nullable<int>        //{ get; set; }
            [<Column>]                          
            MsLevel : Nullable<int>             //{ get; set; }
            [<Column>]                          
            Mass : Nullable<double>             //{ get; set; }
        }

    let private createBafSqlStep tarSpec num isoType reacType msLvl mass =
        {
            BafSqlStep.TargetSpectrum   = tarSpec
            BafSqlStep.Number           = num
            BafSqlStep.IsolationType    = isoType
            BafSqlStep.ReactionType     = reacType
            BafSqlStep.MsLevel          = msLvl
            BafSqlStep.Mass             = mass
        }

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

        member this.GetBafSqlSpectrum() =
            CompiledQuery.Compile(fun db id -> db.GetTable<BafSqlSpectrum>().Where(fun x -> x.Id = id).SingleOrDefault())

        member this.GetBafSqlAcquisitionKey() =
            CompiledQuery.Compile(fun db id -> db.GetTable<BafSqlAcquisitionKey>().Where(fun x -> x.Id = id).SingleOrDefault())

        member this.GetBafSqlSteps() =
            CompiledQuery.Compile(fun db id -> db.GetTable<BafSqlStep>().Where(fun x -> x.TargetSpectrum = id).SingleOrDefault())

        member this.GetPerSpectrumVariables() =
            CompiledQuery.Compile(fun db id -> db.GetTable<BafSqlPerSpectrumVariable>().Where(fun x -> 
                x.Spectrum = id && x.Variable <> System.Nullable() && x.Value <> System.Nullable()).SingleOrDefault())
