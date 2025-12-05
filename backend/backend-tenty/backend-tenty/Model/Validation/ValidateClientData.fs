module backend_tenty.Model.Validation.ValidateClientData

//TODO: Add custom Error messages for Validation

open System
open System.Collections.Generic
open backend_tenty.Service.GeoJsonService
open backend_tenty.Model.Types
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
        System.Console.WriteLine("did featureFromDto before fail.")
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
    