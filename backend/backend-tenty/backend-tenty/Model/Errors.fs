module backend_tenty.Model.Errors

open System.Threading.Tasks
open Giraffe

// General App Errors 
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
    
//Errors for Camp operations:
type CampLoadError =
    | CampNotFound of int
    | DbError of string
    | PermissionDenied of string
    | ZonesMissing
    | DetailsMissing of string
    | MapsMissing of string
    | InvalidPropertyValue of string
    
// Errors for Auth operations:
type AuthError =
    | RequestEmpty
    | RequestInvalid
    | DbError of string
    | UserParseError of string
    | TokenGenerationError of string
    
type FeatureError =
    | FeatureNotFound of int
    | DbError of string
    | PermissionDenied of string
    | InvalidPropertyValue of string
    | GeometryInvalid of string


// sichere mapError (kompatibel mit allen F# Versionen)
let mapError f = function
    | Ok v -> Ok v
    | Error e -> Error (f e)
    
let mapTaskError (f: 'a -> 'b) (taskResult: Task<Result<'T, 'a>>) : Task<Result<'T, 'b>> =
    task {
        let! result = taskResult
        return mapError f result
    }

let fromCampLoadError (e: CampLoadError) : AppError =
    match e with
    | CampNotFound id -> AppError.NotFound (sprintf "Camp %d not found" id)
    | CampLoadError.DbError s -> AppError.DbError s
    | CampLoadError.PermissionDenied s -> AppError.PermissionDenied s
    | ZonesMissing -> AppError.Unknown "ZonesMissing"
    | DetailsMissing s -> AppError.Unknown s
    | MapsMissing s -> AppError.Unknown s
    | CampLoadError.InvalidPropertyValue s -> AppError.Unknown s

let fromAuthError (e: AuthError) : AppError =
    match e with
    | RequestEmpty -> AppError.RequestEmpty
    | RequestInvalid -> AppError.RequestInvalid
    | AuthError.DbError s -> AppError.DbError s
    | UserParseError s -> AppError.UserParseError s
    | TokenGenerationError s -> AppError.TokenGenerationError s

let fromFeatureError (e: FeatureError) : AppError =
    match e with
    | FeatureNotFound id -> AppError.NotFound (sprintf "Feature %d not found" id)
    | FeatureError.DbError s -> AppError.DbError s
    | PermissionDenied s -> AppError.PermissionDenied s
    | InvalidPropertyValue s -> AppError.ValidationError s
    | GeometryInvalid s -> AppError.ValidationError s

// Map From Error Types to General Error Type.
let mapCampResult (r: Result<'a, CampLoadError>) : Result<'a, AppError> =
    mapError fromCampLoadError r

let mapAuthResult (r: Result<'a, AuthError>) : Result<'a, AppError> =
    mapError fromAuthError r
    
let mapFeatureResult (r: Result<'a, FeatureError>) : Result<'a, AppError> =
    mapError fromFeatureError r




