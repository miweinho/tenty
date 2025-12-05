module Tests.Model.Types.SwissCoordinateTests

open Xunit
open backend_tenty.Model.Types

[<Fact>]
let ``create accepts LV95 range`` () =
    let x = 2600000m
    let y = 1200000m

    match SwissCoordinate.create x y with
    | Some coord -> Assert.Equal((x, y), SwissCoordinate.value coord)
    | None -> failwith "Expected Some for valid Swiss coordinates"

[<Fact>]
let ``create accepts boundary values`` () =
    let candidates =
        [ SwissCoordinate.create 2480000m 1070000m
          SwissCoordinate.create 2840000m 1300000m ]

    for result in candidates do
        match result with
        | Some _ -> ()
        | None -> failwith "Expected boundary coordinates to be valid"

[<Fact>]
let ``create rejects coordinates outside LV95 range`` () =
    let invalidCoordinates =
        [ (2400000m, 1200000m)
          (2600000m, 900000m)
          (2900000m, 1400000m) ]

    for (x, y) in invalidCoordinates do
        Assert.True(SwissCoordinate.create x y |> Option.isNone)
