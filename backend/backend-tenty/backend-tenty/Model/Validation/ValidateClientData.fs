module backend_tenty.Model.Validation.ValidateClientData

/// Validation helpers for incoming client payloads to ensure geographic and textual constraints are respected.

open System
open System.Text.Json
open System.Drawing
open backend_tenty.Service.GeoJsonService
open backend_tenty.Model.Types
open backend_tenty.Service.ResultExt

/// Validates that a provided name fits the String50 constraints used across the application.
let validateName(campName: string): Result<string, string> =
    match String50.create campName with
    | None -> Error "No valid name: "
    | Some n -> Ok campName

/// Checks whether a position is inside the Swiss bounding box used by the project.
let private posInSwitzerland (p: Position) : bool =
    p.lon > 5.51 && p.lon < 10.76 && p.lat > 45.58 && p.lat < 47.80

/// Ensures a geometry stays within Switzerland and returns a descriptive error otherwise.
let private geometryInSwitzerland (geometry: Geometry ) =
    let rec checkRing (coords: Position list) =
        match coords with
        | [] -> Ok ()
        | c :: cs ->
            if posInSwitzerland c then checkRing cs
            else Error $"Point is outside of Switzerland: {c.lat}, {c.lon}"

    let rec checkRings (rings: Position list list) =
        match rings with
        | [] -> Ok ()
        | ring :: rest ->
            match checkRing ring with
            | Ok () -> checkRings rest
            | Error e -> Error e

    match geometry with
    | Point p ->
        if posInSwitzerland p then Ok geometry
        else Error $"Point is outside of Switzerland: {p.lat}, {p.lon}"

    | LineString points ->
        match checkRing points with
        | Ok () -> Ok geometry
        | Error e -> Error e

    | Polygon rings ->
        match checkRings rings with
        | Ok () -> Ok geometry
        | Error e -> Error e


/// Validates a GeoJSON feature location by converting it to domain geometry and checking its bounds.
let validateGeoJsonFeatureLocation (location: GeoJsonFeatureDto) : Result<GeoJsonFeatureDto, string> =
    try
        let locFeature = featureFromDto location
        match geometryInSwitzerland locFeature.geometry with
        | Ok g -> Ok location
        | Error e -> Error e
    with
    | ex -> Error $"No or missing location: {ex.Message}"

/// Ensures the referenced parent camp exists in the provided collection.
let validateParentCamp (camps: DbCamp list) (parentId: int) : Result<DbCamp, string> =
    match camps |> List.tryFind (fun el -> el.Id = parentId) with
    | Some camp -> Ok camp
    | None -> Error "No parent Camp found."

/// Parses and validates a date string from the client.
let validateDateString (date: string) : Result<DateTimeOffset, string> =
    try
        let date = DateTimeOffset.Parse(date)
        Ok (date)
    with
    | ex -> Error $"Error while parsing dateString: {ex.Message}"

/// Verifies that a provided feature kind maps to a known domain kind.
let validateFeatureKind (kind: string) : Result<string, string> =
    match featureKindFromString kind with
    | Ok _ -> Ok kind
    | Error e -> Error e

/// Optionally validates string fields constrained to 50 characters.
let validateOptionalString50 (value: string option) (fieldName: string) : Result<string option, string> =
    match value with
    | None -> Ok None
    | Some v ->
        match String50.create v with
        | Some _ -> Ok (Some v)
        | None -> Error $"Invalid {fieldName}: must be between 1 and 50 characters"

/// Ensures a heading is within the 0–360 degree interval.
let validateHeading (heading: double option) : Result<double option, string> =
    match heading with
    | None -> Ok None
    | Some h when h >= 0 && h < 360 -> Ok heading
    | Some h -> Error $"Invalid heading {h}: must be between 0 (inclusive) and 360 (exclusive)"

/// Validates that an optional dimension is strictly positive.
let validatePositiveDimension (name: string) (value: double option) : Result<double option, string> =
    match value with
    | None -> Ok None
    | Some v when v > 0 -> Ok value
    | Some v -> Error $"Invalid {name} {v}: must be greater than 0"

/// Accepts HTML color strings or returns a readable error when the translation fails.
let validateFeatureColor (color: string option) : Result<string option, string> =
    match color with
    | None -> Ok None
    | Some c ->
        try
            ColorTranslator.FromHtml c |> ignore
            Ok (Some c)
        with ex ->
            Error $"Invalid color format: {ex.Message}"

/// Validates JSON content stored in the props field without altering it.
let validatePropsJson (props: string option) : Result<string option, string> =
    match props with
    | None -> Ok None
    | Some p when String.IsNullOrWhiteSpace p -> Ok None
    | Some p ->
        try
            JsonDocument.Parse(p) |> ignore
            Ok (Some p)
        with ex -> Error $"Invalid props json: {ex.Message}"

/// Ensures feature detail DTOs use valid titles.
let validateFeatureDetailDto (detail: FeatureDetailDto) : Result<FeatureDetailDto, string> =
    match String50.create detail.Title with
    | Some _ -> Ok detail
    | None -> Error "Invalid title for feature detail"

/// Validates a feature creation DTO including nested optional details and geometry.
let validateFeatureDto (feature: CreateFeatureDto) : Result<CreateFeatureDto, string> =
    result {
        do! validateFeatureKind feature.Kind |> Result.map ignore
        do! validateOptionalString50 feature.Name "feature name" |> Result.map ignore
        do! validateGeoJsonFeatureLocation feature.Geometry |> Result.map ignore
        do! validateHeading feature.HeadingDeg |> Result.map ignore
        do! validatePositiveDimension "length" feature.LengthM |> Result.map ignore
        do! validatePositiveDimension "width" feature.WidthM |> Result.map ignore
        do! validateFeatureColor feature.Color |> Result.map ignore
        do! validatePropsJson feature.Props |> Result.map ignore

        let! details =
            feature.Details
            |> Option.defaultValue []
            |> List.map validateFeatureDetailDto
            |> sequenceResultsList

        let normalizedDetails =
            match feature.Details with
            | Some _ -> Some details
            | None -> None

        return { feature with Details = normalizedDetails }
    }