module backend_tenty.Model.Conversion.DBConversions

open System
open System.Drawing
open System.Net.Mail
open Npgsql.FSharp
open backend_tenty.Model.Types
open backend_tenty.Model.Types.String50
open backend_tenty.Model.Validation.ValidateDbData
open backend_tenty.Service.GeoJsonService
open backend_tenty.Service.ResultExt

        //Used to translate Permission stored as int into fsharp type. Since there are only entries for user
    //with permission, the default case is Read.
let validateDbCampUserPermission (permission: int) : DbUserPermission =
    match permission with
    | 2 -> DbUserPermission.Admin
    | 1 -> DbUserPermission.Write
    | _ -> DbUserPermission.Read
    
    //Append admin Permission for missing integer. Only case where this happens is when Checked user is owner.
let validateUserPermission (permission: int option) :UserPermission =
    match permission with
    | Some 2 -> UserPermission.Admin
    | Some 1 -> UserPermission.Write
    | Some _ -> UserPermission.Read
    | None -> UserPermission.Admin
    
let userPermissionToInt (permission: UserPermission) : int =
    match permission with
    | UserPermission.Admin -> 2
    | UserPermission.Write -> 1
    | UserPermission.Read -> 0
    
let dbUserPermissionToUserPermission (dbPermission: DbUserPermission): UserPermission =
    match dbPermission with
    | DbUserPermission.Admin -> UserPermission.Admin
    | DbUserPermission.Read -> UserPermission.Read
    | DbUserPermission.Write -> UserPermission.Write


// Functions used to transfer DB Records to Domain records:

let dbUserToDomain (db: DbUser) : Result<User, string> =
    match validateName db.FirstName db.MiddleName db.LastName with
    | Error e -> Error e
    | Ok nameInfo ->
        match validateMail db.Email with
        | Error e -> Error e
        | Ok mailInfo ->
            Ok { Id = db.Id
                 NameInformation = nameInfo
                 MailInformation = mailInfo
               }
            
let dbCampUserToDomain (dbCampUser: DbCampUser, user: DbUser) : Result<CampUser, string> =
    match (dbCampUser.UserId, user.Id) with
    | (x, y) when x <> y -> Error "Cant create CampUser from different user inputs."
    | (x, y) when x = y ->
        let permission = dbUserPermissionToUserPermission dbCampUser.UserPermission
        match dbUserToDomain user with
        | Error e -> Error $"Error while parsing User: {e}"
        | Ok user -> Ok (
                         {
                             User = user
                             Permission = permission                
                         }
                         )
    | (_, _) -> Error "Error while parsing CampUser: Missing Input"
    

let domainUserToDb (domain: User) : DbUser =
    {
        Id = domain.Id
        FirstName = string50Value domain.NameInformation.FirstName
        MiddleName = string50ValueOpt domain.NameInformation.MiddleName
        LastName = string50Value domain.NameInformation.LastName
        Email = mailAddressToString domain.MailInformation.MailAddress
        CreatedAt = None
        UpdatedAt = None        
    }

let parseHtmlColor (color: string) : Result<Color, string> =
    if System.String.IsNullOrWhiteSpace color then
        Error "Color cant be empty."
    else
        try
            Ok(ColorTranslator.FromHtml color)
        with
        | :? System.ArgumentException as ex -> Error $"Invalid color format: {ex.Message}"
        | ex -> Error $"Error parsing color: {ex.Message}"
    
let dbCampZoneToDomain (campZone: DbCampZone) : Result<CampZone, string> =
    result {
    let! campZoneName =
        match String50.create campZone.Name with
        | Some name -> Ok name
        | None -> Error "No name for Campzone found."
    let delimitation = campZone.Zone
                        |> geometryFromJson
                        |> geometryToFeature
        
        
    let! color =
        parseHtmlColor campZone.Color
        |> Result.mapError (fun e -> $"No valid Color: {e}")
    
    return {
        Name = campZoneName
        Color = color
        IsDelimitation = campZone.IsCampDelimitation
        Delimitation = delimitation
    }
    }

let dbDetailListEntryToDomain (detailEntry: DbDetailListEntry) : Result<DetailListEntry, string> =
        match String50.create detailEntry.Title with
        | None -> Error "No title in Detail list entry: "
        | Some title -> Ok {
                Title = title
                Content = detailEntry.Content
            }
    
let dbCampDetailsToDomain (campDetail: DbCampDetail) (detailListEntries: DbDetailListEntry list) : Result<CampDetail, string> =
    let detailList = List.map dbDetailListEntryToDomain detailListEntries |> sequenceResultsList
    match detailList with
        | Error e -> Error $"Error while parsing DetailListEntry: {e}"
        | Ok list ->
            Ok {
        StartDate = campDetail.StartDate
        EndDate = campDetail.EndDate
        Description = campDetail.Description
        City = campDetail.City
        DetailList = Some list
        }
            
let dbShownMapsToDomain (shownMaps: DbShownMap list) : Result<String50 list, string> =
    shownMaps
    |> List.map (fun mapName -> 
        match String50.create mapName.MapName with
        | None -> Error "Error while parsing shown maps:"
        | Some s -> Ok s
    )
    |> sequenceResultsList
    
