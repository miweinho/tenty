module backend_tenty.Service.GeoJsonService

open System
open System.Collections.Generic
open System.Text.Json
open System.Text.Json.Serialization
open Giraffe

// -------------------------
// Domain types
// -------------------------

type Position = {
    lon: float
    lat: float
}



type Geometry =
    | Point of Position
    | Polygon of Position list list

type FeatureProps = {
    name      : string option
    coordType : string option
    extra     : Map<string, JsonElement>
}

type GeoJsonFeature = {
    geometry   : Geometry
    properties : FeatureProps
}

type GeoJsonFeatureCollection = {
    features : GeoJsonFeature list
}

// -------------------------
// DTOs (shape of GeoJSON)
// -------------------------

type GeoJsonGeometryDto = {
    [<JsonPropertyName("type")>]
    ``type``      : string

    [<JsonPropertyName("coordinates")>]
    coordinates   : JsonElement
}

type GeoJsonFeatureDto = {
    [<JsonPropertyName("type")>]
    ``type``      : string

    [<JsonPropertyName("geometry")>]
    geometry      : GeoJsonGeometryDto

    [<JsonPropertyName("properties")>]
    properties    : JsonElement
}

type GeoJsonFeatureCollectionDto = {
    [<JsonPropertyName("type")>]
    ``type``   : string

    [<JsonPropertyName("features")>]
    features   : GeoJsonFeatureDto array
}

// Optional: serializer options
let private jsonOptions =
    let o = JsonSerializerOptions()
    o.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
    o.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
    o

// -------------------------
// Domain -> DTO
// -------------------------

let private geometryToDto (g: Geometry) : GeoJsonGeometryDto =
    match g with
    | Point p ->
        let coords = JsonSerializer.SerializeToElement([| p.lon; p.lat |], jsonOptions)
        { ``type`` = "Point"
          coordinates = coords }

    | Polygon rings ->
        // [[ [lon,lat]; ... ]; ...]
        let coords =
            rings
            |> List.map (fun ring ->
                ring
                |> List.map (fun p -> [| p.lon; p.lat |])
                |> List.toArray)
            |> List.toArray
            |> fun arr -> JsonSerializer.SerializeToElement(arr, jsonOptions)

        { ``type`` = "Polygon"
          coordinates = coords }

let private propsToJsonElement (p: FeatureProps) : JsonElement =
    let dict = Dictionary<string, JsonElement>()

    match p.name with
    | Some n -> dict.Add("name", JsonSerializer.SerializeToElement(n, jsonOptions))
    | None -> ()

    match p.coordType with
    | Some ct -> dict.Add("coordType", JsonSerializer.SerializeToElement(ct, jsonOptions))
    | None -> ()

    for KeyValue(k, v) in p.extra do
        dict.[k] <- v

    JsonSerializer.SerializeToElement(dict, jsonOptions)

let private featureToDto (f: GeoJsonFeature) : GeoJsonFeatureDto =
    { ``type``   = "Feature"
      geometry   = geometryToDto f.geometry
      properties = propsToJsonElement f.properties }

let private collectionToDto (fc: GeoJsonFeatureCollection) : GeoJsonFeatureCollectionDto =
    { ``type`` = "FeatureCollection"
      features = fc.features |> List.map featureToDto |> List.toArray }

// -------------------------
// DTO -> Domain
// -------------------------

let private geometryFromDto (g: GeoJsonGeometryDto) : Geometry =
    System.Console.WriteLine($"geometryFromDto type: {g}")

    match g.``type`` with
    | "Point" ->
        let arr = g.coordinates.EnumerateArray() |> Seq.toArray
        if arr.Length <> 2 then
            failwithf "Invalid Point coordinates: expected [lon, lat], got %d elements" arr.Length

        let lon = arr.[0].GetDouble()
        let lat = arr.[1].GetDouble()
        Point { lon = lon; lat = lat }

    | "Polygon" ->
        // [ [ [lon,lat], ... ], ... ]
        let rings =
            g.coordinates.EnumerateArray()
            |> Seq.map (fun ring ->
                ring.EnumerateArray()
                |> Seq.map (fun coord ->
                    let a = coord.EnumerateArray() |> Seq.toArray
                    if a.Length <> 2 then
                        failwithf "Invalid polygon coordinate: expected [lon, lat], got %d elements" a.Length
                    { lon = a.[0].GetDouble()
                      lat = a.[1].GetDouble() })
                |> Seq.toList)
            |> Seq.toList

        Polygon rings

    | other ->
        failwithf "Unsupported GeoJSON geometry type: %s" other

let private propsFromJsonElement (propsJson: JsonElement) : FeatureProps =
    let mutable name : string option = None
    let mutable coordType : string option = None
    let mutable extra : Map<string, JsonElement> = Map.empty

    for p in propsJson.EnumerateObject() do
        match p.Name with
        | "name" ->
            if p.Value.ValueKind <> JsonValueKind.Null then
                name <- Some (p.Value.GetString())
        | "coordType" ->
            if p.Value.ValueKind <> JsonValueKind.Null then
                coordType <- Some (p.Value.GetString())
        | "crs" -> //Ignore CRS-Field
            ()
        | other ->
            extra <- extra.Add(other, p.Value.Clone())

    { name      = name
      coordType = coordType
      extra     = extra }
    
let private geometryToFeature (g: Geometry) : GeoJsonFeature =
    {
        geometry = g
        properties = {name = None; coordType = None; extra = Map.empty}
    }

let featureFromDto (f: GeoJsonFeatureDto) : GeoJsonFeature =
    { geometry   = geometryFromDto f.geometry
      properties = propsFromJsonElement f.properties }

let private featureCollectionFromDto (fc: GeoJsonFeatureCollectionDto) : GeoJsonFeatureCollection =
    { features = fc.features |> Array.toList |> List.map featureFromDto }

let private removeCrsFromJson (json: string) : string =
    // Entfernt: ,"crs":{...} oder "crs":{...},
    System.Text.RegularExpressions.Regex.Replace(
        json,
        @"""crs""\s*:\s*\{(?:[^{}]|(?:\{[^}]*\}))*\}\s*,?",
        ""
    ).Replace(",,", ",").Replace(",}", "}")

// -------------------------
// Public API you probably want
// -------------------------

/// 1. Parse GeoJSON from client -> domain model
let deserializeFeatureCollection (json: string) : GeoJsonFeatureCollection =
    let cleanJson = removeCrsFromJson json
    let dto =
        JsonSerializer.Deserialize<GeoJsonFeatureCollectionDto>(cleanJson, jsonOptions)
        |> Option.ofObj
        |> Option.defaultWith (fun () -> failwith "Invalid GeoJSON: cannot deserialize FeatureCollection")
    featureCollectionFromDto dto
    
let deserializeFeature (json: string) :GeoJsonFeature =
    let cleanJson = removeCrsFromJson json
    let dto =
        JsonSerializer.Deserialize<GeoJsonFeatureDto>(cleanJson, jsonOptions)
        |> Option.ofObj
        |> Option.defaultWith (fun () -> failwith "Invalid GeoJSON: cannot deserialize Feature")
    featureFromDto dto
    

    

/// 2. Serialize domain -> GeoJSON string (for DB or client)
let serializeFeatureCollection (fc: GeoJsonFeatureCollection) : string =
    let dto = collectionToDto fc
    JsonSerializer.Serialize(dto, jsonOptions)
   
let serializeFeature (f: GeoJsonFeature) : GeoJsonFeatureDto =
    featureToDto f

/// 3. Helper: single geometry <-> JSON (if you need it)
let geometryFromJson (json: string) : Geometry =
    let cleanJson = removeCrsFromJson json
    let dto =
        JsonSerializer.Deserialize<GeoJsonGeometryDto>(cleanJson, jsonOptions)
        |> Option.ofObj
        |> Option.defaultWith (fun () -> failwith "Invalid GeoJSON geometry")
    geometryFromDto dto

let geometryToJson (g: Geometry) : string =
    let dto = geometryToDto g
    JsonSerializer.Serialize(dto, jsonOptions)
    


//Used public functions to deserialize features from db and fronend:

let deserializeFeatureFromJson (json: string) : GeoJsonFeature =
    json
    |> geometryFromJson
    |> geometryToFeature
    
    
let dtoToJson (dto: GeoJsonFeatureDto) : string =
    JsonSerializer.Serialize(dto, jsonOptions)
