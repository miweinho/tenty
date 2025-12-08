module backend_tenty.Database

open System.Threading.Tasks
open Npgsql
open Npgsql.FSharp
open backend_tenty.Model.Types
open backend_tenty.Model.Conversion.DBConversions

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

// Database functions to modify Users:
let loadUserById (id: int) : Task<Result<DbUser, string>> = task {
    try
        let! rows =
            connectionString
            |> Sql.connect
            |> Sql.query "SELECT id, first_name, middle_name, last_name, email, created_at, updated_at FROM tenty.users WHERE id = @id"
            |> Sql.parameters [ "@id", Sql.int id ]
            |> Sql.executeAsync (fun read ->
                {
                    Id = read.int "id"
                    FirstName = read.string "first_name"
                    MiddleName = read.stringOrNone "middle_name"
                    LastName = read.string "last_name"
                    Email = read.string "email"
                    CreatedAt = read.datetimeOffsetOrNone "created_at"
                    UpdatedAt = read.datetimeOffsetOrNone "updated_at"
                }
            )
        return
            match rows |> List.tryHead with
            | None -> Error $"User with id {id} not found"
            | Some db -> Ok db
    with
    | ex -> return Error $"Database error: {ex.Message}"
  }
    
let loadUserByMail (mail: string) : Task<Result<DbUser, string>> = task {
    try

        let! rows =
            connectionString
            |> Sql.connect
            |> Sql.query "SELECT id, first_name, middle_name, last_name, email, created_at, updated_at FROM tenty.users WHERE email = @mail"
            |> Sql.parameters [ "@mail", Sql.string mail ]
            |> Sql.executeAsync (fun read ->
                {
                    Id = read.int "id"
                    FirstName = read.string "first_name"
                    MiddleName = read.stringOrNone "middle_name"
                    LastName = read.string "last_name"
                    Email = read.string "email"
                    CreatedAt = read.datetimeOffsetOrNone "created_at"
                    UpdatedAt = read.datetimeOffsetOrNone "updated_at"
                }
            )
        return
            match rows |> List.tryHead with
            | None -> Error $"User with email {mail} not found"
            | Some db -> Ok db
    with
    | ex -> return Error $"Database error: {ex.Message}"
}
    
let checkForUserByEmail (email: string): Task<Result<int, string>> = task {
    try
        let! rows =
            connectionString
            |> Sql.connect
            |> Sql.query "SELECT Count(*) AS count FROM tenty.users WHERE email = @email"
            |> Sql.parameters ["@email", Sql.string email ]
            |> Sql.executeAsync (fun read -> read.int "count")
        return
            match rows |> List.tryHead with
            | Some c -> Ok c
            | None -> Ok 0
    with
    | ex -> return Error $"Database error: {ex.Message}"
}

let addNewUser (user: User) : Task<Result<int, string>> = task {
    try
        let dbUser = domainUserToDb user
        let! rows =
            connectionString
            |> Sql.connect
            |> Sql.query "INSERT INTO users (first_name, middle_name, last_name, email) VALUES (@first_name, @middle_name, @last_name, @email)"
            |> Sql.parameters [ "@first_name", Sql.string dbUser.FirstName; "@middle_name", Sql.stringOrNone dbUser.MiddleName; "@last_name", Sql.string dbUser.LastName; "@email", Sql.string dbUser.Email]
            |> Sql.executeNonQueryAsync
        return Ok rows
    with
    | ex -> return Error $"Database error: {ex.Message}"
}
    
let updateUser (user: User) : Task<Result<int, string>> = task {
    try
        let dbUser = domainUserToDb user
        let! rows =
            connectionString
            |> Sql.connect
            |> Sql.query "UPDATE users SET (first_name, middle_name, last_name, email) = (@first_name, @middle_name, @last_name, @email) WHERE id = @id "
            |> Sql.parameters [ "@first_name", Sql.string dbUser.FirstName; "@middle_name", Sql.stringOrNone dbUser.MiddleName; "@last_name", Sql.string dbUser.LastName; "@email", Sql.string dbUser.Email; "@id", Sql.int dbUser.Id]
            |> Sql.executeNonQueryAsync
        return
            match rows with
            | 0 -> Error $"User with id {dbUser.Id} not found"
            | _ -> Ok 1
    with
    | ex -> return Error $"Database error: {ex.Message}"
}

// Converted following functions to Task-based async implementations

let getAllCampsForUser (id: int) : Task<Result<DbCamp list, string>> = task {
    try
        let! rows =
            connectionString
            |> Sql.connect
            |> Sql.query "SELECT c.id, c.owner, c.camp_name, ST_AsGeoJSON(c.loc,6)::jsonb AS geojson, c.is_sub_camp, c.parent_id, c.created_at, c.updated_at FROM tenty.camps c left join (Select * from tenty.camp_users where user_id = @id) cu on c.id = cu.camp_id where c.owner = @id OR cu.user_id = @id;"
            |> Sql.parameters ["@id", Sql.int id]
            |> Sql.executeAsync (fun read ->
                {
                    Id = read.int "id"
                    DbCamp.OwnerId = read.int "owner"
                    CampName = read.string "camp_name"
                    Location = read.text "geojson"
                    IsSubCamp = read.bool "is_sub_camp"
                    ParentId = read.intOrNone "parent_id"
                    CreatedAt = read.datetimeOffsetOrNone "created_at"
                    UpdatedAt = read.datetimeOffsetOrNone "updated_at"
                })
        return
            match rows with
            | [] -> Error $"No Camps for User with id {id} found"
            | _ -> Ok rows
    with
    | ex -> return Error $"Database error: {ex.Message}"
}

let getCampById (campId: int) : Task<Result<DbCamp, string>> = task {
    try
        let! rows =
            connectionString
            |> Sql.connect
            |> Sql.query "Select id, owner, camp_name, ST_AsGeoJSON(loc,6)::jsonb as geojson, is_sub_camp, parent_id, created_at, updated_at from tenty.camps where id = @id"
            |> Sql.parameters ["@id", Sql.int campId]
            |> Sql.executeAsync (fun read ->
                    {
                    DbCamp.Id = read.int "id"
                    OwnerId = read.int "owner"
                    CampName = read.string "camp_name"
                    Location = read.text "geojson"
                    IsSubCamp = read.bool "is_sub_camp"
                    ParentId = read.intOrNone "parent_id"
                    CreatedAt = read.datetimeOffsetOrNone "created_at"
                    UpdatedAt = read.datetimeOffsetOrNone "updated_at"
                })
        return
            match rows |> List.tryHead with
            | None -> Error $"Camp with id {campId} not found"
            | Some r -> Ok r
    with
    | ex -> return Error $"Database error: {ex.Message}"
}
    
let getAllCampsWithPermissionForUser (id: int) : Task<Result<CampPermissionSummary list, string>> = task {
    try
        let! rows =
            connectionString
            |> Sql.connect
            |> Sql.query "SELECT c.id, c.owner, c.camp_name, ST_AsGeoJSON(c.loc,6) AS geojson, cu.user_permission FROM tenty.camps c left join (Select * from tenty.camp_users where user_id = @id) cu on c.id = cu.camp_id where c.owner = @id OR cu.user_id = @id;"
            |> Sql.parameters ["@id", Sql.int id]
            |> Sql.executeAsync (fun read ->
                {
                    Id = read.int "id"
                    CampName = read.string "camp_name"
                    OwnerId = read.int "owner"
                    Permission = validateUserPermission(read.intOrNone "user_permission")

                })
        return
            match rows with
            | [] -> Error $"No Camps for User with id {id} found"
            | _ -> Ok rows
    with
    | ex -> return Error $"Database error: {ex.Message}"
}
    
let getCampUserForUser (id: int) : Task<Result<DbCampUser list ,string>> = task {
    try
        let! rows =
            connectionString
            |> Sql.connect
            |> Sql.query "Select * From tenty.camp_users WHERE user_id = @id "
            |> Sql.parameters ["@id", Sql.int id ]
            |> Sql.executeAsync (fun read ->
                {
                    UserId = read.int "user_id"
                    CampId = read.int "camp_id"
                    UserPermission = validateDbCampUserPermission (read.int "user_permission")
                    CreatedAt = read.datetimeOffsetOrNone "created_at"
                    UpdatedAt = read.datetimeOffsetOrNone "updated_at"
                })
        return
            match rows with
            | [] -> Error $"No CampUser with user id {id} found"
            | _ -> Ok rows
    with
    | ex -> return Error $"Database error: {ex.Message}"
}
    
let getCampUserForCamp (campId: int) : Task<Result<DbCampUser list, string>> = task {
    try
        let! rows =
            connectionString
            |> Sql.connect
            |> Sql.query "Select * From tenty.camp_users WHERE camp_id = @id "
            |> Sql.parameters ["@id", Sql.int campId ]
            |> Sql.executeAsync (fun read ->
                {
                    UserId = read.int "user_id"
                    CampId = read.int "camp_id"
                    UserPermission = validateDbCampUserPermission (read.int "user_permission")
                    CreatedAt = read.datetimeOffsetOrNone "created_at"
                    UpdatedAt = read.datetimeOffsetOrNone "updated_at"
                })
        return Ok rows
    with
    | ex -> return Error $"Database error: {ex.Message}"
}
    
let getCampDetailForCamp (camp_id: int): Task<Result<DbCampDetail, string>> = task {
    try
        let! rows =
            connectionString
            |> Sql.connect
            |> Sql.query "Select * from tenty.camp_details cd WHERE camp_id = @camp_id"
            |> Sql.parameters ["@camp_id", Sql.int camp_id]
            |> Sql.executeAsync (fun read ->
                {
                    Id = read.int "id"
                    CampId = read.int "camp_id"
                    StartDate = read.datetimeOffset "start_date"
                    EndDate = read.datetimeOffset "end_date"
                    Description = read.textOrNone "description"
                    City = read.string "city"
                    CreatedAt = read.datetimeOffsetOrNone "created_at"
                    UpdatedAt = read.datetimeOffsetOrNone "updated_at"
                 }
                )
        return
            match rows |> List.tryHead with
            | None -> Error $"Camp details for camp id {camp_id} not found"
            | Some r -> Ok r
    with
    | ex -> return Error $"Database error: {ex.Message}"
}
    
let getDetailListEntriesForCamp (camp_id: int): Task<Result<DbDetailListEntry list, string>> = task {
    try
        let! rows =
            connectionString
            |> Sql.connect
            |> Sql.query "Select * From tenty.detail_list_entries dle Where camp_details_id = @camp_id"
            |> Sql.parameters ["@camp_id", Sql.int camp_id]
            |> Sql.executeAsync (fun read ->
                {
                    Id = read.int "id"
                    CampDetailsId = read.int "camp_details_id"
                    Title = read.string "title"
                    Content = read.text "content"
                    CreatedAt = read.datetimeOffsetOrNone "created_at"
                    UpdatedAt = read.datetimeOffsetOrNone "updated_at"
                 }
                )
        return Ok rows
    with
    | ex -> return Error $"Database error: {ex.Message}"
}
    
let getCampZonesForCamp (camp_id: int, isDelimitation: bool): Task<Result<DbCampZone list, string>> = task {
    try
        let! rows =
            connectionString
            |> Sql.connect
            |> Sql.query "Select cz.id, cz.camp_id, cz.name, cz.is_camp_delimitation, cz.color, ST_AsGeoJSON(cz.geom,6) AS geojson, cz.created_at, cz.updated_at  From tenty.camp_zones cz where cz.camp_id = @camp_id AND cz.is_camp_delimitation = @is_delimitation;"
            |> Sql.parameters ["@camp_id", Sql.int camp_id; "@is_delimitation", Sql.bool isDelimitation ]
            |> Sql.executeAsync (fun read ->
                {
                    Id = read.int "id"
                    CampId = read.int "camp_id"
                    Name = read.string "name"
                    IsCampDelimitation = read.bool "is_camp_delimitation"
                    Color = read.string "color"
                    Zone = read.text "geojson"
                    CreatedAt = read.datetimeOffsetOrNone "created_at"
                    UpdatedAt = read.datetimeOffsetOrNone "updated_at"
                 }
                )
        return
            match rows with
            | [] when isDelimitation -> Error $"No CampZone for Camp with id {camp_id} found"
            | [] -> Ok []
            | _ -> Ok rows
        
    with
    | ex -> return Error $"Database error: {ex.Message}"
}
    
let getShownMaps (camp_id: int): Task<Result<DbShownMap list, string>> = task {
    try
        let! rows =
            connectionString
            |> Sql.connect
            |> Sql.query "select * from tenty.shown_maps sm where camp_id = @camp_id;"
            |> Sql.parameters ["@camp_id", Sql.int camp_id]
            |> Sql.executeAsync (fun read ->
                {
                    Id = read.int "id"
                    CampId = read.int "camp_id"
                    MapName = read.string "map_name"
                    CreatedAt = read.datetimeOffsetOrNone "created_at"
                    UpdatedAt = read.datetimeOffsetOrNone "updated_at"
                 }
                )
        return Ok rows
    with
    | ex -> return Error $"Database error: {ex.Message}"
}

// Add new DbUser (returns inserted id) - accept NewDbUserDto
let addNewDbUser (dto: NewDbUserDto) : Task<Result<int, string>> = task {
    try
        let! ids =
            connectionString
            |> Sql.connect
            |> Sql.query "INSERT INTO tenty.users (first_name, middle_name, last_name, email) VALUES (@first_name, @middle_name, @last_name, @email) RETURNING id"
            |> Sql.parameters [ "@first_name", Sql.string dto.FirstName; "@middle_name", Sql.stringOrNone dto.MiddleName; "@last_name", Sql.string dto.LastName; "@email", Sql.string dto.Email ]
            |> Sql.executeAsync (fun read -> read.int "id")
        return
            match ids |> List.tryHead with
            | Some id -> Ok id
            | None -> Error "Insert did not return an id"
    with
    | ex -> return Error $"Database error: {ex.Message}"
}

// Add a new camp (returns inserted id). Accept NewDbCampDto
let addNewDbCamp (dto: NewDbCampDto) : Task<Result<int, string>> = task {
    try
        let! ids =
            connectionString
            |> Sql.connect
            |> Sql.query "INSERT INTO tenty.camps (owner, camp_name, loc, is_sub_camp, parent_id) VALUES (@owner, @camp_name, ST_SetSRID(ST_GeomFromText(@location), 4326), @is_sub_camp, @parent_id) RETURNING id"
            |> Sql.parameters [ "@owner", Sql.int dto.OwnerId; "@camp_name", Sql.string dto.CampName; "@location", Sql.text dto.Location; "@is_sub_camp", Sql.bool dto.IsSubCamp; "@parent_id", Sql.intOrNone dto.ParentId ]
            |> Sql.executeAsync (fun read -> read.int "id")
        return
            match ids |> List.tryHead with
            | Some id -> Ok id
            | None -> Error "Insert did not return an id"
    with
    | ex -> return Error $"Database error: {ex.Message}"
}

// Add new CampZone (returns inserted id). Accept NewDbCampZoneDto
let addNewDbCampZone (dto: NewDbCampZoneDto) : Task<Result<int, string>> = task {
    try
        let! ids =
            connectionString
            |> Sql.connect
            |> Sql.query "INSERT INTO tenty.camp_zones (camp_id, name, is_camp_delimitation, color, geom) VALUES (@camp_id, @name, @is_delimitation, @color, ST_SetSRID(ST_GeomFromText(@zone), 4326)) RETURNING id"
            |> Sql.parameters [ "@camp_id", Sql.int dto.CampId
                                "@name", Sql.string dto.Name
                                "@is_delimitation", Sql.bool dto.IsCampDelimitation
                                "@color", Sql.stringOrNone dto.Color
                                "@zone", Sql.text dto.Zone ]
            |> Sql.executeAsync (fun read -> read.int "id")
        return
            match ids |> List.tryHead with
            | Some id -> Ok id
            | None -> Error "Insert did not return an id"
    with
    | ex -> return Error $"Database error: {ex.Message}"
}

let updateDbCampZone (dbZone: DbCampZone) : Task<Result<int, string>> = task {
    try
        let! affected =
            connectionString
            |> Sql.connect
            |> Sql.query "UPDATE tenty.camp_zones SET camp_id = @camp_id, name = @name, is_camp_delimitation = @is_delimitation, color = @color, geom = ST_SetSRID(ST_GeomFromText(@zone), 4326), updated_at = NOW() WHERE id = @id"
            |> Sql.parameters [ "@camp_id", Sql.int dbZone.CampId; "@name", Sql.string dbZone.Name; "@is_delimitation", Sql.bool dbZone.IsCampDelimitation; "@color", Sql.string dbZone.Color; "@zone", Sql.text dbZone.Zone; "@id", Sql.int dbZone.Id ]
            |> Sql.executeNonQueryAsync
        return
            match affected with
            | 0 -> Error $"CampZone with id {dbZone.Id} not found"
            | n -> Ok n
    with
    | ex -> return Error $"Database error: {ex.Message}"
}

// Add new CampUser (returns affected rows or inserted composite key). Many schemas don't return id for join table; return affected rows.
let addNewDbCampUser (dto: NewDbCampUserDto) : Task<Result<int, string>> = task {
    try
        let! affected =
            connectionString
            |> Sql.connect
            |> Sql.query "INSERT INTO tenty.camp_users (user_id, camp_id, user_permission) VALUES (@user_id, @camp_id, @permission)"
            |> Sql.parameters [ "@user_id", Sql.int dto.UserId; "@camp_id", Sql.int dto.CampId; "@permission", Sql.int (int dto.UserPermission) ]
            |> Sql.executeNonQueryAsync
        return Ok affected
    with
    | ex -> return Error $"Database error: {ex.Message}"
}

let updateDbCampUser (dbCampUser: DbCampUser) : Task<Result<int, string>> = task {
    try
        let! affected =
            connectionString
            |> Sql.connect
            |> Sql.query "UPDATE tenty.camp_users SET user_permission = @permission, updated_at = NOW() WHERE user_id = @user_id AND camp_id = @camp_id"
            |> Sql.parameters [ "@permission", Sql.int (int dbCampUser.UserPermission); "@user_id", Sql.int dbCampUser.UserId; "@camp_id", Sql.int dbCampUser.CampId ]
            |> Sql.executeNonQueryAsync
        return
            match affected with
            | 0 -> Error $"CampUser entry for user {dbCampUser.UserId} and camp {dbCampUser.CampId} not found"
            | n -> Ok n
    with
    | ex -> return Error $"Database error: {ex.Message}"
}

let addNewDbCampDetail (dto: NewDbCampDetailDto) : Task<Result<int, string>> = task {
    try
        let! ids =
            connectionString
            |> Sql.connect
            |> Sql.query "INSERT INTO tenty.camp_details (camp_id, start_date, end_date, description, city) VALUES (@camp_id, @start_date, @end_date, @description, @city) RETURNING id"
            |> Sql.parameters [ "@camp_id", Sql.int dto.CampId; "@start_date", Sql.timestamp dto.StartDate.UtcDateTime; "@end_date", Sql.timestamp dto.EndDate.UtcDateTime; "@description", Sql.textOrNone dto.Description; "@city", Sql.stringOrNone dto.City ]
            |> Sql.executeAsync (fun read -> read.int "id")
        return
            match ids |> List.tryHead with
            | Some id -> Ok id
            | None -> Error "Insert did not return an id"
    with
    | ex -> return Error $"Database error: {ex.Message}"
}

let updateDbCampDetail (dbDetail: DbCampDetail) : Task<Result<int, string>> = task {
    try
        let! affected =
            connectionString
            |> Sql.connect
            |> Sql.query "UPDATE tenty.camp_details SET start_date = @start_date, end_date = @end_date, description = @description, city = @city, updated_at = NOW() WHERE id = @id"
            |> Sql.parameters [ "@start_date", Sql.timestamp dbDetail.StartDate.UtcDateTime; "@end_date", Sql.timestamp dbDetail.EndDate.UtcDateTime; "@description", Sql.textOrNone dbDetail.Description; "@city", Sql.string dbDetail.City; "@id", Sql.int dbDetail.Id ]
            |> Sql.executeNonQueryAsync
        return
            match affected with
            | 0 -> Error $"CampDetail with id {dbDetail.Id} not found"
            | n -> Ok n
    with
    | ex -> return Error $"Database error: {ex.Message}"
}


// Transaction to add full camp. Aborts if there is an error on any part of the transaction:
    //camps: (owner, campName, location, is_sub_camp, parent_id option, )
    //camp_zones: (camp_id, name(evtl. campDelimitation), is_camp_deli, color(basecolor), geometry),
    //camp_details (camp_id, start, end, descirption, city,
    // shownMaps (evtl. Default eintrag.)
    //camp_zones: äquivalent zu delimitation 
    //owner: (id) check ob owner richtiger User (evtl weglassen da user authenticated von controller)

// Private Helper function to insert into DB to allow rerolls.
let private insertCamp (conn: NpgsqlConnection) (camp: NewDbCampDto) = task {
    let! ids =
        conn
        |> Sql.existingConnection
        |> Sql.query "
            INSERT INTO tenty.camps (
                owner, camp_name, loc, is_sub_camp, parent_id
            )
            VALUES (
                @owner,
                @camp_name,
                ST_SetSRID(ST_GeomFromText(@location), 4326),
                @is_sub_camp,
                @parent_id
            )
            RETURNING id"
        |> Sql.parameters [
            "@owner",      Sql.int camp.OwnerId
            "@camp_name",  Sql.string camp.CampName
            "@location",   Sql.text camp.Location
            "@is_sub_camp",Sql.bool camp.IsSubCamp
            "@parent_id",  Sql.intOrNone camp.ParentId
        ]
        |> Sql.executeAsync (fun read -> read.int "id")

    match List.tryHead ids with
    | Some id -> return id
    | None    -> return failwith "Insert camp did not return an id"
}

let private insertCampDetail (conn: NpgsqlConnection) (campId: int) (detail: NewDbCampDetailDto) = task {
    let! ids =
        conn
        |> Sql.existingConnection
        |> Sql.query "
            INSERT INTO tenty.camp_details (
                camp_id, start_date, end_date, description, city
            )
            VALUES (
                @camp_id,
                @start_date,
                @end_date,
                @description,
                @city
            )
            RETURNING id"
        |> Sql.parameters [
            "@camp_id",    Sql.int campId
            "@start_date", Sql.timestamp detail.StartDate.DateTime
            "@end_date",   Sql.timestamp detail.EndDate.DateTime
            "@description",Sql.textOrNone detail.Description
            "@city",       Sql.stringOrNone detail.City
        ]
        |> Sql.executeAsync (fun read -> read.int "id")

    match List.tryHead ids with
    | Some _ -> return ()
    | None   -> return failwith "Insert camp_detail did not return an id"
}

let private insertCampZones (conn: NpgsqlConnection) (campId: int) (zones: NewDbCampZoneDto list) = task {
    for zone in zones do
        let! affected =
            conn
            |> Sql.existingConnection
            |> Sql.query "
                INSERT INTO tenty.camp_zones (
                    camp_id, name, is_camp_delimitation, color, geom
                )
                VALUES (
                    @camp_id,
                    @name,
                    @is_delimitation,
                    @color,
                    ST_SetSRID(ST_GeomFromText(@zone), 4326)
                )"
            |> Sql.parameters [
                "@camp_id",        Sql.int campId
                "@name",           Sql.string zone.Name
                "@is_delimitation",Sql.bool zone.IsCampDelimitation
                "@color",          Sql.stringOrNone zone.Color
                "@zone",           Sql.string zone.Zone
            ]
            |> Sql.executeNonQueryAsync

        // Optional, falls du stille Fehler vermeiden willst:
        if affected <> 1 then
            failwithf "Insert camp_zone affected %d rows (expected 1)" affected
}

let private insertShownMaps (conn: NpgsqlConnection) (campId: int) (maps: NewDbShownMapsDto list) = task {
    for sm in maps do
        let! affected =
            conn
            |> Sql.existingConnection
            |> Sql.query "
                INSERT INTO tenty.shown_maps (
                    camp_id, map_name
                )
                VALUES (
                    @camp_id,
                    @map_name
                )"
            |> Sql.parameters [
                "@camp_id",  Sql.int campId
                "@map_name", Sql.string sm.MapName
            ]
            |> Sql.executeNonQueryAsync

        // Optional
        if affected <> 1 then
            failwithf "Insert shown_map affected %d rows (expected 1)" affected
}

// Transaction function to add new camp. Aborts transaction if any of the steps fail.
let addNewCamp (newCamp: NewDbFullCampDto) : Task<Result<int, string>> = task {
    use conn = new NpgsqlConnection(connectionString)
    do! conn.OpenAsync()

    use! tran = conn.BeginTransactionAsync()
    try
        // Create camp and get id if successfully.
        let! campId = insertCamp conn newCamp.CampDto

        // Create camp detail. Domain ensures its always existing.
        do! insertCampDetail conn campId newCamp.CampDetails

        // Create camp zones. At least the delimitation is in there otherwise the code fails before.
        do! insertCampZones conn campId newCamp.CampZones

        // Create show maps. Not used atm can be implemented. No shown maps means there is nothing for it. 
        //do! insertShownMaps conn campId newCamp.

        // 5) Commit
        do! tran.CommitAsync()
        return Ok campId
    with ex ->
        try
            do! tran.RollbackAsync()
        with _ -> ()
        return Error $"Database error: {ex.Message}"
}

         
    



// Add new DetailListEntry (returns inserted id)
let addNewDbDetailListEntry (dto: DbDetailListEntry) : Task<Result<int, string>> = task {
    try
        let! ids =
            connectionString
            |> Sql.connect
            |> Sql.query "INSERT INTO tenty.detail_list_entries (camp_details_id, title, content) VALUES (@camp_details_id, @title, @content) RETURNING id"
            |> Sql.parameters [ "@camp_details_id", Sql.int dto.CampDetailsId; "@title", Sql.string dto.Title; "@content", Sql.text dto.Content ]
            |> Sql.executeAsync (fun read -> read.int "id")
        return
            match ids |> List.tryHead with
            | Some id -> Ok id
            | None -> Error "Insert did not return an id"
    with
    | ex -> return Error $"Database error: {ex.Message}"
}

let updateDbDetailListEntry (entry: DbDetailListEntry) : Task<Result<int, string>> = task {
    try
        let! affected =
            connectionString
            |> Sql.connect
            |> Sql.query "UPDATE tenty.detail_list_entries SET camp_details_id = @camp_details_id, title = @title, content = @content, updated_at = NOW() WHERE id = @id"
            |> Sql.parameters [ "@camp_details_id", Sql.int entry.CampDetailsId; "@title", Sql.string entry.Title; "@content", Sql.text entry.Content; "@id", Sql.int entry.Id ]
            |> Sql.executeNonQueryAsync
        return
            match affected with
            | 0 -> Error $"DetailListEntry with id {entry.Id} not found"
            | n -> Ok n
    with
    | ex -> return Error $"Database error: {ex.Message}"
}
    



// DB Functions used by UserController to modify RefreshTokens

let getRefreshToken (token: RefreshTokenDTO) : Task<Result<DbRefreshToken, string>> = task {
    try

        let! rows =
            connectionString
            |> Sql.connect
            |> Sql.query "SELECT user_id, token_hash, expires_at From tenty.refresh_tokens Where token_hash = @token_hash"
            |> Sql.parameters ["@token_hash", Sql.string token.Token]
            |> Sql.executeAsync (fun read -> {
                Token = read.text "token_hash"
                UserId = read.int "user_id"
                ExpiresAt = read.dateTime "expires_at"
            })
        return
            match rows with
            | [] -> Error "Refresh token not found"
            | [single] -> Ok single
            | _ -> Error "Multiple refresh tokens found (data integrity issue)"
    with
    | ex -> return Error $"Database error: {ex.Message}"
}
    
let deleteRefreshToken (token: RefreshTokenDTO) : Task<Result<int, string>> = task {
    try
        let! affectedRows =
            connectionString
            |> Sql.connect
            |> Sql.query "DELETE FROM tenty.refresh_tokens WHERE token_hash = @token_hash;"
            |> Sql.parameters ["@token_hash", Sql.string token.Token]
            |> Sql.executeNonQueryAsync
        return
            match affectedRows with
            | 0 -> Error "Token was not found."
            | 1 -> Ok 1
            | _ -> Error "Multiple refresh tokens deleted (data integrity issue)"
    with
    | ex -> return Error $"Database error: {ex.Message}"
}
    
let deleteRefreshTokenByUserId (user :User) : Task<Result<int, string>> = task {
    try
        let! affectedRows =
            connectionString
            |> Sql.connect
            |> Sql.query "DELETE FROM tenty.refresh_tokens WHERE user_id = @user_id;"
            |> Sql.parameters ["@user_id", Sql.int user.Id]
            |> Sql.executeNonQueryAsync
        return
            match affectedRows with
            | 0 -> Ok 0
            | 1 -> Ok 1
            | _ -> Error "Multiple refresh tokens deleted (data integrity issue)"
    with
    | ex -> return Error $"Database error: {ex.Message}"
}
   
let addRefreshToken (token: DbRefreshToken) : Task<Result<int, string>> = task {
    try
        let! affectedRows =
            connectionString
            |> Sql.connect
            |> Sql.query "INSERT INTO tenty.refresh_tokens (user_id, token_hash, expires_at) VALUES (@user_id, @token_hash, @expires_at);"
            |> Sql.parameters ["@user_id", Sql.int token.UserId; "@token_hash", Sql.string token.Token; "@expires_at", Sql.date token.ExpiresAt ]
            |> Sql.executeNonQueryAsync
        return
            match affectedRows with
            | 0 -> Error "Token wasn't added."
            | 1 -> Ok 1
            | _ -> Error "Multiple tokens were added."
    with
    | ex -> return Error $"Database error: {ex.Message}"
}
