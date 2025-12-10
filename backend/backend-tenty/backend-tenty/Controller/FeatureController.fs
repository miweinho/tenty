/// API endpoints for managing camp features.
namespace backend_tenty.Controller.FeatureController

open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open System.Threading.Tasks
open backend_tenty.Model.AuthService
open backend_tenty.Model.Errors
open backend_tenty.Model.FeatureService
open backend_tenty.Model.Conversion.DTOConversion
open backend_tenty.Model.Types

[<ApiController>]
[<Route("api/[controller]")>]
/// Exposes feature CRUD endpoints secured by camp permissions.
type public FeatureController(logger: ILogger<FeatureController>) =
    inherit ControllerBase()

    /// Converts domain-level feature errors into HTTP responses.
    member private this.handleFeatureError (err: FeatureError) (defaultMessage: string) =
        match fromFeatureError err with
        | AppError.NotFound msg -> this.NotFound(msg) :> IActionResult
        | AppError.PermissionDenied _ -> this.Forbid()
        | AppError.ValidationError msg -> this.BadRequest(msg)
        | AppError.DbError msg -> this.Problem(detail = msg)
        | _ -> this.Problem(detail = defaultMessage)

    [<HttpGet("camp/{campId}")>]
    [<Authorize>]
    /// Returns all features for the specified camp when the caller has access.
    member this.GetFeaturesForCamp(campId: int) : Task<ActionResult<FeatureDto list>> =
        task {
            match checkClaimForPermission (this.HttpContext.User.FindFirst("camp_roles")) campId Read with
            | Error e ->
                logger.LogError("Error while parsing permission claim: {Error}", e)
                return ActionResult<FeatureDto list>(this.Unauthorized("Unauthorized"))
            | Ok _ ->
                let! featureResult = listFeaturesForCamp campId
                match featureResult |> mapFeatureResult with
                | Ok features ->
                    return ActionResult<FeatureDto list>(features |> List.map featureToDto)
                | Error appErr ->
                    logger.LogError("Error while loading features for camp {CampId}: {Error}", campId, appErr)
                    return ActionResult<FeatureDto list>(this.BadRequest "Error while loading features.")
        }

    [<HttpGet("{id}")>]
    [<Authorize>]
    /// Retrieves a single feature by id, enforcing camp-level permissions.
    member this.GetFeature(id: int) : Task<ActionResult<FeatureDto>> =
        task {
            let! featureResult = getFeatureByIdWithDetails id
            match featureResult |> mapFeatureResult with
            | Ok feature ->
                match checkClaimForPermission (this.HttpContext.User.FindFirst("camp_roles")) feature.CampId Read with
                | Error e ->
                    logger.LogError("Error while parsing permission claim: {Error}", e)
                    return ActionResult<FeatureDto>(this.BadRequest("Unauthorized"))
                | Ok _ ->
                    return ActionResult<FeatureDto>(featureToDto feature)
            | Error appErr ->
                logger.LogError("Error while loading feature {FeatureId}: {Error}", id, appErr)
                return ActionResult<FeatureDto>(this.BadRequest "Error while loading feature.")
        }

    [<HttpPost>]
    [<Authorize>]
    /// Creates a new feature for a camp the caller can modify.
    member this.CreateFeature(featureDto: CreateFeatureDto) : Task<ActionResult<FeatureDto>> =
        task {
            match checkClaimForPermission (this.HttpContext.User.FindFirst("camp_roles")) featureDto.CampId Write with
            | Error e ->
                logger.LogError("Error while parsing permission claim: {Error}", e)
                return ActionResult<FeatureDto>(this.BadRequest("Unauthorized"))
            | Ok _ ->
                let! createResult = createFeature featureDto
                match createResult |> mapFeatureResult with
                | Ok feature ->
                    return ActionResult<FeatureDto>(featureToDto feature)
                | Error appErr ->
                    logger.LogError("Error while creating feature for camp {CampId}: {Error}", featureDto.CampId, appErr)
                    return ActionResult<FeatureDto>(this.BadRequest "Error while creating feature.")
        }