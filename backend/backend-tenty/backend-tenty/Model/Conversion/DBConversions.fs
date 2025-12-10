/// Converters between database DTOs and domain models.
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
open System.Text.Json

        //Used to translate Permission stored as int into fsharp type. Since there are only entries for user
    //with permission, the default case is Read.
/// Maps a stored integer permission into a database user permission DU.
let validateDbCampUserPermission (permission: int) : DbUserPermission =
    match permission with
    | 2 -> DbUserPermission.Admin
    | 1 -> DbUserPermission.Write
    | _ -> DbUserPermission.Read
    
    //Append admin Permission for missing integer. Only case where this happens is when Checked user is owner.
/// Maps an optional integer permission into a domain ``UserPermission``.
let validateUserPermission (permission: int option) :UserPermission =
    match permission with
    | Some 2 -> UserPermission.Admin
    | Some 1 -> UserPermission.Write
    | Some _ -> UserPermission.Read
    | None -> UserPermission.Admin
    
/// Converts a domain permission into its persisted integer form.
let userPermissionToInt (permission: UserPermission) : int =
    match permission with
    | UserPermission.Admin -> 2
    | UserPermission.Write -> 1
    | UserPermission.Read -> 0
    
/// Converts a database permission record into a domain permission value.
let dbUserPermissionToUserPermission (dbPermission: DbUserPermission): UserPermission =
    match dbPermission with
    | DbUserPermission.Admin -> UserPermission.Admin
    | DbUserPermission.Read -> UserPermission.Read
    | DbUserPermission.Write -> UserPermission.Write


// Functions used to transfer DB Records to Domain records:

/// Builds a domain ``User`` from persisted columns.
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
            
/// Combines a ``DbCampUser`` row with its ``DbUser`` to a domain ``CampUser``.
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
    

/// Converts a domain user into its database DTO form.
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

/// Parses an HTML color string into a ``Color`` value.
let parseHtmlColor (color: string) : Result<Color, string> =
    if System.String.IsNullOrWhiteSpace color then
        Error "Color cant be empty."
    else
        try
            Ok(ColorTranslator.FromHtml color)
        with
        | :? System.ArgumentException as ex -> Error $"Invalid color format: {ex.Message}"
        | ex -> Error $"Error parsing color: {ex.Message}"
    
/// Converts a camp zone row into the domain camp zone representation.
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

/// Converts a persisted detail list entry into the domain model.
let dbDetailListEntryToDomain (detailEntry: DbDetailListEntry) : Result<DetailListEntry, string> =
        match String50.create detailEntry.Title with
        | None -> Error "No title in Detail list entry: "
        | Some title -> Ok {
                Title = title
                Content = detailEntry.Content
            }
    
/// Combines camp details and related list entries into a domain ``CampDetail``.
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
            
/// Converts shown map rows into validated ``String50`` values.
let dbShownMapsToDomain (shownMaps: DbShownMap list) : Result<String50 list, string> =
    shownMaps
    |> List.map (fun mapName -> 
        match String50.create mapName.MapName with
        | None -> Error "Error while parsing shown maps:"
        | Some s -> Ok s
    )
    |> sequenceResultsList
    
/// Serializes a feature kind DU into its string database representation.
let featureKindToString (kind: FeatureKind) : string =
    match kind with
    | FeatureKind.Tent -> "tent"
    | FeatureKind.Toilet -> "toilet"
    | FeatureKind.Power -> "power"
    | FeatureKind.Water -> "water"
    | FeatureKind.Gate -> "gate"
    | FeatureKind.Custom -> "custom"

/// Parses a persisted feature kind string into the domain DU.
let featureKindFromString (kind: string) : Result<FeatureKind, string> =
    match kind.ToLowerInvariant() with
    | "tent" -> Ok FeatureKind.Tent
    | "toilet" -> Ok FeatureKind.Toilet
    | "power" -> Ok FeatureKind.Power
    | "water" -> Ok FeatureKind.Water
    | "gate" -> Ok FeatureKind.Gate
    | "custom" -> Ok FeatureKind.Custom
    | other -> Error $"Unsupported feature kind: {other}"

let private optionToResult errorMsg opt =
    match opt with
    | Some v -> Ok v
    | None -> Error errorMsg

let private optionMapResult (mapper: 'a -> Result<'b, string>) (value: 'a option) : Result<'b option, string> =
    match value with
    | Some v -> mapper v |> Result.map Some
    | None -> Ok None

let private geometryTextToFeature (geomText: string) : Result<GeoJsonFeature, string> =
    try
        geomText
        |> geometryFromJson
        |> geometryToFeature
        |> Ok
    with _ ->
        try
            geomText
            |> geometryFromWkt
            |> geometryToFeature
            |> Ok
        with ex -> Error $"Invalid geometry: {ex.Message}"

let private parsePropsJson (props: string option) : Result<JsonElement option, string> =
    match props with
    | None -> Ok None
    | Some p when String.IsNullOrWhiteSpace p -> Ok None
    | Some p ->
        try
            let doc = JsonDocument.Parse(p)
            Ok (Some (doc.RootElement.Clone()))
        with ex -> Error $"Invalid props json: {ex.Message}"
/// Converts a database feature row into the domain ``Feature`` model.
let dbFeatureToDomain (dbFeature: DbFeature) : Result<Feature, string> =
    result {
        let! kind = featureKindFromString dbFeature.Kind
        let! name = optionMapResult (fun n -> String50.create n |> optionToResult "Invalid feature name") dbFeature.Name
        let! icon = optionMapResult (fun i -> String50.create i |> optionToResult "Invalid icon value") dbFeature.Icon
        let! color =
            dbFeature.Color
            |> Option.map parseHtmlColor
            |> Option.map (Result.map Some)
            |> Option.defaultValue (Ok None)
        let! geometry = geometryTextToFeature dbFeature.Geometry

        return {
            Id = dbFeature.Id
            CampId = dbFeature.CampId
            Kind = kind
            Name = name
            Geometry = geometry
            HeadingDeg = dbFeature.HeadingDeg
            LengthM = dbFeature.LengthM
            WidthM = dbFeature.WidthM
            Color = color
            Icon = icon
            Props = dbFeature.Props
            Details = None
            CreatedAt = dbFeature.CreatedAt
            UpdatedAt = dbFeature.UpdatedAt
        }
    }

/// Converts a feature detail row into the domain representation.
let dbFeatureDetailToDomain (dbDetail: DbFeatureDetail) : Result<FeatureDetail, string> =
    result {
        let! title =
            dbDetail.Title
            |> String50.create
            |> optionToResult "Invalid feature detail title"

        return {
            Id = dbDetail.Id
            FeatureId = dbDetail.FeatureId
            Title = title
            Content = dbDetail.Content
            CreatedAt = dbDetail.CreatedAt
            UpdatedAt = dbDetail.UpdatedAt
        }
    }

/// Serializes a domain feature into a database DTO.
let domainFeatureToDb (feature: Feature) : DbFeature =
    {
        Id = feature.Id
        CampId = feature.CampId
        Kind = featureKindToString feature.Kind
        Name = feature.Name |> Option.map string50Value
        Geometry = featureToDbWkt feature.Geometry
        HeadingDeg = feature.HeadingDeg
        LengthM = feature.LengthM
        WidthM = feature.WidthM
        Color = feature.Color |> Option.map ColorTranslator.ToHtml
        Icon = feature.Icon |> Option.map string50Value
        Props = feature.Props
        CreatedAt = feature.CreatedAt
        UpdatedAt = feature.UpdatedAt
    }

/// Serializes a domain feature detail into a database DTO.
let domainFeatureDetailToDb (detail: FeatureDetail) : DbFeatureDetail =
    {
        Id = detail.Id
        FeatureId = detail.FeatureId
        Title = detail.Title |> string50Value
        Content = detail.Content
        CreatedAt = detail.CreatedAt
        UpdatedAt = detail.UpdatedAt
    }
    
/// Prepares an insert DTO for creating a new feature record.
let domainFeatureToNewDb (feature: Feature) : NewDbFeatureDto =
    {
        CampId = feature.CampId
        Kind = featureKindToString feature.Kind
        Name = feature.Name |> Option.map string50Value
        Geometry = feature.Geometry |> featureToDbWkt
        HeadingDeg = feature.HeadingDeg
        LengthM = feature.LengthM
        WidthM = feature.WidthM
        Color = feature.Color |> Option.map ColorTranslator.ToHtml
        Icon = feature.Icon |> toStringOption
        Props = feature.Props
    }
    
/// Prepares an insert DTO for creating a new feature detail tied to a feature id.
let featureDetailToNewDb (featureId: int) (detail: FeatureDetail) : NewDbFeatureDetailDto =
    {
        FeatureId = featureId
        Title = string50Value detail.Title
        Content = detail.Content
    }