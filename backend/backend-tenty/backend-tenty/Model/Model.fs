/// Domain and data-transfer models used throughout the backend services.
module backend_tenty.Model.Types

open System
open System.Collections.Generic
open System.Drawing
open System.Net.Mail
open backend_tenty.Service.GeoJsonService

/// Configuration parameters for JWT generation and validation.
type TokenConfig = {
    Issuer: string
    Audience: string
    SigningKey: string
    AccessTtl: TimeSpan
    RefreshTtl: TimeSpan
    PermVersion: int option
}

/// String type constrained to at most 50 characters.
type String50 = private String50 of string

/// Utilities for creating and unwrapping ``String50`` values.
module String50 =
    /// Attempts to build a ``String50`` ensuring the value is non-empty and short enough.
    let create (input: string) =
        if String.IsNullOrEmpty(input) || input.Length > 50 then
            None
        else
            Some (String50 input)

    /// Extracts the underlying string from a ``String50``.
    let value (String50 s) = s

    /// Converts an optional ``String50`` to an optional string for serialization.
    let toStringOption (opt: String50 option) : string option =
        Option.map ( fun (String50 s) -> s) opt


    /// Wraps a ``String50`` in ``Some`` to simplify pipeline composition.
    let toOpString (String50 s) = Some s

/// Structured name components captured for a user.
type NameInformation = {
    FirstName : String50
    MiddleName: String50 option
    LastName: String50
}

/// Wrapper for validated email addresses.
type MailInformation = {
    MailAddress : MailAddress
    
}

/// Domain representation of a user account.
type User = {
    Id: int
    NameInformation : NameInformation
    MailInformation : MailInformation
}

/// Permission levels assigned to users per camp.
type UserPermission =
    | Read
    | Write
    | Admin

/// Links a ``User`` to a camp together with the granted permission.
type CampUser = {
    User: User
    Permission: UserPermission
}
/// Entry for a bulleted detail list inside camp descriptions.
type DetailListEntry = {
    Title: String50
    Content: String 
}

/// Additional descriptive data attached to a camp.
type CampDetail = {
    StartDate: DateTimeOffset
    EndDate: DateTimeOffset
    Description: string option
    City: string
    DetailList: DetailListEntry list option
}

/// Geographic zone belonging to a camp, optionally serving as the delimitation.
type CampZone = {
    Name: String50
    Color: Color
    IsDelimitation: bool
    Delimitation: GeoJsonFeature
}

/// Aggregate domain object for a camp including zones and details.
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

/// Summary projection of a user's permission for a camp.
type CampPermissionSummary = {
    Id: int
    CampName: string
    OwnerId: int
    Permission: UserPermission
}

/// DTO to create a subcamp using a name and delimitation.
type CreateSubCampDto = {
    Name: String50
    Delimitation: CampZone
}

/// Partial update payload for camp resources.
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


/// Database model mirroring the user table schema.
type DbUser = {
    Id: int
    FirstName: string
    MiddleName: string option
    LastName: string
    Email: string
    CreatedAt: DateTimeOffset option
    UpdatedAt: DateTimeOffset option
}

/// Database model for the camp table schema.
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

/// Permission flag encoding stored in the database.
type DbUserPermission =
    | Read = 0
    | Write = 1
    | Admin = 2

/// Database representation of the camp-user join table.
type DbCampUser = {
    UserId: int
    CampId: int
    UserPermission: DbUserPermission
    CreatedAt: DateTimeOffset option
    UpdatedAt: DateTimeOffset option
}

/// Database record storing detailed camp metadata.
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

/// Database row representing a single entry in a camp's detail list.
type DbDetailListEntry = {
    Id: int
    CampDetailsId: int
    Title: string
    Content: string
    CreatedAt: DateTimeOffset option
    UpdatedAt: DateTimeOffset option
}

/// Database model for camp zone shapes and styling.
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

/// Database model for maps configured to be shown for a camp.
type DbShownMap = {
    Id: int
    CampId: int
    MapName: string
    CreatedAt: DateTimeOffset option
    UpdatedAt: DateTimeOffset option
}


/// DTO for transferring user details to the frontend.
type UserDto = {
    Id: int
    FirstName: string
    MiddleName: string option
    LastName: string
    Email: string
}

/// DTO combining a user with a permission flag for API responses.
type CampUserDto = {
    User: UserDto
    Permission: int
}

/// DTO describing a camp zone to clients, including geometry.
type CampZoneDto = {
    Name: string
    IsCampDelimitation: bool
    Color: string // In hex
    Zone: GeoJsonFeatureDto
}

/// DTO for a single detail list entry in camp metadata.
type DetailListEntryDto = {
    Title: string
    Content: string
}

/// DTO describing camp-specific details for transfer to clients.
type CampDetailDto = {
    StartDate: string
    EndDate: string
    Description: string option
    City: string
    DetailList: DetailListEntryDto list option
}

/// Lightweight DTO representing a camp record.
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

/// Comprehensive camp DTO including nested zones and user permissions.
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

/// Payload for creating a new camp via the API.
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

/// Payload for updating an existing camp via the API.
type UpdateCampDto = {
    Name: string
    Location: GeoJsonFeatureDto
    Delimitation: CampZoneDto
    CampDetail: CampDetailDto
    IsSubCamp: bool
    ParentId: int
    
}


/// DTO for inserting a new user row.
type NewDbUserDto = {
    FirstName: string
    MiddleName: string option
    LastName: string
    Email: string
}

/// DTO for inserting a new camp row.
type NewDbCampDto = {
    OwnerId: int
    CampName: string
    Location: string
    IsSubCamp: bool
    ParentId: int option
}

/// DTO for inserting a new camp zone row.
type NewDbCampZoneDto = {
    CampId: int
    Name: string
    IsCampDelimitation: bool
    Color: string option
    Zone: string
}

/// DTO for inserting a camp-user permission row.
type NewDbCampUserDto = {
    UserId: int
    CampId: int
    UserPermission: DbUserPermission
}

/// DTO for inserting camp detail records.
type NewDbCampDetailDto = {
    CampId: int
    StartDate: DateTimeOffset
    EndDate: DateTimeOffset
    Description: string option
    City: string option
}

/// DTO for inserting individual detail list entries.
type NewDbDetailListEntryDto = {
    CampDetailsId: int
    Title: string
    Content: string
}

/// DTO for inserting shown map references.
type NewDbShownMapsDto = {
    CampId: int
    MapName: string
}

/// Aggregate DTO bundling all camp creation pieces for a transaction.
type NewDbFullCampDto = {
    CampDto: NewDbCampDto
    CampZones: NewDbCampZoneDto list
    CampDetails: NewDbCampDetailDto
    CampDetailListEntries: NewDbDetailListEntryDto list option
    
}


/// DTO for login requests (currently email-only, extensible to OAuth or passwords).
[<CLIMutable>]
type LoginDTO = {
    MailAddress: string
}

/// Wrapper DTO for single-token responses.
type TokenDTO = {
    Token: string
}

/// Roles claim content decoded from JWTs.
type CampRoles = {
    Admin: int list
    Write: int list
    Read: int list
}

/// Access and refresh token pair along with expiration metadata.
type TokenPair = {
    AccessToken: string
    AccessExpiresAt: DateTime
    RefreshToken: string
    RefreshExpiresAt: DateTime
}

/// Persisted refresh token entry.
type DbRefreshToken = {
    Token: string
    UserId: int
    ExpiresAt: DateTime
}

/// Request payload carrying a refresh token.
type RefreshTokenDTO = {
    Token: string
} 
  
/// Known kinds of map features supported by the application.
type FeatureKind =
    | Tent
    | Toilet
    | Power
    | Water
    | Gate
    | Custom

/// Domain representation of a feature detail entry.
type FeatureDetail = {
    Id: int
    FeatureId: int
    Title: String50
    Content: string
    CreatedAt: DateTimeOffset option
    UpdatedAt: DateTimeOffset option
}

/// Domain representation of a map feature belonging to a camp.
type Feature = {
    Id: int
    CampId: int
    Kind: FeatureKind
    Name: String50 option
    Geometry: GeoJsonFeature
    HeadingDeg: double option
    LengthM: double option
    WidthM: double option
    Color: Color option
    Icon: String50 option
    Props: string option
    Details: FeatureDetail list option
    CreatedAt: DateTimeOffset option
    UpdatedAt: DateTimeOffset option
}

/// Database record for stored features.
type DbFeature = {
    Id: int
    CampId: int
    Kind: string
    Name: string option
    Geometry: string
    HeadingDeg: double option
    LengthM: double option
    WidthM: double option
    Color: string option
    Icon: string option
    Props: string option
    CreatedAt: DateTimeOffset option
    UpdatedAt: DateTimeOffset option
}

/// Database record for stored feature detail entries.
type DbFeatureDetail = {
    Id: int
    FeatureId: int
    Title: string
    Content: string
    CreatedAt: DateTimeOffset option
    UpdatedAt: DateTimeOffset option
}

/// DTO for exposing feature details to clients.
type FeatureDetailDto = {
    Id: int
    Title: string
    Content: string
    CreatedAt: DateTimeOffset option
    UpdatedAt: DateTimeOffset option
}

/// DTO representing a feature with optional nested details.
type FeatureDto = {
    Id: int
    CampId: int
    Kind: string
    Name: string option
    Geometry: GeoJsonFeatureDto
    HeadingDeg: double option
    LengthM: double option
    WidthM: double option
    Color: string option
    Icon: string option
    Props: string option
    Details: FeatureDetailDto list option
    CreatedAt: DateTimeOffset option
    UpdatedAt: DateTimeOffset option
}

/// DTO used to create a new feature.
type CreateFeatureDto = {
    CampId: int
    Kind: string
    Name: string option
    Geometry: GeoJsonFeatureDto
    HeadingDeg: double option
    LengthM: double option
    WidthM: double option
    Color: string option
    Icon: string option
    Props: string option
    Details: FeatureDetailDto list option
}

/// DTO for inserting a new feature record.
type NewDbFeatureDto = {
    CampId: int
    Kind: string
    Name: string option
    Geometry: string
    HeadingDeg: double option
    LengthM: double option
    WidthM: double option
    Color: string option
    Icon: string option
    Props: string option
}

/// DTO for inserting a new feature detail record.
type NewDbFeatureDetailDto = {
    FeatureId: int
    Title: string
    Content: string
}

/// Extracts the raw string from a ``String50`` value.
let string50Value (s: String50) = String50.value s

/// Maps an optional ``String50`` into an optional string.
let string50ValueOpt (opt: String50 option) : string option = Option.map String50.value opt

/// Converts a ``MailAddress`` to its string representation.
let mailAddressToString (m: MailAddress) = m.Address

/// Converts a ``FeatureKind`` discriminated union into its database string form.
let featureKindToString (kind: FeatureKind) : string =
    match kind with
    | FeatureKind.Tent -> "tent"
    | FeatureKind.Toilet -> "toilet"
    | FeatureKind.Power -> "power"
    | FeatureKind.Water -> "water"
    | FeatureKind.Gate -> "gate"
    | FeatureKind.Custom -> "custom"

/// Attempts to parse a feature kind string into the corresponding union case.
let featureKindFromString (kind: string) : Result<FeatureKind, string> =
    match kind.ToLowerInvariant() with
    | "tent" -> Ok FeatureKind.Tent
    | "toilet" -> Ok FeatureKind.Toilet
    | "power" -> Ok FeatureKind.Power
    | "water" -> Ok FeatureKind.Water
    | "gate" -> Ok FeatureKind.Gate
    | "custom" -> Ok FeatureKind.Custom
    | other -> Error $"Unknown feature kind: {other}"

/// Serializes a ``JsonElement`` back into a string.
let jsonElementToString (element: System.Text.Json.JsonElement) : string =
    System.Text.Json.JsonSerializer.Serialize(element)

/// Serializes an optional ``JsonElement`` back into an optional string.
let jsonElementToStringOption (element: System.Text.Json.JsonElement option) : string option =
    element |> Option.map jsonElementToString