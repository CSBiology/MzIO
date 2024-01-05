module Tests

open Expecto

module NumpressTests =

    let private testMasses1 =
        [|354.3518131; 354.8040287; 355.0633232; 355.3465387; 356.0616964;
          357.0667788; 357.3215892; 358.0599902; 359.0519199; 360.8024357;
          363.8687143; 365.8189235; 369.1195284; 370.8484082; 371.0972668;
          371.3083209; 372.0935356; 372.3102925; 373.09114; 374.098229; 375.0876355;
          383.8309447; 385.8145542; 386.8497089; 390.8264846; 391.2791726;
          392.271558; 399.3376388; 401.844953; 402.0673915; 403.8350045; 405.8073382;
          406.8264657; 413.370581; 419.3052157; 421.8310856; 422.7950418;
          425.3545569; 428.889774; 429.0788462; 430.0801683; 430.878568; 431.0797416;
          431.2955451; 432.3286496; 432.8807284; 433.883549; 434.8699575;
          436.8696174; 440.7912881; 441.8032248; 444.7897183; 445.1155874;
          446.1146748; 446.8506236; 447.1030037; 447.340603; 448.1132366;
          449.1246095; 454.7674354; 455.7593013; 459.7947854; 461.829761;
          462.1376592; 463.1526378; 464.1233448; 472.7802264; 474.7794412;
          475.7591872; 477.7861724; 479.8205427; 481.8099104; 490.7663251;
          491.7935654; 492.7813501; 518.7540152; 519.1283314; 519.8901775;
          520.1399797; 521.1365824; 522.1309299; 523.126225; 536.1545568;
          537.1598682; 538.1498313; 539.7800423; 550.7396852; 557.7954749;
          568.7580689; 570.7627294; 571.8066809; 572.754111; 586.7738735;
          592.7834338; 593.1425184; 594.1416871; 595.1451228; 610.1664209;
          612.1836372; 628.7123315; 630.6929353; 667.1702329; 668.1572881; 669.1668706;
          741.2038739; 742.1906684; 743.1819479; 744.1892136; 801.1679261;
          815.2135926; 816.2083415; 817.2117265; 996.6839583; 998.6224541;
          998.9020722; 999.2394414; 999.6035088; 1001.229301; 1001.460395;
          1005.099193; 1006.577946; 1006.756183; 1007.340018; 1007.504948;
          1024.097647; 1025.792708; 1026.197571; 1026.42253; 1026.913026;
          1027.642236; 1028.164545; 1034.659434; 1035.49081; 1036.431028;
          1036.620932; 1037.276685; 1037.62952; 1038.964506; 1039.693453;
          1040.064816; 1040.409066; 1040.608394; 1041.265407; 1041.646116;
          1042.593659; 1050.535429; 1051.031652; 1051.505222; 1051.650957;
          1052.416233; 1052.521024; 1092.988728; 1093.401997; 1094.544697;
          1094.665505; 1095.571777; 1096.580737; 1101.287588; 1103.255249;
          1103.460509; 1104.183737; 1106.527687; 1106.733251; 1108.560809;
          1109.57573; 1109.921939; 1110.525594; 1111.045149; 1111.597602;
          1112.862207; 1114.56353; 1115.384192; 1115.82982; 1117.012339; 1123.659202;
          1124.120606; 1125.038988; 1125.698571; 1126.146255; 1127.112613;
          1127.650182; 1135.880551; 1136.155096; 1137.021552; 1137.623057;
          1139.234168; 1140.552335; 1141.097843; 1142.231968; 1177.572857;
          1227.036509; 1227.631843; 1227.887733; 1229.650163; 1230.014621;
          1230.581117; 1237.53745; 1238.525752; 1240.251283|]

    let private testMasses2 =
        [|354.8729419; 355.0581611; 355.3652022; 356.0512298; 357.0589541;
        365.8217733; 368.8499289; 370.8296561; 371.0974469; 371.3112079;
        372.1018462; 372.3158962; 373.0886137; 373.3192284; 374.0875532;
        376.8197128; 383.8421708; 384.8250888; 385.8451256; 386.8609878;
        391.2766385; 392.2801498; 393.2905167; 397.8826628; 399.3407151;
        401.8508614; 402.8565582; 403.8381111; 404.8095555; 405.8246039;
        411.8418258; 412.0328047; 413.3651855; 419.3170537; 419.8492546;
        421.8285441; 422.8213781; 423.8529661; 425.1608693; 425.375187;
        428.8843232; 429.0908497; 429.2043149; 430.071801; 430.8847713;
        431.0742833; 432.8811079; 433.0769036; 434.8820584; 440.7975911;
        441.7977301; 444.8019852; 445.1160077; 445.403465; 446.1180648;
        447.1034307; 447.33509; 448.1166399; 449.1161144; 449.8128347; 454.7678882;
        455.7657539; 457.7617612; 461.803075; 462.138137; 463.1440518; 463.8182938;
        464.1571116; 472.7654721; 479.8272337; 482.812858; 486.8111934;
        489.8060009; 490.78868; 492.7881684; 500.7639118; 504.1007795; 510.8014091;
        518.757888; 519.1354065; 520.1246438; 521.1308528; 522.1316158;
        535.8301497; 536.1650485; 537.1606076; 538.1636062; 550.7437691;
        552.7395737; 554.738988; 557.8029223; 568.7622715; 569.7507583;
        570.7568794; 571.7403811; 588.7768564; 593.1537219; 594.1529029;
        595.1494987; 610.1778335; 611.1738858; 612.1707505; 630.7081248; 667.1823346;
        668.1621411; 669.1753631; 670.1784455; 727.1637017; 741.1862602;
        743.187288; 745.221684; 815.2113781; 816.2061286; 904.0744268; 922.6860691;
        923.0615162; 923.5864168; 926.2772714; 963.2614937; 964.6350052;
        964.9141875; 1175.480666; 1177.041266; 1177.508684; 1178.038859;
        1178.178652; 1178.564331; 1179.340701; 1181.420317; 1182.492203;
        1183.066973; 1183.574238; 1184.598763; 1185.135366; 1194.009277;
        1194.494613; 1195.281068; 1195.552989; 1197.058825; 1197.685726;
        1198.375993; 1198.512126|]

    let private testMasses3 =
        [|120.0802488; 130.0621784; 173.1347074; 197.131058; 244.1699318;
        310.1891582; 363.1899845; 387.2211462; 429.0699926; 430.275327;
        449.2248044; 511.3396848; 542.8016093; 600.3729056; 637.4318806;
        714.4811173; 720.3800509; 736.2697555; 809.4668384; 988.5456103|]

    let private encTestMasses1 =
        NumpressHelper.NumpressEncodingHelpers.encodeLin testMasses1

    let private encTestMasses2 =
        NumpressHelper.NumpressEncodingHelpers.encodeLin testMasses2

    let private encTestMasses3 =
        NumpressHelper.NumpressEncodingHelpers.encodeLin testMasses3

    let private decTestMasses1 =
        NumpressHelper.NumpressDecodingHelpers.decodeLin
            (encTestMasses1.Bytes, encTestMasses1.NumberEncodedBytes, encTestMasses1.OriginalDataLength)

    let private decTestMasses2 =
        NumpressHelper.NumpressDecodingHelpers.decodeLin
            (encTestMasses2.Bytes, encTestMasses2.NumberEncodedBytes, encTestMasses2.OriginalDataLength)

    let private decTestMasses3 =
        NumpressHelper.NumpressDecodingHelpers.decodeLin
            (encTestMasses3.Bytes, encTestMasses3.NumberEncodedBytes, encTestMasses3.OriginalDataLength)

    let private equalWithinRange (n1: float) (n2: float) accuracy =
        let diff = abs (n1 - n2)
        diff < accuracy
    
    [<Tests>]
    let testNumpressEncodeDecodeLin =
        testList "Numpress EncodeLin and DecodeLin"[
            testCase "Equality Testmasses 1" <| fun () ->
                let testEquality =
                    Array.map2 (fun x y ->
                        equalWithinRange x y 0.0000001
                    ) testMasses1 decTestMasses1
                    |> Array.contains false
                Expect.isFalse testEquality "Difference between decoded and original mass1 is greather than 0.0000001"

            testCase "Equality Testmasses 2" <| fun () ->
                let testEquality =
                    Array.map2 (fun x y ->
                        equalWithinRange x y 0.0000001
                    ) testMasses2 decTestMasses2
                    |> Array.contains false
                Expect.isFalse testEquality "Difference between decoded and original mass2 is greather than 0.0000001"
        
            testCase "Equality Testmasses 3" <| fun () ->
                let testEquality =
                    Array.map2 (fun x y ->
                        equalWithinRange x y 0.0000001
                    ) testMasses3 decTestMasses3
                    |> Array.contains false
                Expect.isFalse testEquality "Difference between decoded and original mass3 is greather than 0.0000001"
        ]