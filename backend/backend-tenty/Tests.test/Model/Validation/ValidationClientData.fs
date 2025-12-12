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
    let camps: DbCamp list = [ { Id = 1; OwnerId = 2; CampName = "Parent"; Location = "loc"; IsSubCamp = false; ParentId = None; CreatedAt = None; UpdatedAt = None } ]

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

[<Fact>]
let ``validateFeatureKind accepts known kinds`` () =
    match validateFeatureKind "tent" with
    | Ok k -> Assert.Equal("tent", k)
    | Error msg -> failwithf "Expected Ok but got Error: %s" msg

[<Fact>]
let ``validateFeatureKind rejects unknown kind`` () =
    match validateFeatureKind "unknownkind" with
    | Ok _ -> failwith "Expected Error for unknown kind"
    | Error msg -> Assert.Contains("Unknown feature kind", msg)

[<Fact>]
let ``validateOptionalString50 accepts None and valid string`` () =
    match validateOptionalString50 None "field" with
    | Ok None -> ()
    | _ -> failwith "Expected Ok None"

    match validateOptionalString50 (Some "valid") "field" with
    | Ok (Some v) -> Assert.Equal("valid", v)
    | Error msg -> failwithf "Expected Ok but got Error: %s" msg

[<Fact>]
let ``validateOptionalString50 rejects invalid string`` () =
    match validateOptionalString50 (Some "") "feature name" with
    | Ok _ -> failwith "Expected Error for empty string"
    | Error msg -> Assert.Contains("must be between 1 and 50 characters", msg)

[<Fact>]
let ``validateHeading accepts valid headings and rejects invalid`` () =
    match validateHeading None with
    | Ok None -> ()
    | _ -> failwith "Expected Ok None"

    match validateHeading (Some 90.0) with
    | Ok (Some h) -> Assert.Equal(90.0, h)
    | Error msg -> failwithf "Expected Ok but got Error: %s" msg

    match validateHeading (Some 360.0) with
    | Ok _ -> failwith "Expected Error for heading 360"
    | Error msg -> Assert.Contains("must be between 0 (inclusive) and 360 (exclusive)", msg)

[<Fact>]
let ``validatePositiveDimension accepts positive and rejects nonpositive`` () =
    match validatePositiveDimension "length" None with
    | Ok None -> ()
    | _ -> failwith "Expected Ok None"

    match validatePositiveDimension "length" (Some 2.5) with
    | Ok (Some v) -> Assert.Equal(2.5, v)
    | Error msg -> failwithf "Expected Ok but got Error: %s" msg

    match validatePositiveDimension "length" (Some 0.0) with
    | Ok _ -> failwith "Expected Error for nonpositive dimension"
    | Error msg -> Assert.Contains("must be greater than 0", msg)

[<Fact>]
let ``validateFeatureColor accepts valid html color and rejects invalid`` () =
    match validateFeatureColor None with
    | Ok None -> ()
    | _ -> failwith "Expected Ok None"

    match validateFeatureColor (Some "#FF0000") with
    | Ok (Some c) -> Assert.Equal("#FF0000", c)
    | Error msg -> failwithf "Expected Ok but got Error: %s" msg

    match validateFeatureColor (Some "notacolor") with
    | Ok _ -> failwith "Expected Error for invalid color"
    | Error msg -> Assert.Contains("Invalid color format", msg)

[<Fact>]
let ``validatePropsJson accepts valid json and rejects invalid`` () =
    match validatePropsJson None with
    | Ok None -> ()
    | _ -> failwith "Expected Ok None"

    match validatePropsJson (Some "   ") with
    | Ok None -> ()
    | _ -> failwith "Expected Ok None for whitespace"

    match validatePropsJson (Some "{}") with
    | Ok (Some s) -> Assert.Equal("{}", s)
    | Error msg -> failwithf "Expected Ok but got Error: %s" msg

    match validatePropsJson (Some "notjson") with
    | Ok _ -> failwith "Expected Error for invalid json"
    | Error msg -> Assert.Contains("Invalid props json", msg)

[<Fact>]
let ``validateFeatureDetailDto accepts valid and rejects invalid titles`` () =
    let validDetail = { Id = 0; Title = "Ok"; Content = "c"; CreatedAt = None; UpdatedAt = None }
    match validateFeatureDetailDto validDetail with
    | Ok d -> Assert.Equal("Ok", d.Title)
    | Error msg -> failwithf "Expected Ok but got Error: %s" msg

    let longTitle = String.replicate 51 "a"
    let invalidDetail = { Id = 0; Title = longTitle; Content = "c"; CreatedAt = None; UpdatedAt = None }
    match validateFeatureDetailDto invalidDetail with
    | Ok _ -> failwith "Expected Error for too long title"
    | Error msg -> Assert.Contains("Invalid title for feature detail", msg)

[<Fact>]
let ``validateFeatureDto accepts valid feature and rejects bad details/kind`` () =
    let goodDetail = { Id = 0; Title = "Detail"; Content = "c"; CreatedAt = None; UpdatedAt = None }

    let goodFeature: CreateFeatureDto =
        { CampId = 1
          Kind = "tent"
          Name = Some "F"
          Geometry = pointFeature 8.5 46.8
          HeadingDeg = Some 0.0
          LengthM = Some 1.0
          WidthM = Some 1.0
          Color = Some "#00FF00"
          Icon = None
          Props = Some "{}"
          Details = Some [ goodDetail ] }

    match validateFeatureDto goodFeature with
    | Ok f -> Assert.True(f.Details.IsSome)
    | Error msg -> failwithf "Expected Ok but got Error: %s" msg

    // reject unknown kind
    let badKind = { goodFeature with Kind = "unknown" }
    match validateFeatureDto badKind with
    | Ok _ -> failwith "Expected Error for unknown kind"
    | Error msg -> Assert.Contains("Unknown feature kind", msg)

    // reject invalid detail
    let badDetail = { goodDetail with Title = String.replicate 51 "x" }
    let badDetailsFeature = { goodFeature with Details = Some [ badDetail ] }
    match validateFeatureDto badDetailsFeature with
    | Ok _ -> failwith "Expected Error for invalid detail"
    | Error msg -> Assert.Contains("Invalid title for feature detail", msg)
