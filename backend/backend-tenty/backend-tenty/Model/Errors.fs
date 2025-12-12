/// Shared error types used across services and controllers.
module backend_tenty.Model.Errors

open System.Threading.Tasks

/// General application error cases surfaced by the API.
type AppError =
    | DbError of string
    | NotFound of string
    | PermissionDenied of string
    | ValidationError of string
    | RequestEmpty
    | RequestInvalid
    | UserParseError of string
    | TokenGenerationError of string
    | Unknown of string
    
/// Error cases specific to camp operations.
type CampLoadError =
    | CampNotFound of int
    | DbError of string
    | PermissionDenied of string
    | ZonesMissing
    | DetailsMissing of string
    | MapsMissing of string
    | InvalidPropertyValue of string
    
/// Errors that can arise during authentication or token issuance.
type AuthError =
    | RequestEmpty
    | RequestInvalid
    | DbError of string
    | UserParseError of string
    | TokenGenerationError of string
    
/// Errors for feature CRUD operations.
type FeatureError =
    | FeatureNotFound of int
    | DbError of string
    | PermissionDenied of string
    | InvalidPropertyValue of string
    | GeometryInvalid of string


/// Safe ``Result`` error mapping that works across F# versions.
let mapError f = function
    | Ok v -> Ok v
    | Error e -> Error (f e)
    
/// Maps the error channel of a ``Task<Result<_,_>>``.
let mapTaskError (f: 'a -> 'b) (taskResult: Task<Result<'T, 'a>>) : Task<Result<'T, 'b>> =
    task {
        let! result = taskResult
        return mapError f result
    }

/// Converts camp-specific errors into general ``AppError`` values.
let fromCampLoadError (e: CampLoadError) : AppError =
    match e with
    | CampNotFound id -> AppError.NotFound (sprintf "Camp %d not found" id)
    | CampLoadError.DbError s -> AppError.DbError s
    | CampLoadError.PermissionDenied s -> AppError.PermissionDenied s
    | ZonesMissing -> AppError.Unknown "ZonesMissing"
    | DetailsMissing s -> AppError.Unknown s
    | MapsMissing s -> AppError.Unknown s
    | CampLoadError.InvalidPropertyValue s -> AppError.Unknown s

/// Converts authentication errors into ``AppError`` values.
let fromAuthError (e: AuthError) : AppError =
    match e with
    | RequestEmpty -> AppError.RequestEmpty
    | RequestInvalid -> AppError.RequestInvalid
    | AuthError.DbError s -> AppError.DbError s
    | UserParseError s -> AppError.UserParseError s
    | TokenGenerationError s -> AppError.TokenGenerationError s

/// Converts feature errors into ``AppError`` values.
let fromFeatureError (e: FeatureError) : AppError =
    match e with
    | FeatureNotFound id -> AppError.NotFound (sprintf "Feature %d not found" id)
    | FeatureError.DbError s -> AppError.DbError s
    | PermissionDenied s -> AppError.PermissionDenied s
    | InvalidPropertyValue s -> AppError.ValidationError s
    | GeometryInvalid s -> AppError.ValidationError s

/// Maps a camp result into the general ``AppError`` space.
let mapCampResult (r: Result<'a, CampLoadError>) : Result<'a, AppError> =
    mapError fromCampLoadError r

/// Maps an auth result into the general ``AppError`` space.
let mapAuthResult (r: Result<'a, AuthError>) : Result<'a, AppError> =
    mapError fromAuthError r

/// Maps a feature result into the general ``AppError`` space.
let mapFeatureResult (r: Result<'a, FeatureError>) : Result<'a, AppError> =
    mapError fromFeatureError r




