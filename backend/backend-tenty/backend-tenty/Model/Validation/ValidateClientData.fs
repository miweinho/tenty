module backend_tenty.Model.Validation.ValidateClientData

//TODO: Add custom Error messages for Validation

open System
open System.Text.Json
open System.Drawing
open backend_tenty.Service.GeoJsonService
open backend_tenty.Model.Types
open backend_tenty.Service.ResultExt

///Used to validate names across tenty. Names can only be less than 50 characters and use normal characters.
let validateName(campName: string): Result<string, string> =
    match String50.create campName with
    | None -> Error "No valid name: "
    | Some n -> Ok campName

// Länge 5.51 - 10.776
// Breite: 45.58 - 47.802
let private posInSwitzerland (p: Position) : bool =
    p.lon > 5.51 && p.lon < 10.76 && p.lat > 45.58 && p.lat < 47.80

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
    | Polygon p ->
        match checkRings p with
        | Ok () -> Ok geometry
        | Error e -> Error e
   
let validateGeoJsonFeatureLocation (location: GeoJsonFeatureDto) : Result<GeoJsonFeatureDto, string> =
    try
        let locFeature = featureFromDto location
        match geometryInSwitzerland locFeature.geometry with
        | Ok g -> Ok location
        | Error e -> Error e
    with
    | ex -> Error $"No or missing location: {ex.Message}"
    
let validateParentCamp (camps: DbCamp list) (parentId: int) : Result<DbCamp, string> =
    match camps |> List.tryFind (fun el -> el.Id = parentId) with
    | Some camp -> Ok camp
    | None -> Error "No parent Camp found."
    
let validateDateString (date: string) : Result<DateTimeOffset, string> =
    try
        let date = DateTimeOffset.Parse(date)
        Ok (date)
    with
    | ex -> Error $"Error while parsing dateString: {ex.Message}"
    
let validateFeatureKind (kind: string) : Result<string, string> =
    match featureKindFromString kind with
    | Ok _ -> Ok kind
    | Error e -> Error e

let validateOptionalString50 (value: string option) (fieldName: string) : Result<string option, string> =
    match value with
    | None -> Ok None
    | Some v ->
        match String50.create v with
        | Some _ -> Ok (Some v)
        | None -> Error $"Invalid {fieldName}: must be between 1 and 50 characters"

let validateHeading (heading: double option) : Result<double option, string> =
    match heading with
    | None -> Ok None
    | Some h when h >= 0 && h < 360 -> Ok heading
    | Some h -> Error $"Invalid heading {h}: must be between 0 (inclusive) and 360 (exclusive)"

let validatePositiveDimension (name: string) (value: double option) : Result<double option, string> =
    match value with
    | None -> Ok None
    | Some v when v > 0 -> Ok value
    | Some v -> Error $"Invalid {name} {v}: must be greater than 0"

let validateFeatureColor (color: string option) : Result<string option, string> =
    match color with
    | None -> Ok None
    | Some c ->
        try
            ColorTranslator.FromHtml c |> ignore
            Ok (Some c)
        with ex ->
            Error $"Invalid color format: {ex.Message}"

let validatePropsJson (props: string option) : Result<string option, string> =
    match props with
    | None -> Ok None
    | Some p when String.IsNullOrWhiteSpace p -> Ok None
    | Some p ->
        try
            JsonDocument.Parse(p) |> ignore
            Ok (Some p)
        with ex -> Error $"Invalid props json: {ex.Message}"

let validateFeatureDetailDto (detail: FeatureDetailDto) : Result<FeatureDetailDto, string> =
    match String50.create detail.Title with
    | Some _ -> Ok detail
    | None -> Error "Invalid title for feature detail"

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