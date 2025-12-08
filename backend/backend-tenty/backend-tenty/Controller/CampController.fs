namespace backend_tenty.Controllers


open Giraffe.ModelParser
open Microsoft.AspNetCore.Authorization
open backend_tenty.Database
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open backend_tenty.Model.AuthService
open backend_tenty.Model.CampService
open backend_tenty.Model.Conversion.DTOConversion
open backend_tenty.Model.Types
open System.Threading.Tasks
open backend_tenty.Service.ResultExt

[<ApiController>]
[<Route("api/[controller]")>]
type public CampController(logger: ILogger<CampController>) =
    inherit ControllerBase()

    //Returns all camps linked to a user id.
    //TODO: Add viewport on map so only maps on view are showed.
    [<HttpGet("list")>]
    [<Authorize>]
    member this.Get() : Task<ActionResult<CampDto list>> =
        task {
            let userId = parseUserIdFromToken this.HttpContext
            match userId with
            | Error err ->
                logger.LogError("Error while checking for valid userId in claim: {err}", err)
                return ActionResult<CampDto list>(this.Problem(detail = "Internal error while loading camp."))
            | Ok userId ->
                    let! dbCamps = getAllCampsForUser userId
                    match dbCamps with
                    | Error err ->
                        logger.LogError("Error while loading camps for user {UserId}: {Error}", userId, err)
                        return ActionResult<CampDto list>(this.Problem(detail = "Internal error while loading camp."))
                    | Ok dbCamps ->
                        let converted = dbCamps |> List.map dbCampToDto |> sequenceResultsList
                        match converted with
                        | Ok camps ->
                            return ActionResult<CampDto list>(camps)
                        | Error e ->
                            logger.LogError("Error while converting DbCamp -> CampDto: {Error}", e)
                            return ActionResult<CampDto list>(this.Problem(detail = "Internal error while converting camp data"))
                                   
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
            let userId = parseUserIdFromToken this.HttpContext
            match userId with
            | Error err ->
                logger.LogError("Error while checking for valid userId in claim: {err}", err)
                return ActionResult<FullCampDto>(this.Problem(detail = "Internal error while loading camp."))
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