namespace MSNumpressFSharp


open System


module internal helpers =

    /// <summary>
    /// This encoding works on a 4 byte integer, by truncating initial zeros or ones.
    /// "x" is the int to be encoded.
    /// "res" is the byte array were halfbytes are stored.
    /// "resOffset" is the position in res were halfbytes are written.
    /// The function returns the number of resulting halfbytes
    /// </summary>
    /// <remarks>
    /// If the initial (most significant) half byte is 0x0 or 0xf, the number of such 
    /// halfbytes starting from the most significant is stored in a halfbyte. This initial 
    /// count is then followed by the rest of the ints halfbytes, in little-endian order. 
    /// A count halfbyte c of
    /// 
    /// 		0 &lt;= c &lt;= 8 		is interpreted as an initial c 		0x0 halfbytes 
    /// 		9 &lt;= c &lt;= 15		is interpreted as an initial (c-8) 	0xf halfbytes
    /// 
    /// Ex:
    /// 	int		c		rest
    /// 	0 	=> 	0x8
    /// 	-1	=>	0xf		0xf
    /// 	23	=>	0x6 	0x7	0x1
    /// 
    /// @x			the int to be encoded
    ///	@res		the byte array were halfbytes are stored
    ///	@resOffset	position in res were halfbytes are written
    ///	@return		the number of resulting halfbytes
    /// </remarks>
    
    let encodeInt ((x: int64), (res: byte[]), (resOffset: int)) =
        let mask: int64 = 0xf0000000L
        let init: int64 = x &&& mask
        match init with 
        | 0L ->  
                let rec loop i= 
                        match i with
                        | value when value >= 0 && value < 8 ->
                                let m = mask >>> (4 * i)
                                if (x &&& m) <> 0L then i
                                else loop (i + 1)
                        | value when value = 8 -> 8
                        | _ -> failwith "Error in encodeInt; case: 0L" 
                let l = loop 0
                res.[resOffset] <- l |> byte
                for i = l to 7 do
                   res.[resOffset + 1 + i - l] <- byte (0xfL &&& (x >>> (4 * (i - l))))
                1 + 8 - l
        | mask ->
                let rec loop i= 
                    match i with
                    | value when value >= 0 && value < 8 ->
                            let m = mask >>> (4 * i)
                            if (x &&& m) <> m then i
                            else loop (i + 1)
                    | value when value = 8 -> 7
                    | _ -> failwith "Error in encodeInt; case: mask" 
                let l = loop 0
                res.[resOffset] <- byte ((l|> int) ||| 8)
                for i = l to 7 do
                   res.[resOffset + 1 + i - l] <- byte (0xfL &&& (x >>> (4 * (i - l))))
                1 + 8 - l

    /// Decodes ints from the half bytes in bytes. Lossless reverse of encodeInt, although not symmetrical in input arguments.
    
    let decodeInt() (bytes: byte[], pos, half)   =
                     let (head, pos2) = 
                        if      not half then
                                let h = (0xff &&& (bytes.[pos] |> int)) >>> 4
                                h, pos
                        else    let h = 0xf &&& (bytes.[pos] |> int)
                                let pos2 = pos + 1
                                h, pos2
                     let half2 = not half  
                     let (n, res) =
                        if      head <= 8 then
                                let n = head
                                n, 0
                        else    let n = head - 8
                               //Unchecked keyword was here. Is it needed in F#?
                                let mask = (0xF0000000 |> int)
                                let rec loop i resV =
                                     match i with
                                     | range when range >= 0 && range < n -> 
                                         let m = mask >>> (4 * i)
                                         let res = resV ||| m
                                         loop (i + 1) res
                                     | result when result = n ->
                                         resV
                                     | _ -> failwith "Error in intDecoder. Bytearray may be corrupted."
                                let res = loop 0 0
                                n, res 
                     if n = 8 then 0, pos2, half2
                     else   let rec loop i resV posV halfV=
                                match i with
                                | range when range < 8 ->
                                        let (hb, pos3) = 
                                            if not halfV then
                                                 let hb = (0xff &&& (bytes.[posV] |> int)) >>> 4
                                                 let pos = posV
                                                 hb, pos
                                            else let hb = 0xf &&& (bytes.[posV]|> int)
                                                 let pos = posV + 1
                                                 hb, pos
                                        let res = resV ||| (hb <<< ((i - n) * 4))
                                        let half = not halfV
                                        loop (i + 1) res pos3 half
                                | 8 -> resV, posV, halfV
                                | _ -> failwith "Error in intDecoder. Bytearray may be corrupted."
                            loop n res pos2 half2

    ///Encodes a given fixed point in the output byte array.
    ///Used in the function encodeLinear.
    
    let encodeFixedPoint ((fixedPoint: double), (result: byte[])) =
        let (fp: int64) = BitConverter.DoubleToInt64Bits(fixedPoint)
        for i = 0 to 7 do
            result.[7 - i] <- byte ((fp >>> (8 * i)) &&& 0xffL)
    
    
    ///Decodes the fixed Point which was encoded in the output byte array from the function encodeLinear.
    ///Used in the function decodeLinear.
    
    let decodeFixedPoint (data: byte[]) =
        let rec loop i fpv =
            match i with
            | value when value >= 0 && value < 8 -> 
                    let fp = fpv ||| ((0xffL &&& (data.[7 - i]|> int64)) <<< (8 * i))
                    loop (i + 1) fp
            | value when value = 8 -> 
                    BitConverter.Int64BitsToDouble(fpv)
            | _ ->  failwith "Error in decodeFixedPoint"
        loop 0 0L

module Encode = 
         
    ///Determines the optimal fixed point for the function encodeLinear depending on the data to encode.
    
    let optimalLinearFixedPoint ((data: double[]), (dataSize:int)) =
        match dataSize with
        | value when value = 0 -> 0.
        | value when value = 1 -> 
                Math.Floor((0xFFFFFFFFL|> double) / data.[0])
        | value when value > 1 -> 
                let rec loop i (maxDoubleV: double) =
                    match i with
                    | value when value >= 2 && value < dataSize ->  
                            let extrapol = data.[i - 1] + (data.[i - 1] - data.[i - 2])
                            let diff = data.[i] - extrapol
                            let maxDouble = Math.Max(maxDoubleV, Math.Ceiling(Math.Abs(diff) + 1.))
                            loop (i + 1) maxDouble
                    | value when value = dataSize ->
                            Math.Floor((0x7FFFFFFFL|> double) / maxDoubleV)
                    | _ ->  failwith "Error in optimalLinearFixedPoint; case: dataSize > 1"
                loop 2 (Math.Max(data.[0], data.[1]))
        | _ -> failwith "Error in optimalLinearFixedPoint. dataSize has to be the length of the inputarray."
    
    ///Determines the optimal fixed point for the function encodeSlof depending on the data to encode.
    
    let optimalSlofFixedPoint ((data: double[]), (dataSize: int)) =
        if dataSize = 0 then 0.
        else
            let rec loop i (maxDoubleV: double) =
                match i with
                | value when value < dataSize ->
                        let x = Math.Log (data.[i] + 1.)
                        let maxDouble = Math.Max (maxDoubleV, x)
                        loop (i + 1) maxDouble
                | value when value = dataSize ->
                        Math.Floor ((0xFFFF |> double) / maxDoubleV)
                | _ -> failwith "Error in optimalSlofFixedPoint. Your dataSize may be wrong."
            loop 0 1.
        
    /// <summary>
    /// Encodes data using MS Numpress linear prediction compression.
    /// "data" is the array of doubles to be encoded.
    /// "dataSize" is the number of doubles in data to be encoded.
    /// "result" is the array where resulting bytes should be stored.
    /// "fixedPoint" is the the scaling factor used for getting the fixed point representation.
    /// This is stored in the binary and automatically extracted on decoding.
    /// Returns the number of encoded bytes.
    /// </summary>
    /// <remarks>
    /// Encodes the doubles in data by first using a 
    ///   - lossy conversion to a 4 byte 5 decimal fixed point repressentation
    ///   - storing the residuals from a linear prediction after first two values
    ///   - encoding by encodeInt (see above) 
    /// 
    /// The resulting binary is maximally 8 + dataSize * 5 bytes, but much less if the 
    /// data is reasonably smooth on the first order.
    ///
    /// This encoding is suitable for typical m/z or retention time binary arrays. 
    /// On a test set, the encoding was empirically show to be accurate to at least 0.002 ppm.
    /// </remarks>
        
    let encodeLinear ((data: double[]), (dataSize: int), (result: byte[]), (fixedPoint: double)) =
        let ints = Array.init 3 (fun x -> int64 0)
        let halfBytes = Array.init 10 (fun x -> byte(0))
        helpers.encodeFixedPoint (fixedPoint, result)
        match dataSize with
        | value when value = 0 -> 8
        | value when value = 1 ->  
                ints.[1] <- int64 (data.[0] * fixedPoint + 0.5)
                for i = 0 to 3 do
                    result.[8 + i] <- byte ((ints.[1] >>> (i * 8)) &&& 0xffL)
                12
        | value when value > 1 ->   
                ints.[1] <- int64 (data.[0] * fixedPoint + 0.5)
                for i = 0 to 3 do
                    result.[8 + i] <- byte ((ints.[1] >>> (i * 8)) &&& 0xffL)
                ints.[2] <- int64 (data.[1] * fixedPoint + 0.5)
                for i = 0 to 3 do
                    result.[12 + i] <- byte ((ints.[2] >>> (i * 8)) &&& 0xffL)
                let rec loop i halfByteCountV riV =
                    match i with
                    | value when value < dataSize ->
                        ints.[0] <- ints.[1]
                        ints.[1] <- ints.[2]
                        ints.[2] <- int64 (data.[i] * fixedPoint + 0.5)
                        let extrapol = ints.[1] + (ints.[1] - ints.[0])
                        let diff = ints.[2] - extrapol
                        let halfByteCount = halfByteCountV + (helpers.encodeInt (diff, halfBytes, halfByteCountV))                
                        let rec loop2 hbi ri =
                            match hbi with
                            | value when value < halfByteCount ->
                                    result.[ri] <- byte (((halfBytes.[hbi - 1]|> int) <<< 4) ||| ((halfBytes.[hbi]|> int) &&& 0xf))
                                    loop2 (hbi + 2) (ri + 1)
                            | value when value >= halfByteCount -> ri
                            | _ -> failwith "Error in encodeLinear; case: dataSize > 1 & i < dataSize"
                        let riOut = loop2 1 riV
                        if   halfByteCount % 2 <> 0 then 
                             halfBytes.[0] <- halfBytes.[halfByteCount - 1]
                             loop (i + 1) 1 riOut
                        else loop (i + 1) 0 riOut
                    | result when result = dataSize -> halfByteCountV, riV
                    | _ -> failwith "Error in encodeLinear; case: dataSize"
                let (halfByteCountOut, riOut) = loop 2 0 16
                if   halfByteCountOut = 1 then
                     result.[riOut] <- byte ((halfBytes.[0]|> int) <<< 4)
                     riOut + 1
                else riOut
                    
        | _ -> failwith "Error in encodeLinear. dataSize has to be the length of the inputarray."    
               
    /// <summary>
    /// Encodes ion counts by simply rounding to the nearest 4 byte integer, and compressing each integer with encodeInt.
    /// "data" is the array of doubles to be encoded.
    /// "dataSize" is the number of doubles in data to be encoded.
    /// "result" is the array where resulting bytes should be stored.
    /// Returns the number of encoded bytes.
    /// </summary>
    /// <remarks>
    /// The handleable range is therefore 0 -> 4294967294.
    /// The resulting binary is maximally dataSize * 5 bytes, but much less if the 
    /// data is close to 0 on average.
    /// </remarks>
    
    let encodePic ((data: double[]), (dataSize: int), (result: byte[])) =
        let halfBytes = Array.init 10 (fun x -> byte (0))
        let rec loop i halfByteCountV riV=
            match i with
            | value when value < dataSize ->
                    let count = int64 (data.[i] + 0.5)
                    let halfByteCount = halfByteCountV + helpers.encodeInt (count, halfBytes, halfByteCountV)
                    let rec loop2 hbi ri =
                        match hbi with
                        | value when value < halfByteCount ->
                                result.[ri] <- byte (((halfBytes.[hbi - 1]|> int) <<< 4) ||| ((halfBytes.[hbi]|> int) &&& 0xf))
                                loop2 (hbi + 2) (ri + 1)
                        | value when value >= halfByteCount -> ri
                        | _ -> failwith "Error in encodePic; case: i < dataSize"
                    let riOut = loop2 1 riV
                    if (halfByteCount % 2) <> 0 then
                            halfBytes.[0] <- halfBytes.[halfByteCount - 1]
                            loop (i + 1) 1 riOut
                    else    loop (i + 1) 0 riOut
            | value when value = dataSize -> halfByteCountV, riV
            | _ -> failwith "Error in encodePic; dataSize has to be the length of the inputarray."
        let (halfByteCountOut, riOut) = loop 0 0 0
        if halfByteCountOut = 1 then
                result.[riOut] <- byte ((halfBytes.[0]|> int) <<< 4)
                riOut + 1
        else    riOut
    
    /// <summary>
    /// Encodes ion counts by taking the natural logarithm, and storing a fixed point representation of this.
    /// "data" is the array of doubles to be encoded.
    /// "dataSize" is the number of doubles in data to be encoded.
    /// "result" is the array where resulting bytes should be stored.
    /// "fixedPoint" is the the scaling factor used for getting the fixed point repr. This is stored in the binary and automatically extracted on decoding.
    /// Returns the number of encoded bytes.
    /// </summary>
    /// <remarks>
    /// Encodes ion counts by taking the natural logarithm, and storing a
    /// fixed point representation of this. This is calculated as
    /// 
    /// unsigned short fp = log(d+1) * fixedPoint + 0.5
    ///
    /// the result vector is exactly |data| * 2 + 8 bytes long
    /// </remarks>
    
    let encodeSlof ((data: double[]), (dataSize: int), (result: byte[]), (fixedPoint: double)) =
        helpers.encodeFixedPoint (fixedPoint, result)
        let rec loop i riV =
            match i with
            | value when value < dataSize ->
                    let x = int (Math.Log (data.[i] + 1.) * fixedPoint + 0.5)
                    result.[riV] <- byte (0xff &&& x)
                    result.[riV + 1] <- byte (x >>> 8)
                    loop (i + 1) (riV + 2)
            | value when value = dataSize -> riV
            | _ -> failwith "Error in encodeSlof. dataSize may be wrong."
        loop 0 8
    
module Decode =

    /// <summary>
    /// Decodes data using MS Numpress linear prediction compression.
    /// "data" is the array of bytes to be decoded.
    /// "dataSize" is the number of bytes in data to be decoded.
    /// "result" is the array where the resulting doubles should be stored.
    /// Returns the number of decoded doubles.
    /// </summary>
    /// <remarks>
    /// Result vector guaranteed to be shorter or equal to (|data| - 8) * 2
    ///
    /// Note that this method may throw a ArrayIndexOutOfBoundsException if it deems the input data to 
    /// be corrupt, i.e. that the last encoded int does not use the last byte in the data. In addition 
    /// the last encoded int need to use either the last halfbyte, or the second last followed by a 
    /// 0x0 halfbyte. 
    /// </remarks>
    
    let decodeLinear ((data: byte[]), (dataSize: int), (result: double[])) =
        let ints = Array.init 3 (fun x -> int64 0)
        match dataSize with
        | value when value = 8 -> 0
        | value when value < 8 -> -1
        | value when value < 12 ->
            let fixedPoint = helpers.decodeFixedPoint (data)
            -1
        | _ ->
            let fixedPoint = helpers.decodeFixedPoint (data)
            ints.[1] <- 0L
            for i = 0 to 3 do
                ints.[1] <- ints.[1] ||| ((0xFFL &&& (data.[8 + i]|> int64)) <<< (i * 8))
            result.[0] <- (ints.[1]|> double) / fixedPoint
            match dataSize with
            | value when value = 12 -> 1
            | value when value < 16 -> -1
            | _ ->
                ints.[2] <- 0L
                for i = 0 to 3 do
                    ints.[2] <- ints.[2] ||| ((0xFFL &&& (data.[12 + i]|> int64)) <<< (i * 8))
                result.[1] <- (ints.[2]|> double) / fixedPoint
                let rec loop riV pos half=
                    let (res2, pos2, half2) = helpers.decodeInt() (data, pos, half)
                    // value < dataSize and pos2 = (dataSize -1) had to be replaced by value <= dataSize and pos2 = dataSize
                    match pos2 with
                    | value when value <= dataSize ->
                        if (pos2 = (dataSize)) && half2 && ((data.[pos2]|> int &&& 0xf) <> 0x8) then
                             riV
                        else ints.[0] <- ints.[1]
                             ints.[1] <- ints.[2]
                             ints.[2] <- res2 |> int64
                             let extrapol = ints.[1] + (ints.[1] - ints.[0])
                             let y = extrapol + ints.[2]
                             result.[riV] <- (y|> double) / fixedPoint
                             ints.[2] <- y
                             loop (riV + 1) pos2 half2
                    | _ -> riV
                loop 2 16 false

    /// <summary>
    /// Decodes data encoded by encodePic.
    /// "data" is the array of bytes to be decoded (need memorycont. repr.).
    /// "dataSize" is the number of bytes in data to be decoded.
    /// "result" is the array where resulting doubles should be stored.
    /// Returns the number of decoded doubles.
    /// </summary>
    /// <remarks>
    /// Result vector guaranteed to be shorter of equal to |data| * 2
    ///
    /// Note that this method may throw a ArrayIndexOutOfBoundsException if it deems the input data to 
    /// be corrupt, i.e. that the last encoded int does not use the last byte in the data. In addition 
    /// the last encoded int needs to use either the last halfbyte, or the second last followed by a 
    /// 0x0 halfbyte. 
    /// </remarks>
    
    let decodePic ((data: byte[]), (dataSize: int), (result: double[])) =
        let rec loop ri (pos: int) (half:bool) =
            let (res2, pos2, half2) = helpers.decodeInt() (data, pos, half)
            match pos2 with
            // value < dataSize and pos2 = (dataSize -1) had to be replaced by value <= dataSize and pos2 = dataSize
            | value when value <= dataSize ->
                    if      (pos2 = (dataSize)) && half2 && (((data.[pos2]|> int) &&& 0xf) <> 0x8) then
                            ri
                    else    result.[ri] <- res2 |> double                    
                            loop (ri + 1) pos2 half2
            | _ -> ri
        loop 0 0 false

    /// <summary>
    /// Decodes data encoded by encodeSlof.
    /// "data" is the array of bytes to be decoded (need memorycont. repr.).
    /// "dataSize" is the number of bytes in data to be decoded.
    /// "result" is the array where resulting doubles should be stored.
    /// Returns the number of decoded doubles.
    /// </summary>
    /// <remarks>
    /// The result vector will be exactly (|data| - 8) / 2 doubles.
    /// returns the number of doubles read, or -1 if there is a problem decoding.
    /// </remarks>
    
    let decodeSlof ((data: byte[]), (dataSize: int), (result: double[])) =
        match dataSize with
        | value when value < 8 -> -1
        | _ ->
                let fixedPoint = helpers.decodeFixedPoint data
                match dataSize with
                | value when (value % 2) <> 0 -> -1
                | _ ->
                        let rec loop i riV =
                            match i with
                            | value when value < dataSize ->
                                    let x = (0xff &&& (data.[i] |> int)) ||| ((0xff &&& (data.[i + 1] |> int)) <<< 8)
                                    result.[riV] <- (Math.Exp (((0xffff &&& x) |> float) / fixedPoint) - 1.)
                                    loop (i + 2) (riV + 1)
                            | value when value >= dataSize -> riV
                            | _ -> failwith "Error in decodeSlof. dataSize may be wrong."
                        loop 8 0
