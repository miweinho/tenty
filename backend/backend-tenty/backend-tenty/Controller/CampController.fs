namespace backend_tenty.Controllers


open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open backend_tenty.Model.AuthService
open backend_tenty.Model.CampService
open backend_tenty.Model.Conversion.DTOConversion
open backend_tenty.Model.Types
open backend_tenty.Model.Errors
open System.Threading.Tasks

[<ApiController>]
[<Route("api/[controller]")>]
type public CampController(logger: ILogger<CampController>) =
    inherit ControllerBase()

     member private this.handleCampError (err: CampLoadError) (defaultMessage: string) =
        match fromCampLoadError err with
        | AppError.NotFound msg -> this.NotFound(msg) :> IActionResult
        | AppError.PermissionDenied _ -> this.Forbid()
        | AppError.ValidationError msg -> this.BadRequest(msg)
        | AppError.DbError msg -> this.Problem(detail = msg)
        | _ -> this.Problem(detail = defaultMessage)
        
    //Returns all camps linked to a user id.
    //TODO: Add viewport on map so only maps on view are showed.
    [<HttpGet("list")>]
    [<Authorize>]
    member this.GetAll() : Task<ActionResult<CampDto list>> =
        task {
            
            match parseUserIdFromToken this.HttpContext with
            | Error err ->
                logger.LogError("Error while checking for valid userId in claim: {err}", err)
                return ActionResult<CampDto list>(this.Problem(detail = "Internal error while loading camps."))
            | Ok userId ->
                    let! campResult = getCampsForUser userId
                    match campResult |> mapCampResult with
                    | Ok camps ->
                        return ActionResult<CampDto list>(camps)
                    | Error appErr ->
                        logger.LogError("Error while loading camps for user {UserId}: {Error}", userId, appErr)
                        return ActionResult<CampDto list>(this.Problem(detail = "Internal error while loading camps."))
                                   
        }
    
    [<HttpGet("{id}")>]
    [<Authorize>]
    member this.GetCamp(id: int) : Task<ActionResult<FullCampDto>> =
        task {
            match checkClaimForPermission (this.HttpContext.User.FindFirst("camp_roles")) id Read with
            | Error e ->
                logger.LogError("Error while parsing permission claim: {Error}", e)
                return ActionResult<FullCampDto>(this.BadRequest("Unauthorized"))
            | Ok r ->
                let! campResult = loadAndBuildCamp id
                match campResult with
                | Error e ->
                    logger.LogError("Error while loading camp: {Error}", e)
                    return ActionResult<FullCampDto>(this.BadRequest($"Error while loading camp with id {id}."))
                | Ok c ->
                    let campDto = domainCampToDto c 
                    return ActionResult<FullCampDto>(campDto)
        }
        
    [<HttpPost>]
    [<Authorize>]
    member this.PostCamp(newCampDto: CreateCampDto) : Task<ActionResult<FullCampDto>> =
        task {
            match parseUserIdFromToken this.HttpContext with
            | Error err ->
                logger.LogError("Error while checking for valid userId in claim: {err}", err)
                return ActionResult<FullCampDto>(this.Problem(detail = "Internal error while creating camp."))
            | Ok userId ->
                let! newCamp = createCamp newCampDto userId
                match newCamp with
                | Error e ->
                    logger.LogError("Error while creating camp: {Error}", e)
                    return ActionResult<FullCampDto>(this.BadRequest($"Error while creating camp."))
                | Ok c ->
                    let campDto = domainCampToDto c
                    return ActionResult<FullCampDto>(campDto)
            
        }
        
    [<HttpPut("{id}")>]
    [<Authorize>]
    member this.PutCamp(id: int, campDto: UpdateCampDto) : Task<ActionResult<FullCampDto>> =
        task {
            match checkClaimForPermission (this.HttpContext.User.FindFirst("camp_roles")) id Write with
            | Error e ->
                logger.LogError("Error while parsing permission claim: {Error}", e)
                return ActionResult<FullCampDto>(this.BadRequest("Unauthorized"))
            | Ok claim ->
                match parseUserIdFromToken this.HttpContext with
                | Error err ->
                    logger.LogError("Error while checking for valid userId in claim: {Error}", err)
                    return ActionResult<FullCampDto>(this.Problem(detail = "Internal error while updating camp."))
                | Ok userId ->
                    let! updateResult = updateCamp id userId campDto
                    match updateResult |> mapCampResult with
                    | Ok camp -> return ActionResult<FullCampDto>(domainCampToDto camp)
                    | Error e ->
                        logger.LogError("Error while updating camp {CampId}: {Error}", id, e)
                        return ActionResult<FullCampDto>(this.BadRequest $"Error while updating camp")
                        }