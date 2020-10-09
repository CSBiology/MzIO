namespace MzIO.Tests

open Expecto

module RunTests =

    [<EntryPoint>]
    let main args =

        Tests.runTestsWithArgs defaultConfig args Tests.testSimpleTests |> ignore
        Tests.runTestsWithArgs defaultConfig args NumpressTests.testNumpressEncodeDecodeLin |> ignore

        0

