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
let ``validateGeoJsonFeatureLocation rejects point outside switzerland`` () =
    let outside = pointFeature 12.0 52.0

    match validateGeoJsonFeatureLocation outside with
    | Ok _ -> failwith "Expected Error for point outside Switzerland"
    | Error msg -> Assert.Contains("outside of Switzerland", msg)

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
