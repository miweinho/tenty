module backend_tenty.Database

open System
open Npgsql.FSharp
open backend_tenty.Model

let connectionString = "Host=localhost;Database=tenty;Username=tenty_user;Password=tenty_password"

// Connection Test
let testConnection () =
    try
        connectionString
        |> Sql.connect
        |> Sql.query "SELECT 1 as test"
        |> Sql.execute (fun read -> read.int "test")
        |> ignore
        printfn "✅ Database connection successful"
        true
    with
    | ex ->
        printfn "❌ Database connection failed: %s" ex.Message
        false

let addCamp (name: string, locationX: decimal, locationY: decimal) : int =
    connectionString
    |> Sql.connect
    |> Sql.query "INSERT INTO camps (campName, LocationX, LocationY) VALUES (@campName ,@locationX, @locationY)"
    |> Sql.parameters [ "@campName", Sql.string name; "@locationX", Sql.decimal locationX; "@locationY", Sql.decimal locationY]
    |> Sql.executeNonQuery

let getDelimitationPoints campId =
    connectionString
    |> Sql.connect
    |> Sql.query "SELECT X, Y FROM DelimitationPoints WHERE CampId = @campId ORDER BY PointOrder ASC"
    |> Sql.parameters [ "@campId", Sql.int campId ]
    |> Sql.execute (fun read ->
        let x = read.decimal "X"
        let y = read.decimal "Y"
        SwissCoordinate.create x y
    )
    |> List.choose id

let toSwissCoordinateDto (coord: SwissCoordinate) =
    let x = SwissCoordinate.x coord
    let y = SwissCoordinate.y coord
    { x = x; y = y }

let getAllCamps () : CampDto list =
    connectionString
    |> Sql.connect
    |> Sql.query "SELECT id, campname, locationx, locationy FROM camps"
    |> Sql.execute (fun read ->
        let id = read.int "id"
        let name = read.string "campname"
        let x = read.decimal "locationx"
        let y = read.decimal "locationy"
        match SwissCoordinate.create x y with
        | Some location ->
            let delimitation = getDelimitationPoints id |> List.map toSwissCoordinateDto |> List.toArray
            Some { Id = string id; Name = name; Location = toSwissCoordinateDto location; Delimitation = delimitation }
        | None -> None
    )
    |> List.choose id
