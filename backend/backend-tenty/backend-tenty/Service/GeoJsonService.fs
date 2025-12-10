/// Helpers for converting between domain geometry, GeoJSON payloads, and WKT strings.
module backend_tenty.Service.GeoJsonService

open System
open System.Collections.Generic
open System.Globalization
open System.Text.Json
open System.Text.Json.Serialization
open Giraffe

/// Represents a geographic coordinate with longitude/latitude precision.
type Position = {
    lon: float
    lat: float
}



/// Discriminated union for GeoJSON geometry shapes supported by the API.
type Geometry =
    | Point of Position
    | LineString of Position list
    | Polygon of Position list list

/// Optional feature metadata captured alongside geometry.
type FeatureProps = {
    name      : string option
    coordType : string option
    extra     : Map<string, JsonElement>
}

/// Domain representation of a GeoJSON Feature.
type GeoJsonFeature = {
    geometry   : Geometry
    properties : FeatureProps
}

/// Domain representation of a GeoJSON FeatureCollection.
type GeoJsonFeatureCollection = {
    features : GeoJsonFeature list
}

/// Client-facing GeoJSON geometry DTO used for serialization and deserialization.
type GeoJsonGeometryDto = {
    [<JsonPropertyName("type")>]
    ``type``      : string

    [<JsonPropertyName("coordinates")>]
    coordinates   : JsonElement
}

/// Client-facing GeoJSON feature DTO used for payload parsing.
type GeoJsonFeatureDto = {
    [<JsonPropertyName("type")>]
    ``type``      : string

    [<JsonPropertyName("geometry")>]
    geometry      : GeoJsonGeometryDto

    [<JsonPropertyName("properties")>]
    properties    : JsonElement
}

/// Client-facing GeoJSON feature collection DTO used for payload parsing.
type GeoJsonFeatureCollectionDto = {
    [<JsonPropertyName("type")>]
    ``type``   : string

    [<JsonPropertyName("features")>]
    features   : GeoJsonFeatureDto array
}

// -------------------------
// JSON options
// -------------------------
let private jsonOptions =
    let o = JsonSerializerOptions()
    o.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
    o.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
    o

// -------------------------
// Helpers
// -------------------------
let private invariant = CultureInfo.InvariantCulture

let private parseDouble (s: string) =
    Double.Parse(s, invariant)

let private formatDouble (v: float) =
    v.ToString("0.########", invariant)

let private ensureLinearRing (ring: Position list) =
    match ring with
    | r when r.Length < 4 ->
        failwithf "Invalid polygon ring: expected at least 4 positions, got %d" r.Length
    | r ->
        // GeoJSON spec: first and last coordinate of a ring MUST be the same.
        let first = r.Head
        let last  = r.[r.Length - 1]
        if first <> last then
            failwith "Invalid polygon ring: first and last position must be identical"
        r


let private emptyProperties =
    { name = None; coordType = None; extra = Map.empty }

// -------------------------
// Domain -> GeoJSON DTO
// -------------------------

let private geometryToDto (g: Geometry) : GeoJsonGeometryDto =
    match g with
    | Point p ->
        let coords = JsonSerializer.SerializeToElement([| p.lon; p.lat |], jsonOptions)
        { ``type`` = "Point"
          coordinates = coords }
        
    | LineString coords ->
        let coordArray =
            coords
            |> List.map (fun p -> [| p.lon; p.lat |])
            |> List.toArray
            |> fun arr -> JsonSerializer.SerializeToElement(arr, jsonOptions)

        { ``type`` = "LineString"
          coordinates = coordArray }

    | Polygon rings ->
        // [[[lon,lat]; ...]; ...]
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

    // Do NOT let extra overwrite known properties
    for KeyValue(k, v) in p.extra do
        if not (dict.ContainsKey k) then
            dict.Add(k, v)

    JsonSerializer.SerializeToElement(dict, jsonOptions)

/// Converts a domain ``GeoJsonFeature`` into its DTO representation for serialization.
let featureToDto (f: GeoJsonFeature) : GeoJsonFeatureDto =
    { ``type``   = "Feature"
      geometry   = geometryToDto f.geometry
      properties = propsToJsonElement f.properties }

let private collectionToDto (fc: GeoJsonFeatureCollection) : GeoJsonFeatureCollectionDto =
    { ``type`` = "FeatureCollection"
      features = fc.features |> List.map featureToDto |> List.toArray }

// -------------------------
// GeoJSON DTO -> Domain
// -------------------------

let private geometryFromDto (g: GeoJsonGeometryDto) : Geometry =
    match g.``type`` with
    | "Point" ->
        if g.coordinates.ValueKind <> JsonValueKind.Array then
            failwith "Invalid Point: coordinates must be an array [lon, lat]"

        let arr = g.coordinates.EnumerateArray() |> Seq.toArray
        if arr.Length <> 2 then
            failwithf "Invalid Point coordinates: expected [lon, lat], got %d elements" arr.Length

        let lon = arr.[0].GetDouble()
        let lat = arr.[1].GetDouble()
        Point { lon = lon; lat = lat }

    | "LineString" ->
        if g.coordinates.ValueKind <> JsonValueKind.Array then
            failwith "Invalid LineString: coordinates must be an array"

        let coords =
            g.coordinates.EnumerateArray()
            |> Seq.map (fun coord ->
                if coord.ValueKind <> JsonValueKind.Array then
                    failwith "Invalid LineString: each coordinate must be an array [lon, lat]"

                let a = coord.EnumerateArray() |> Seq.toArray
                if a.Length <> 2 then
                    failwithf "Invalid LineString coordinate: expected [lon, lat], got %d elements" a.Length

                { lon = a.[0].GetDouble()
                  lat = a.[1].GetDouble() })
            |> Seq.toList

        if coords.Length < 2 then
            failwith "Invalid LineString: expected at least 2 coordinates"

        LineString coords

    
    | "Polygon" ->
        if g.coordinates.ValueKind <> JsonValueKind.Array then
            failwith "Invalid Polygon: coordinates must be an array of linear rings"

        let rings =
            g.coordinates.EnumerateArray()
            |> Seq.map (fun ringJson ->
                if ringJson.ValueKind <> JsonValueKind.Array then
                    failwith "Invalid Polygon: each ring must be an array"
                ringJson.EnumerateArray()
                |> Seq.map (fun coord ->
                    if coord.ValueKind <> JsonValueKind.Array then
                        failwith "Invalid Polygon: each coordinate must be an array [lon, lat]"
                    let a = coord.EnumerateArray() |> Seq.toArray
                    if a.Length <> 2 then
                        failwithf "Invalid polygon coordinate: expected [lon, lat], got %d elements" a.Length
                    { lon = a.[0].GetDouble()
                      lat = a.[1].GetDouble() })
                |> Seq.toList
                |> ensureLinearRing)
            |> Seq.toList

        Polygon rings

    | other ->
        failwithf "Unsupported GeoJSON geometry type: %s" other

let private propsFromJsonElement (propsJson: JsonElement) : FeatureProps =
    let mutable name : string option = None
    let mutable coordType : string option = None
    let mutable extra : Map<string, JsonElement> = Map.empty

    if propsJson.ValueKind <> JsonValueKind.Object then
        // Allow null/undefined → empty properties
        if propsJson.ValueKind <> JsonValueKind.Null && propsJson.ValueKind <> JsonValueKind.Undefined then
            failwith "GeoJSON properties must be an object"
    else
        for p in propsJson.EnumerateObject() do
            match p.Name with
            | "name" ->
                if p.Value.ValueKind = JsonValueKind.String then
                    name <- Some (p.Value.GetString())
            | "coordType" ->
                if p.Value.ValueKind = JsonValueKind.String then
                    coordType <- Some (p.Value.GetString())
            | "crs" -> // Ignore CRS field if it shows up in properties
                ()
            | other ->
                extra <- extra.Add(other, p.Value.Clone())

    { name      = name
      coordType = coordType
      extra     = extra }

/// Wraps a geometry in a ``GeoJsonFeature`` with empty properties.
let geometryToFeature (g: Geometry) : GeoJsonFeature =
    {
        geometry   = g
        properties = { name = None; coordType = None; extra = Map.empty }
    }

/// Converts a GeoJSON feature DTO into the domain representation, surfacing parse errors.
let featureFromDto (f: GeoJsonFeatureDto) : GeoJsonFeature =
    try
        { geometry   = geometryFromDto f.geometry
          properties = propsFromJsonElement f.properties }
    with ex ->            
        failwithf "featureFromDto failed: %s" ex.Message

let private featureCollectionFromDto (fc: GeoJsonFeatureCollectionDto) : GeoJsonFeatureCollection =
    try
        let features =
            fc.features
            |> Array.mapi (fun i dto ->
                try featureFromDto dto
                with ex ->
                    let dtoJson =
                        try JsonSerializer.Serialize(dto, jsonOptions)
                        with _ -> "<failed to serialize feature dto>"
                    failwithf "featureCollectionFromDto: failure at feature index %d: %s | DTO: %s" i ex.Message dtoJson
            )
            |> Array.toList
        { features = features }
    with ex ->
        let fcJson =
            try JsonSerializer.Serialize(fc, jsonOptions)
            with _ -> "<failed to serialize feature collection dto>"
        failwithf "featureCollectionFromDto failed: %s | Collection DTO: %s" ex.Message fcJson

// -------------------------
// WKT (DB) <-> Geometry
// -------------------------
//
// DB DTO: string like "POINT(2600000 1200000)" or "POLYGON((...))"
// Optional EWKT prefix "SRID=2056;..." is accepted on input.

let private stripSridPrefix (wkt: string) =
    let s = wkt.Trim()
    if s.StartsWith("SRID=", StringComparison.OrdinalIgnoreCase) then
        let idx = s.IndexOf(';')
        if idx > 0 then s.Substring(idx + 1).Trim() else s
    else
        s

let private betweenParens (s: string) =
    let openIdx = s.IndexOf('(')
    let closeIdx = s.LastIndexOf(')')
    if openIdx < 0 || closeIdx <= openIdx then
        failwithf "Invalid WKT: %s" s
    s.Substring(openIdx + 1, closeIdx - openIdx - 1).Trim()

/// Parses Well-Known Text (WKT) into a domain ``Geometry`` value.
let geometryFromWkt (wkt: string) : Geometry =
    let s = stripSridPrefix wkt

    if s.StartsWith("POINT", StringComparison.OrdinalIgnoreCase) then
        let content = betweenParens s
        let parts =
            content.Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)
        if parts.Length <> 2 then
            failwithf "Invalid POINT WKT: %s" wkt
        let x = parseDouble parts.[0]
        let y = parseDouble parts.[1]
        Point { lon = x; lat = y }
        
    elif s.StartsWith("LINESTRING", StringComparison.OrdinalIgnoreCase) then
        let content = betweenParens s

        let coords =
            content.Split([| ',' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.map (fun coordText ->
                let parts =
                    coordText.Trim().Split(
                        [| ' '; '\t' |],
                        StringSplitOptions.RemoveEmptyEntries
                    )

                if parts.Length <> 2 then
                    failwithf "Invalid LINESTRING coordinate in WKT: '%s'" coordText

                { lon = parseDouble parts.[0]
                  lat = parseDouble parts.[1] })
            |> Array.toList

        if coords.Length < 2 then
            failwithf "Invalid LINESTRING WKT (needs at least 2 coordinates): %s" wkt

        LineString coords

    elif s.StartsWith("POLYGON", StringComparison.OrdinalIgnoreCase) then
        let content = betweenParens s
        // content is like "(x y, x y),(x y, x y)" or "x y, x y"
        let contentTrim = content.Trim()

        let inner =
            if contentTrim.StartsWith("(") && contentTrim.EndsWith(")") then
                contentTrim.Substring(1, contentTrim.Length - 2)
            else
                contentTrim

        let ringsText = inner.Split([| ")(" |], StringSplitOptions.None)

        let rings =
            ringsText
            |> Array.map (fun ringText ->
                ringText.Split([| ',' |], StringSplitOptions.RemoveEmptyEntries)
                |> Array.map (fun coordText ->
                    let parts =
                        coordText.Trim().Split(
                            [| ' '; '\t' |],
                            StringSplitOptions.RemoveEmptyEntries
                        )
                    if parts.Length <> 2 then
                        failwithf "Invalid POLYGON coordinate in WKT: '%s'" coordText
                    { lon = parseDouble parts.[0]
                      lat = parseDouble parts.[1] })
                |> Array.toList
                |> ensureLinearRing)
            |> Array.toList

        Polygon rings

    else
        failwithf "Unsupported or invalid WKT geometry: %s" wkt

/// Serializes a domain ``Geometry`` into its WKT representation.
let geometryToWkt (g: Geometry) : string =
    match g with
    | Point p ->
        $"POINT({formatDouble p.lon} {formatDouble p.lat})"
        
    | LineString coords ->
        // LINESTRING(x1 y1, x2 y2, ...)
        let coordsText =
            coords
            |> List.map (fun p -> $"{formatDouble p.lon} {formatDouble p.lat}")
            |> String.concat ", "

        $"LINESTRING({coordsText})"

    | Polygon rings ->
        // POLYGON((x1 y1, x2 y2,...),(...hole...))
        let ringsText =
            rings
            |> List.map (fun ring ->
                ring
                |> List.map (fun p -> $"{formatDouble p.lon} {formatDouble p.lat}")
                |> String.concat ", "
                |> fun coords -> $"({coords})")
            |> String.concat ","

        $"POLYGON({ringsText})"

/// Serializes a domain ``GeoJsonFeature`` to WKT using its geometry component.
let featureToWkt (f: GeoJsonFeature) : string =
    geometryToWkt f.geometry

/// Serializes a feature collection to a list of WKT strings.
let featureCollectionToWkts (fc: GeoJsonFeatureCollection) : string list =
    fc.features |> List.map featureToWkt

// -------------------------
// Public API - Client (GeoJSON)
// -------------------------

/// Parse GeoJSON FeatureCollection JSON from client → domain model
let deserializeFeatureCollectionFromJson (json: string) : GeoJsonFeatureCollection =
    let dto =
        JsonSerializer.Deserialize<GeoJsonFeatureCollectionDto>(json, jsonOptions)
        |> Option.ofObj
        |> Option.defaultWith (fun () -> failwith "Invalid GeoJSON: cannot deserialize FeatureCollection")
    featureCollectionFromDto dto

/// Parse GeoJSON Feature JSON from client → domain model
let deserializeFeatureFromJson (json: string) : GeoJsonFeature =
    let dto =
        JsonSerializer.Deserialize<GeoJsonFeatureDto>(json, jsonOptions)
        |> Option.ofObj
        |> Option.defaultWith (fun () -> failwith "Invalid GeoJSON: cannot deserialize Feature")
    Console.WriteLine(dto)
    featureFromDto dto

/// Serialize domain FeatureCollection → GeoJSON string
let serializeFeatureCollectionToJson (fc: GeoJsonFeatureCollection) : string =
    let dto = collectionToDto fc
    JsonSerializer.Serialize(dto, jsonOptions)

/// Serialize domain Feature → GeoJSON string
let serializeFeatureToJson (f: GeoJsonFeature) : string =
    let dto = featureToDto f
    JsonSerializer.Serialize(dto, jsonOptions)

/// Helper: single geometry JSON (GeoJSON Geometry object) → domain
let geometryFromJson (json: string) : Geometry =
    let dto =
        JsonSerializer.Deserialize<GeoJsonGeometryDto>(json, jsonOptions)
        |> Option.ofObj
        |> Option.defaultWith (fun () -> failwith "Invalid GeoJSON geometry")
    geometryFromDto dto

/// Helper: domain geometry → GeoJSON Geometry JSON string
let geometryToJson (g: Geometry) : string =
    let dto = geometryToDto g
    JsonSerializer.Serialize(dto, jsonOptions)

// -------------------------
// Public API - DB (WKT)
// -------------------------

/// Parse WKT geometry from DB (e.g. 'POINT(2600000 1200000)') → domain Geometry
let deserializeGeometryFromWkt (wkt: string) : Geometry =
    geometryFromWkt wkt

/// Serialize domain Geometry → WKT string for DB
///
/// Example usage with PostGIS:
///   ST_GeomFromText(@wkt, 2056)
let serializeGeometryToWkt (g: Geometry) : string =
    geometryToWkt g

/// Serialize a GeoJsonFeature to WKT string for DB
let featureToDbWkt (feature: GeoJsonFeature) : string =
    serializeGeometryToWkt feature.geometry
    
/// Converts a GeoJSON feature DTO into a database-ready WKT string.
let featureDtoToDbWkt (feature: GeoJsonFeatureDto) : string =
    featureFromDto feature
    |> featureToDbWkt
