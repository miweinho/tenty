module backend_tenty.Model.Types

open System
open System.Collections.Generic
open System.Drawing
open System.Net.Mail
open backend_tenty.Service.GeoJsonService

//Config Models:

type TokenConfig = {
    Issuer: string
    Audience: string
    SigningKey: string
    AccessTtl: TimeSpan
    RefreshTtl: TimeSpan
    PermVersion: int option
}

type String50 = private String50 of string

module String50 =
    let create (input: string) =
        if String.IsNullOrEmpty(input) || input.Length > 50 then
            None
        else
            Some (String50 input)
    
    let value (String50 s) = s
    
    let toStringOption (opt: String50 option) : string option =
        Option.map ( fun (String50 s) -> s) opt
        
    
    let toOpString (String50 s) = Some s

type NameInformation = {
    FirstName : String50
    MiddleName: String50 option
    LastName: String50
}

type MailInformation = {
    MailAddress : MailAddress
    
}

type User = {
    Id: int
    NameInformation : NameInformation
    MailInformation : MailInformation
}

type UserPermission =
    | Read
    | Write
    | Admin
    
type CampUser = {
    User: User
    Permission: UserPermission
}
type DetailListEntry = {
    Title: String50
    Content: String 
}

type CampDetail = {
    StartDate: DateTimeOffset
    EndDate: DateTimeOffset
    Description: string option
    City: string
    DetailList: DetailListEntry list option
}

type CampZone = {
    Name: String50
    Color: Color
    IsDelimitation: bool
    Delimitation: GeoJsonFeature
}

type Camp = {
    Id: int
    Owner: User
    Name: String50
    Location: GeoJsonFeature
    UserList: CampUser list
    Delimitation: CampZone
    CampDetail: CampDetail
    IsSubCamp: Boolean
    SubCamp: Camp list option
    ShownMaps: String50  list
    CampZones: CampZone list
}

type CampPermissionSummary = {
    Id: int
    CampName: string
    OwnerId: int
    Permission: UserPermission
}

// DTO to create subcamp. Using only a name and delimitation inherits other values from parent camp.
type CreateSubCampDto = {
    Name: String50
    Delimitation: CampZone
}

type PatchCampDto = {
    Id: int    
    owner: User option
    Name: String50 option
    Location: Geometry option
    UserList: CampUser List option
    Delimitation: CampZone option
    CampDetail: CampDetail option
    SubCamp: Camp List option
    ShownMaps: String50  List option
    CampZones: CampZone List option
    
}


//DB Models to ensure correct translation from Application to DB
type DbUser = {
    Id: int
    FirstName: string
    MiddleName: string option
    LastName: string
    Email: string
    CreatedAt: DateTimeOffset option
    UpdatedAt: DateTimeOffset option
}

type DbCamp = {
    Id: int
    OwnerId: int
    CampName: string
    Location: string
    IsSubCamp: bool
    ParentId: int option
    CreatedAt: DateTimeOffset option
    UpdatedAt: DateTimeOffset option
}

type DbUserPermission =
    | Read = 0
    | Write = 1
    | Admin = 2

type DbCampUser = {
    UserId: int
    CampId: int
    UserPermission: DbUserPermission
    CreatedAt: DateTimeOffset option
    UpdatedAt: DateTimeOffset option
}

type DbCampDetail = {
    Id: int
    CampId: int
    StartDate: DateTimeOffset
    EndDate: DateTimeOffset
    Description: string option
    City: string
    CreatedAt: DateTimeOffset option
    UpdatedAt: DateTimeOffset option
}

type DbDetailListEntry = {
    Id: int
    CampDetailsId: int
    Title: string
    Content: string
    CreatedAt: DateTimeOffset option
    UpdatedAt: DateTimeOffset option
}

type DbCampZone = {
    Id: int
    CampId: int
    Name: string
    IsCampDelimitation: bool
    Color: string
    Zone: string
    CreatedAt: DateTimeOffset option
    UpdatedAt: DateTimeOffset option
}

type DbShownMap = {
    Id: int
    CampId: int
    MapName: string
    CreatedAt: DateTimeOffset option
    UpdatedAt: DateTimeOffset option
}


// DTO für Transfer an Frontend:

type UserDto = {
    Id: int
    FirstName: string
    MiddleName: string option
    LastName: string
    Email: string
}

// New/Create DTOs for DB insert operations (no id / timestamps)
type CampUserDto = {
    User: UserDto
    Permission: int
}

type CampZoneDto = {
    Name: string
    IsCampDelimitation: bool
    Color: string // In hex
    Zone: GeoJsonFeatureDto
}

type DetailListEntryDto = {
    Title: string
    Content: string
}

type CampDetailDto = {
    StartDate: string
    EndDate: string
    Description: string option
    City: string
    DetailList: DetailListEntryDto list option
}

type CampDto = {
    Id: int
    OwnerId: int
    CampName: string
    Location: GeoJsonFeatureDto
    IsSubCamp: bool
    ParentId: int option
    CreatedAt: DateTimeOffset option
    UpdatedAt: DateTimeOffset option
}

type FullCampDto = {
    Id: int
    Owner: UserDto
    Name: string
    Location: GeoJsonFeatureDto
    UserList: CampUserDto list
    Delimitation: CampZoneDto
    CampDetail: CampDetailDto
    IsSubCamp: bool
    SubCamp: int list
    ShownMaps: string list
    CampZones: CampZoneDto list
}

type CreateCampDto = {
    Name: string
    Location: GeoJsonFeatureDto
    Delimitation: CampZoneDto
    CampDetail: CampDetailDto
    IsSubCamp: bool
    ParentId: int
    SubCamp: int list
    ShownMaps: string list option
    CampZones: CampZoneDto list option
}


type NewDbUserDto = {
    FirstName: string
    MiddleName: string option
    LastName: string
    Email: string
}

type NewDbCampDto = {
    OwnerId: int
    CampName: string
    Location: string
    IsSubCamp: bool
    ParentId: int option
}

type NewDbCampZoneDto = {
    CampId: int
    Name: string
    IsCampDelimitation: bool
    Color: string option
    Zone: string
}

type NewDbCampUserDto = {
    UserId: int
    CampId: int
    UserPermission: DbUserPermission
}

type NewDbCampDetailDto = {
    CampId: int
    StartDate: DateTimeOffset
    EndDate: DateTimeOffset
    Description: string option
    City: string option
}

type NewDbDetailListEntryDto = {
    CampDetailsId: int
    Title: string
    Content: string
}

type NewDbShownMapsDto = {
    CampId: int
    MapName: string
}

type NewDbFullCampDto = {
    CampDto: NewDbCampDto
    CampZones: NewDbCampZoneDto list
    CampDetails: NewDbCampDetailDto
    CampDetailListEntries: NewDbDetailListEntryDto list option
    
}


// Login DTO Momentan nur mailadresse wird in später ersetzt entweder durch anbindung an oauth
// oder durch speichern von password hash.

[<CLIMutable>]
type LoginDTO = {
    MailAddress: string
}

type TokenDTO = {
    Token: string
}

// Authentication Models
type CampRoles = {
    Admin: int list
    Write: int list
    Read: int list
}

type TokenPair = {
    AccessToken: string
    AccessExpiresAt: DateTime
    RefreshToken: string
    RefreshExpiresAt: DateTime
}

type DbRefreshToken = {
    Token: string
    UserId: int
    ExpiresAt: DateTime
}

type RefreshTokenDTO = {
    Token: string
}   
  



//helpers
let string50Value (s: String50) = String50.value s
let string50ValueOpt (opt: String50 option) : string option = Option.map String50.value opt
let mailAddressToString (m: MailAddress) = m.Address