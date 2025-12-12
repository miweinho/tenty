module Tests.Model.GeoJson.GeoJsonTest

open System
open System.Text.Json
open System.Text.Json.Serialization
open Xunit
open backend_tenty.Service.GeoJsonService

// Local JSON options for tests (cannot access the service's private jsonOptions)
let private jsonOptions =
    let o = JsonSerializerOptions()
    o.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
    o.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
    o

// Tests for public API of GeoJsonService

[<Fact>]
let ``geometry json roundtrip for Point`` () =
    let g = Point { lon = 7.4474; lat = 46.9480 }
    let json = geometryToJson g
    let parsed = geometryFromJson json
    Assert.Equal(g, parsed)

[<Fact>]
let ``geometry json roundtrip for LineString`` () =
    let coords = [ { lon = 7.44; lat = 46.94 }; { lon = 7.45; lat = 46.95 } ]
    let g = LineString coords
    let json = geometryToJson g
    let parsed = geometryFromJson json
    Assert.Equal(g, parsed)

[<Fact>]
let ``geometry json roundtrip for Polygon`` () =
    let ring = [ { lon = 7.44; lat = 46.94 }; { lon = 7.45; lat = 46.94 }; { lon = 7.45; lat = 46.95 }; { lon = 7.44; lat = 46.94 } ]
    let g = Polygon [ ring ]
    let json = geometryToJson g
    let parsed = geometryFromJson json
    Assert.Equal(g, parsed)

[<Fact>]
let ``feature json roundtrip`` () =
    let g = geometryToFeature (Point { lon = 7.4474; lat = 46.9480 })
    let json = serializeFeatureToJson g
    let parsed = deserializeFeatureFromJson json
    Assert.Equal(g, parsed)

[<Fact>]
let ``feature collection json roundtrip`` () =
    let f1 = geometryToFeature (Point { lon = 7.44; lat = 46.94 })
    let f2 = geometryToFeature (Point { lon = 7.45; lat = 46.95 })
    let fc = { features = [ f1; f2 ] }
    let json = serializeFeatureCollectionToJson fc
    let parsed = deserializeFeatureCollectionFromJson json
    Assert.Equal(fc.features.Length, parsed.features.Length)
    Assert.Equal(fc, parsed)

[<Fact>]
let ``geometry wkt roundtrip for Point`` () =
    let g = Point { lon = 7.4474; lat = 46.9480 }
    let wkt = serializeGeometryToWkt g
    let parsed = deserializeGeometryFromWkt wkt
    Assert.Equal(g, parsed)

[<Fact>]
let ``geometry wkt roundtrip for LineString`` () =
    let g = LineString [ { lon = 7.44; lat = 46.94 }; { lon = 7.45; lat = 46.95 } ]
    let wkt = serializeGeometryToWkt g
    let parsed = deserializeGeometryFromWkt wkt
    Assert.Equal(g, parsed)

[<Fact>]
let ``geometry wkt roundtrip for Polygon`` () =
    let ring = [ { lon = 7.44; lat = 46.94 }; { lon = 7.45; lat = 46.94 }; { lon = 7.45; lat = 46.95 }; { lon = 7.44; lat = 46.94 } ]
    let g = Polygon [ ring ]
    let wkt = serializeGeometryToWkt g
    let parsed = deserializeGeometryFromWkt wkt
    Assert.Equal(g, parsed)

[<Fact>]
let ``deserializeGeometryFromWkt accepts SRID prefix`` () =
    let wkt = "SRID=2056;POINT(7.4474 46.9480)"
    let parsed = deserializeGeometryFromWkt wkt
    Assert.Equal(Point { lon = 7.4474; lat = 46.9480 }, parsed)

[<Fact>]
let ``featureDtoToDbWkt converts valid dto to wkt`` () =
    let coords = JsonSerializer.SerializeToElement<float[]>([| 7.4474; 46.9480 |], jsonOptions)
    let geomDto = { ``type`` = "Point"; coordinates = coords }
    let props = JsonSerializer.SerializeToElement<Map<string,string>>(Map.empty, jsonOptions)
    let featureDto = { ``type`` = "Feature"; geometry = geomDto; properties = props }
    let wkt = featureDtoToDbWkt featureDto
    // should produce a POINT(...) string
    Assert.StartsWith("POINT(", wkt)

[<Fact>]
let ``serializeFeatureToJson contains proper type and geometry type`` () =
    let f = geometryToFeature (Point { lon = 7.4474; lat = 46.9480 })
    let json = serializeFeatureToJson f
    use doc = JsonDocument.Parse(json)
    let root = doc.RootElement
    Assert.Equal("Feature", root.GetProperty("type").GetString())
    Assert.Equal("Point", root.GetProperty("geometry").GetProperty("type").GetString())

[<Fact>]
let ``featureCollectionToWkts returns matching WKT list`` () =
    let f1 = geometryToFeature (Point { lon = 7.44; lat = 46.94 })
    let f2 = geometryToFeature (Point { lon = 7.45; lat = 46.95 })
    let fc = { features = [ f1; f2 ] }
    let wkts = featureCollectionToWkts fc
    Assert.Equal(2, wkts.Length)
    Assert.True(wkts |> List.forall (fun s -> s.StartsWith("POINT(")))

[<Fact>]
let ``serializeGeometryToWkt LineString formatting`` () =
    let g = LineString [ { lon = 7.44; lat = 46.94 }; { lon = 7.45; lat = 46.95 } ]
    let wkt = serializeGeometryToWkt g
    Assert.StartsWith("LINESTRING(", wkt)
    Assert.Contains(",", wkt)

[<Fact>]
let ``deserializeFeatureCollectionFromJson reports failing feature index`` () =
    let badJson = "{ \"type\": \"FeatureCollection\", \"features\": [ { \"type\": \"Feature\", \"geometry\": { \"type\": \"Point\", \"coordinates\": [7.44, 46.94] }, \"properties\": {} }, { \"type\": \"Feature\", \"geometry\": { \"type\": \"Point\", \"coordinates\": 123 }, \"properties\": {} } ] }"
    let ex = Assert.ThrowsAny<System.Exception>(fun () -> deserializeFeatureCollectionFromJson badJson |> ignore)
    Assert.Contains("feature index", ex.Message)

[<Fact>]
let ``featureDtoToDbWkt throws wrapped featureFromDto failure for unsupported geometry`` () =
    let coords = JsonSerializer.SerializeToElement<int>(123, jsonOptions)
    let geomDto = { ``type`` = "Foo"; coordinates = coords }
    let props = JsonSerializer.SerializeToElement<Map<string,string>>(Map.empty, jsonOptions)
    let featureDto = { ``type`` = "Feature"; geometry = geomDto; properties = props }
    let ex = Assert.ThrowsAny<System.Exception>(fun () -> featureDtoToDbWkt featureDto |> ignore)
    Assert.Contains("featureFromDto failed", ex.Message)

[<Fact>]
let ``deserializeGeometryFromWkt throws on invalid WKT`` () =
    let bad = "NOTDEFINED(1 2)"
    let ex = Assert.ThrowsAny<System.Exception>(fun () -> deserializeGeometryFromWkt bad |> ignore)
    Assert.Contains("Unsupported or invalid WKT", ex.Message)

[<Fact>]
let ``geometryFromJson throws when coordinates are strings`` () =
    let badJson = "{ \"type\": \"Point\", \"coordinates\": [\"7.4474\", \"46.948\"] }"
    Assert.ThrowsAny<System.Exception>(fun () -> geometryFromJson badJson |> ignore) |> ignore

[<Fact>]
let ``geometryFromJson throws on invalid geometry`` () =
    let badJson = "{ \"type\": \"Point\", \"coordinates\": 123 }"
    Assert.Throws<System.Exception>(fun () -> geometryFromJson badJson |> ignore) |> ignore

[<Fact>]
let ``deserializeFeatureFromJson throws on malformed json`` () =
    let bad = "{ this is not valid json }"
    Assert.ThrowsAny<System.Exception>(fun () -> deserializeFeatureFromJson bad |> ignore) |> ignore

[<Fact>]
let ``deserializeFeatureCollectionFromJson throws on malformed json`` () =
    let bad = "{ not a feature collection }"
    Assert.ThrowsAny<System.Exception>(fun () -> deserializeFeatureCollectionFromJson bad |> ignore) |> ignore

[<Fact>]
let ``deserializeFeatureFromJson preserves properties name and extra`` () =
    // Build DTO JSON with properties.name and custom key
    let json = "{ \"type\": \"Feature\", \"geometry\": { \"type\": \"Point\", \"coordinates\": [7.44, 46.94] }, \"properties\": { \"name\": \"myName\", \"custom\": 123 } }"
    let f = deserializeFeatureFromJson json
    Assert.Equal(Some "myName", f.properties.name)
    Assert.True(f.properties.extra.ContainsKey("custom"))

[<Fact>]
let ``serializeFeatureCollectionToJson with Polygon roundtrip`` () =
    let ring = [ { lon = 7.44; lat = 46.94 }; { lon = 7.45; lat = 46.94 }; { lon = 7.45; lat = 46.95 }; { lon = 7.44; lat = 46.94 } ]
    let f = geometryToFeature (Polygon [ ring ])
    let fc = { features = [ f ] }
    let json = serializeFeatureCollectionToJson fc
    let parsed = deserializeFeatureCollectionFromJson json
    Assert.Equal(1, parsed.features.Length)
    match parsed.features.Head.geometry with
    | Polygon _ -> ()
    | _ -> failwith "Expected Polygon geometry"

[<Fact>]
let ``serializeGeometryToWkt Polygon formatting`` () =
    let ring = [ { lon = 7.44; lat = 46.94 }; { lon = 7.45; lat = 46.94 }; { lon = 7.45; lat = 46.95 }; { lon = 7.44; lat = 46.94 } ]
    let g = Polygon [ ring ]
    let wkt = serializeGeometryToWkt g
    Assert.StartsWith("POLYGON(", wkt)
    Assert.Contains("(", wkt)
    Assert.Contains(")", wkt)

[<Fact>]
let ``featureDtoToDbWkt converts polygon dto to POLYGON`` () =
    // Build geometry dto array [[[lon,lat],...]]
    let ringArr : float[][] = [| [| 7.44; 46.94 |]; [| 7.45; 46.94 |]; [| 7.45; 46.95 |]; [| 7.44; 46.94 |] |]
    let coordsArr : float[][][] = [| ringArr |]
    let coords = JsonSerializer.SerializeToElement<float[][][]>(coordsArr, jsonOptions)
    let geomDto = { ``type`` = "Polygon"; coordinates = coords }
    let props = JsonSerializer.SerializeToElement<Map<string,string>>(Map.empty, jsonOptions)
    let featureDto = { ``type`` = "Feature"; geometry = geomDto; properties = props }
    let wkt = featureDtoToDbWkt featureDto
    Assert.StartsWith("POLYGON(", wkt)

[<Fact>]
let ``deserializeFeatureFromJson accepts properties null`` () =
    let json = "{ \"type\": \"Feature\", \"geometry\": { \"type\": \"Point\", \"coordinates\": [7.44, 46.94] }, \"properties\": null }"
    let f = deserializeFeatureFromJson json
    Assert.Equal(None, f.properties.name)
    Assert.Equal(None, f.properties.coordType)
    Assert.True(f.properties.extra.IsEmpty)

[<Fact>]
let ``geometryFromJson LineString with single coordinate throws`` () =
    let json = "{ \"type\": \"LineString\", \"coordinates\": [[7.44,46.94]] }"
    Assert.ThrowsAny<System.Exception>(fun () -> geometryFromJson json |> ignore) |> ignore

[<Fact>]
let ``geometryFromJson Polygon with short ring throws`` () =
    // ring with only 3 positions (less than 4)
    let json = "{ \"type\": \"Polygon\", \"coordinates\": [[[7.44,46.94],[7.45,46.94],[7.44,46.94]]] }"
    Assert.ThrowsAny<System.Exception>(fun () -> geometryFromJson json |> ignore) |> ignore

[<Fact>]
let ``featureDtoToDbWkt fails for polygon ring not closed`` () =
    // ring where first != last
    let ringArr : float[][] = [| [| 7.44; 46.94 |]; [| 7.45; 46.94 |]; [| 7.45; 46.95 |]; [| 7.44; 46.95 |] |]
    let coordsArr : float[][][] = [| ringArr |]
    let coords = JsonSerializer.SerializeToElement<float[][][]>(coordsArr, jsonOptions)
    let geomDto = { ``type`` = "Polygon"; coordinates = coords }
    let props = JsonSerializer.SerializeToElement<Map<string,string>>(Map.empty, jsonOptions)
    let featureDto = { ``type`` = "Feature"; geometry = geomDto; properties = props }
    let ex = Assert.ThrowsAny<System.Exception>(fun () -> featureDtoToDbWkt featureDto |> ignore)
    Assert.Contains("Invalid polygon ring", ex.Message)

[<Fact>]
let ``deserializeGeometryFromWkt LineString with single coord throws`` () =
    let bad = "LINESTRING(7.44 46.94)"
    let ex = Assert.ThrowsAny<System.Exception>(fun () -> deserializeGeometryFromWkt bad |> ignore)
    Assert.Contains("needs at least 2 coordinates", ex.Message)

[<Fact>]
let ``serializeFeatureToJson extra cannot overwrite name property`` () =
    // Build a Feature with properties.name = "keep" and extra contains "name" -> should not overwrite
    let extraMap = Map.ofList [ ("name", JsonSerializer.SerializeToElement<string>("bad", jsonOptions)); ("x", JsonSerializer.SerializeToElement<int>(1, jsonOptions)) ]
    let props = { name = Some "keep"; coordType = Some "EPSG:4326"; extra = extraMap }
    let feature = { geometry = Point { lon = 7.44; lat = 46.94 }; properties = props }
    let json = serializeFeatureToJson feature
    use doc = JsonDocument.Parse(json)
    let p = doc.RootElement.GetProperty("properties")
    Assert.Equal("keep", p.GetProperty("name").GetString())
    Assert.Equal(1, p.GetProperty("x").GetInt32())

[<Fact>]
let ``deserializeFeatureFromJson preserves coordType when present`` () =
    let json = "{ \"type\": \"Feature\", \"geometry\": { \"type\": \"Point\", \"coordinates\": [7.44, 46.94] }, \"properties\": { \"coordType\": \"EPSG:4326\" } }"
    let f = deserializeFeatureFromJson json
    Assert.Equal(Some "EPSG:4326", f.properties.coordType)

[<Fact>]
let ``geometryToJson numeric formatting uses dot and preserves precision`` () =
    let g = Point { lon = 7.4474; lat = 46.948 }
    let json = geometryToJson g
    use doc = JsonDocument.Parse(json)
    let coords = doc.RootElement.GetProperty("coordinates")
    // coordinates is an array [lon, lat]
    let lon = coords.[0].GetDouble()
    let lat = coords.[1].GetDouble()
    Assert.Equal(7.4474, lon)
    Assert.Equal(46.948, lat)

[<Fact>]
let ``featureCollectionToWkts handles mixed geometries`` () =
    let p = geometryToFeature (Point { lon = 7.44; lat = 46.94 })
    let ring = [ { lon = 7.44; lat = 46.94 }; { lon = 7.45; lat = 46.94 }; { lon = 7.45; lat = 46.95 }; { lon = 7.44; lat = 46.94 } ]
    let poly = geometryToFeature (Polygon [ ring ])
    let fc = { features = [ p; poly ] }
    let wkts = featureCollectionToWkts fc
    Assert.Equal(2, wkts.Length)
    Assert.True(wkts.[0].StartsWith("POINT(") || wkts.[1].StartsWith("POINT("))
    Assert.True(wkts |> List.exists (fun s -> s.StartsWith("POLYGON(")))

[<Fact>]
let ``serializeFeatureToJson omits null optional properties`` () =
    let props = { name = None; coordType = None; extra = Map.empty }
    let feature = { geometry = Point { lon = 7.44; lat = 46.94 }; properties = props }
    let json = serializeFeatureToJson feature
    use doc = JsonDocument.Parse(json)
    let p = doc.RootElement.GetProperty("properties")
    // With DefaultIgnoreCondition.WhenWritingNull the properties object will be empty (no name/coordType)
    Assert.Equal(JsonValueKind.Object, p.ValueKind)
    Assert.Equal(0, p.EnumerateObject() |> Seq.length)

[<Fact>]
let ``serializeFeatureToJson preserves complex extra object in properties.extra`` () =
    let complex = JsonSerializer.SerializeToElement<{| a:int; b:string |}>({| a = 1; b = "x" |}, jsonOptions)
    let extra = Map.ofList [ ("meta", complex) ]
    let props = { name = None; coordType = None; extra = extra }
    let feature = { geometry = Point { lon = 7.44; lat = 46.94 }; properties = props }
    let json = serializeFeatureToJson feature
    use doc = JsonDocument.Parse(json)
    let p = doc.RootElement.GetProperty("properties")
    let meta = p.GetProperty("meta")
    Assert.Equal(JsonValueKind.Object, meta.ValueKind)
    Assert.Equal(1, meta.GetProperty("a").GetInt32())
    Assert.Equal("x", meta.GetProperty("b").GetString())

// Neue Tests ergänzen

[<Fact>]
let ``geometryFromJson ignores unknown extra fields like bbox`` () =
    let json = "{ \"type\": \"Point\", \"coordinates\": [7.44, 46.94], \"bbox\": [0,0,1,1] }"
    let parsed = geometryFromJson json
    Assert.Equal(Point { lon = 7.44; lat = 46.94 }, parsed)

[<Fact>]
let ``deserializeFeatureFromJson ignores crs inside properties and keeps extras`` () =
    let json = "{ \"type\": \"Feature\", \"geometry\": { \"type\": \"Point\", \"coordinates\": [7.44, 46.94] }, \"properties\": { \"name\": \"keep\", \"crs\": { \"type\": \"name\", \"properties\": { \"name\": \"EPSG:4326\" } }, \"x\": 42 } }"
    let f = deserializeFeatureFromJson json
    Assert.Equal(Some "keep", f.properties.name)
    // "crs" must be ignored and therefore not present in extra
    Assert.False(f.properties.extra.ContainsKey("crs"))
    // "x" should be present in extra with correct numeric value
    Assert.Equal(42, f.properties.extra.["x"].GetInt32())

[<Fact>]
let ``serializeGeometryToWkt preserves numeric precision up to 8 decimal places`` () =
    // Use values with 8 decimal places which matches format "0.########"
    let g = Point { lon = 1.2345678; lat = 9.8765432 }
    let wkt = serializeGeometryToWkt g
    // Ensure both longitude and latitude are present with full precision
    Assert.Contains("1.2345678", wkt)
    Assert.Contains("9.8765432", wkt)

[<Fact>]
let ``polygon with hole wkt roundtrip`` () =
    let outer = [ { lon = 7.44; lat = 46.94 }; { lon = 7.45; lat = 46.94 }; { lon = 7.45; lat = 46.95 }; { lon = 7.44; lat = 46.94 } ]
    let inner = [ { lon = 7.445; lat = 46.945 }; { lon = 7.446; lat = 46.945 }; { lon = 7.446; lat = 46.946 }; { lon = 7.445; lat = 46.945 } ]
    let g = Polygon [ outer; inner ]
    let wkt = serializeGeometryToWkt g
    Assert.StartsWith("POLYGON(", wkt)
    let parsed = deserializeGeometryFromWkt wkt
    Assert.Equal(g, parsed)

[<Fact>]
let ``geometryFromJson Polygon ignores unknown extra fields like bbox`` () =
    let json = "{ \"type\": \"Polygon\", \"coordinates\": [[[7.44,46.94],[7.45,46.94],[7.45,46.95],[7.44,46.94]]], \"bbox\": [0,0,1,1] }"
    let ring = [ { lon = 7.44; lat = 46.94 }; { lon = 7.45; lat = 46.94 }; { lon = 7.45; lat = 46.95 }; { lon = 7.44; lat = 46.94 } ]
    let expected = Polygon [ ring ]
    let parsed = geometryFromJson json
    Assert.Equal(expected, parsed)

[<Fact>]
let ``deserializeFeatureFromJson ignores nonstring name in properties`` () =
    let json = "{ \"type\": \"Feature\", \"geometry\": { \"type\": \"Point\", \"coordinates\": [7.44, 46.94] }, \"properties\": { \"name\": 123, \"other\": \"x\" } }"
    let f = deserializeFeatureFromJson json
    // name is not a string so it should be ignored
    Assert.Equal(None, f.properties.name)
    Assert.True(f.properties.extra.ContainsKey("other"))

[<Fact>]
let ``deserializeGeometryFromWkt tolerates whitespace and lowercase srid and keywords`` () =
    let s = "  srid=2056; point ( 7.4474   46.9480 ) "
    let parsed = deserializeGeometryFromWkt s
    Assert.Equal(Point { lon = 7.4474; lat = 46.9480 }, parsed)

[<Fact>]
let ``featureDtoToDbWkt converts polygon dto with hole to POLYGON with multiple rings`` () =
    // Build geometry dto array with outer and inner rings
    let outerArr : float[][] = [| [| 7.44; 46.94 |]; [| 7.45; 46.94 |]; [| 7.45; 46.95 |]; [| 7.44; 46.94 |] |]
    let innerArr : float[][] = [| [| 7.445; 46.945 |]; [| 7.446; 46.945 |]; [| 7.446; 46.946 |]; [| 7.445; 46.945 |] |]
    let coordsArr : float[][][] = [| outerArr; innerArr |]
    let coords = JsonSerializer.SerializeToElement<float[][][]>(coordsArr, jsonOptions)
    let geomDto = { ``type`` = "Polygon"; coordinates = coords }
    let props = JsonSerializer.SerializeToElement<Map<string,string>>(Map.empty, jsonOptions)
    let featureDto = { ``type`` = "Feature"; geometry = geomDto; properties = props }
    let wkt = featureDtoToDbWkt featureDto
    Assert.StartsWith("POLYGON(", wkt)
    // WKT for multiple rings should contain the separator "),("
    Assert.Contains("),(", wkt)

// Focused coverage tests for specific functions requested

[<Fact>]
let ``featureToDto produces expected dto structure`` () =
    let f = geometryToFeature (Point { lon = 8.0; lat = 47.0 })
    let dto = featureToDto f
    Assert.Equal("Feature", dto.``type``)
    Assert.Equal("Point", dto.geometry.``type``)
    // properties is a JsonElement object and should be an object (empty)
    Assert.Equal(JsonValueKind.Object, dto.properties.ValueKind)

[<Fact>]
let ``geometryToFeature returns feature with empty properties`` () =
    let g = LineString [ { lon = 8.0; lat = 47.0 }; { lon = 8.1; lat = 47.1 } ]
    let f = geometryToFeature g
    Assert.Equal(g, f.geometry)
    Assert.Equal(None, f.properties.name)
    Assert.Equal(None, f.properties.coordType)
    Assert.True(f.properties.extra.IsEmpty)

[<Fact>]
let ``geometryToWkt and geometryFromWkt are inverse for LineString`` () =
    let g = LineString [ { lon = 7.1; lat = 46.1 }; { lon = 7.2; lat = 46.2 } ]
    let wkt = geometryToWkt g
    let parsed = geometryFromWkt wkt
    Assert.Equal(g, parsed)

[<Fact>]
let ``featureToWkt equals geometry WKT for feature`` () =
    let f = geometryToFeature (Point { lon = 7.5; lat = 46.5 })
    let w1 = featureToWkt f
    let w2 = geometryToWkt f.geometry
    Assert.Equal(w1, w2)

[<Fact>]
let ``featureCollectionToWkts returns wkts for each feature`` () =
    let f1 = geometryToFeature (Point { lon = 7.0; lat = 46.0 })
    let f2 = geometryToFeature (Point { lon = 7.1; lat = 46.1 })
    let fc = { features = [ f1; f2 ] }
    let wkts = featureCollectionToWkts fc
    Assert.Equal(2, wkts.Length)
    Assert.Equal(serializeGeometryToWkt f1.geometry, wkts.[0])

[<Fact>]
let ``deserializeFeatureCollectionFromJson and deserializeFeatureFromJson roundtrip simple collection`` () =
    let f = geometryToFeature (Point { lon = 7.2; lat = 46.2 })
    let fc = { features = [ f ] }
    let json = serializeFeatureCollectionToJson fc
    let parsedFc = deserializeFeatureCollectionFromJson json
    Assert.Equal(1, parsedFc.features.Length)
    let featureJson = serializeFeatureToJson f
    let parsedF = deserializeFeatureFromJson featureJson
    Assert.Equal(f.geometry, parsedF.geometry)

[<Fact>]
let ``serializeFeatureToJson yields properties object and geometry type`` () =
    let props = { name = Some "n"; coordType = None; extra = Map.empty }
    let f = { geometry = Point { lon = 6.0; lat = 45.0 }; properties = props }
    let json = serializeFeatureToJson f
    use doc = JsonDocument.Parse(json)
    let root = doc.RootElement
    Assert.Equal("Feature", root.GetProperty("type").GetString())
    Assert.Equal("Point", root.GetProperty("geometry").GetProperty("type").GetString())
    Assert.Equal("n", root.GetProperty("properties").GetProperty("name").GetString())

[<Fact>]
let ``geometryToJson and geometryFromJson roundtrip for Polygon`` () =
    let ring = [ { lon = 7.44; lat = 46.94 }; { lon = 7.45; lat = 46.94 }; { lon = 7.45; lat = 46.95 }; { lon = 7.44; lat = 46.94 } ]
    let g = Polygon [ ring ]
    let json = geometryToJson g
    let parsed = geometryFromJson json
    Assert.Equal(g, parsed)

[<Fact>]
let ``deserializeGeometryFromWkt and serializeGeometryToWkt wrappers`` () =
    let g = Point { lon = 2.0; lat = 3.0 }
    let wkt = serializeGeometryToWkt g
    let parsed = deserializeGeometryFromWkt wkt
    Assert.Equal(g, parsed)

[<Fact>]
let ``featureToDbWkt equals featureToWkt and featureDtoToDbWkt produces POLYGON for polygon dto`` () =
    let f = geometryToFeature (Polygon [ [ { lon = 1.0; lat = 2.0 }; { lon = 1.1; lat = 2.0 }; { lon = 1.1; lat = 2.1 }; { lon = 1.0; lat = 2.0 } ] ])
    let w1 = featureToDbWkt f
    let w2 = featureToWkt f
    Assert.Equal(w1, w2)

    // Test featureDtoToDbWkt produces POLYGON for a DTO
    let ringArr : float[][] = [| [| 1.0; 2.0 |]; [| 1.1; 2.0 |]; [| 1.1; 2.1 |]; [| 1.0; 2.0 |] |]
    let coordsArr : float[][][] = [| ringArr |]
    let coords = JsonSerializer.SerializeToElement<float[][][]>(coordsArr, jsonOptions)
    let geomDto = { ``type`` = "Polygon"; coordinates = coords }
    let props = JsonSerializer.SerializeToElement<Map<string,string>>(Map.empty, jsonOptions)
    let featureDto = { ``type`` = "Feature"; geometry = geomDto; properties = props }
    let wktDto = featureDtoToDbWkt featureDto
    Assert.StartsWith("POLYGON(", wktDto)
