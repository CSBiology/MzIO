namespace MzIO.MetaData.UO


open MzIO.Model.CvParam
open MzIO.MetaData.ParamEditExtension


module UO =

    type UO<'TPC when 'TPC :> DynamicObj>() =
    
        member this.X = ""
    
    type IHasUnit<'TPC when 'TPC :> DynamicObj> with

        ///Volumn units
        member this.UO_Liter() =
        
            this.SetUnit("UO:0000099")

        ///Concentration units
        member this.UO_GramPerLiter() =
        
            this.SetUnit("UO:0000175")

        ///Mass units
        member this.UO_Dalton() =
        
            this.SetUnit("UO:0000175")

        ///Energy units
        member this.UO_Electronvolt() =
        
            this.SetUnit("UO:0000266")

        ///Time units
        member this.UO_Second() =
        
            this.SetUnit("UO:0000010")

        ///Time units
        member this.UO_Minute() =
        
            this.SetUnit("UO:0000031")
