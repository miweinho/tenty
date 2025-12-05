module Tests.Model.Validation.ValidationClientData

open System
open System.Text.Json
open Xunit
open backend_tenty.Service.GeoJsonService
open backend_tenty.Model.Validation.ValidateClientData
open backend_tenty.Model.Types

let private pointFeature lon lat =
    let coords = JsonSerializer.SerializeToElement([| lon; lat |])
    let props = JsonSerializer.SerializeToElement(Map.empty<string, string>)
    { ``type`` = "Feature"
      geometry = { ``type`` = "Point"; coordinates = coords }
      properties = props }

let private polygonFeature (points: (float * float) list) =
    let ring =
        points
        |> List.map (fun (lon, lat) -> [| lon; lat |])
        |> List.toArray

    let coords =
        [| ring |]
        |> JsonSerializer.SerializeToElement

    let props = JsonSerializer.SerializeToElement(Map.empty<string, string>)

    { ``type`` = "Feature"
      geometry = { ``type`` = "Polygon"; coordinates = coords }
      properties = props }

[<Fact>]
let ``validateName accepts strings within 50 chars`` () =
    let name = String.replicate 10 "camp"
    Assert.Equal(Ok name, validateName name)

[<Theory>]
[<InlineData("")>]
[<InlineData(null:string)>]
[<InlineData("This name is definitely more than fifty characters long for validation")>]
let ``validateName rejects empty or oversized strings`` (name: string) =
    match validateName name with
    | Ok _ -> failwithf "Expected Error for name '%s'" name
    | Error msg -> Assert.Contains("No valid name", msg)

[<Fact>]
let ``validateGeoJsonFeatureLocation accepts swiss point`` () =
    let location = pointFeature 8.5 46.8

    match validateGeoJsonFeatureLocation location with
    | Ok _ -> ()
    | Error msg -> failwithf "Expected Ok but got Error: %s" msg

[<Fact>]
let ``validateGeoJsonFeatureLocation accepts swiss polygon`` () =
    let polygon =
        polygonFeature
            [ (8.4, 46.8)
              (8.5, 46.9)
              (8.6, 46.8)
              (8.4, 46.8) ]

    match validateGeoJsonFeatureLocation polygon with
    | Ok _ -> ()
    | Error msg -> failwithf "Expected Ok but got Error: %s" msg

[<Fact>]
let ``validateGeoJsonFeatureLocation rejects point outside switzerland`` () =
    let outside = pointFeature 12.0 52.0

    match validateGeoJsonFeatureLocation outside with
    | Ok _ -> failwith "Expected Error for point outside Switzerland"
    | Error msg -> Assert.Contains("outside of Switzerland", msg)

[<Fact>]
let ``validateGeoJsonFeatureLocation rejects polygon with outside vertex`` () =
    let polygon =
        polygonFeature
            [ (8.4, 46.8)
              (11.0, 46.9)
              (8.6, 46.8)
              (8.4, 46.8) ]

    match validateGeoJsonFeatureLocation polygon with
    | Ok _ -> failwith "Expected Error for polygon outside Switzerland"
    | Error msg -> Assert.Contains("outside of Switzerland", msg)

[<Fact>]
let ``validateGeoJsonFeatureLocation rejects swapped coordinate order`` () =
    let swapped = pointFeature 46.8 8.5

    match validateGeoJsonFeatureLocation swapped with
    | Ok _ -> failwith "Expected Error for swapped lon/lat order"
    | Error msg -> Assert.Contains("outside of Switzerland", msg)

[<Fact>]
let ``validateGeoJsonFeatureLocation reports malformed coordinates`` () =
    let malformedCoords = JsonSerializer.SerializeToElement([| 8.5; 46.8; 1.0 |])
    let props = JsonSerializer.SerializeToElement(Map.empty<string, string>)

    let malformedPoint =
        { ``type`` = "Feature"
          geometry =
            { ``type`` = "Point"
              coordinates = malformedCoords }
          properties = props }

    match validateGeoJsonFeatureLocation malformedPoint with
    | Ok _ -> failwith "Expected Error for malformed coordinate array"
    | Error msg -> Assert.Contains("Invalid Point coordinates", msg)

[<Fact>]
let ``validateParentCamp finds matching id`` () =
    let camps = [ { Id = 1; OwnerId = 2; CampName = "Parent"; Location = "loc"; IsSubCamp = false; ParentId = None; CreatedAt = None; UpdatedAt = None } ]

    match validateParentCamp camps 1 with
    | Ok camp -> Assert.Equal(1, camp.Id)
    | Error msg -> failwithf "Expected Ok but got Error: %s" msg

[<Fact>]
let ``validateParentCamp returns error when id missing`` () =
    match validateParentCamp [] 99 with
    | Ok camp -> failwithf "Expected Error but got camp %d" camp.Id
    | Error msg -> Assert.Equal("No parent Camp found.", msg)

[<Fact>]
let ``validateDateString parses valid ISO date`` () =
    match validateDateString "2024-02-01T12:00:00+00:00" with
    | Ok dt -> Assert.Equal(2024, dt.Year)
    | Error msg -> failwithf "Expected Ok but got Error: %s" msg

[<Theory>]
[<InlineData("invalid-date")>]
[<InlineData("2024-13-01")>]
let ``validateDateString returns error for invalid inputs`` (value: string) =
    match validateDateString value with
    | Ok dt -> failwithf "Expected Error but got %O" dt
    | Error msg -> Assert.StartsWith("Error while parsing dateString", msg)
