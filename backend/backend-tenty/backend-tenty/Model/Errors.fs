module backend_tenty.Model.Errors

open System.Threading.Tasks

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
    | PermissionDenied s -> AppError.PermissionDenied s
    | ZonesMissing -> AppError.Unknown "ZonesMissing"
    | DetailsMissing s -> AppError.Unknown s
    | MapsMissing s -> AppError.Unknown s
    | InvalidPropertyValue s -> AppError.Unknown s

let fromAuthError (e: AuthError) : AppError =
    match e with
    | RequestEmpty -> AppError.RequestEmpty
    | RequestInvalid -> AppError.RequestInvalid
    | DbError s -> AppError.DbError s
    | UserParseError s -> AppError.UserParseError s
    | TokenGenerationError s -> AppError.TokenGenerationError s


// Map From Error Types to General Error Type.
let mapCampResult (r: Result<'a, CampLoadError>) : Result<'a, AppError> =
    mapError fromCampLoadError r

let mapAuthResult (r: Result<'a, AuthError>) : Result<'a, AppError> =
    mapError fromAuthError r




