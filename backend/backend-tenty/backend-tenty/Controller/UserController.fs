namespace backend_tenty.Controllers

open System
open System.Threading.Tasks
open backend_tenty.Database
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open backend_tenty.Model.AuthService
open backend_tenty.Model.Conversion.DBConversions
open backend_tenty.Model.Types

[<ApiController>]
[<Route("api/[controller]")>]
type public UserController(logger: ILogger<UserController>, tokenCfg: TokenConfig) =
    inherit ControllerBase()
        
    [<HttpPost("login")>]
    member this.NewLogin([<FromBody>] dto: LoginDTO) : Task<ActionResult> =
        task{
            //Todo: Add real authentication
            //Todo: Add better logger logic.
            let! result = processLogin tokenCfg dto
            match result with
            | Ok value ->
                return this.Ok(value) :> ActionResult
            |Error e ->
                logger.LogError("Error while logging in: {Error}", e)
                return (this.BadRequest($"Error while logging in. Provide valid credentials.")) :> ActionResult
        }
                    
    [<HttpPost("refresh")>]
    member this.Refresh([<FromBody>] dto: RefreshTokenDTO) : Task<ActionResult> =
        task{
        if String.IsNullOrWhiteSpace(dto.Token) then
            return this.BadRequest("No refresh Token provided.") :> ActionResult
        else
            let! refreshToken = generateHashedRefreshTokenDTO dto |> getRefreshToken
            match refreshToken with
            | Error e ->
                logger.LogError("Error while checking for Refresh Token in DB: {e}", e)
                return this.BadRequest("Refresh Token not valid. Try to log in again.") :> ActionResult
            | Ok t ->
                let! user = loadUserById t.UserId
                match user with
                | Error e ->
                    logger.LogError("Error while loading user from refresh token: {e}", e)
                    return this.BadRequest("Refresh Token not valid. Try to log in again.") :> ActionResult
                | Ok dbUser ->
                     match dbUserToDomain dbUser with
                     | Error e ->
                         logger.LogError("Error while parsing Domain User {e}", e)
                         return this.BadRequest("Error while logging in.") :> ActionResult
                     | Ok user ->
                        let! newToken = generateTokenForUser tokenCfg user 
                        match newToken with
                        | Error e ->
                            logger.LogError("Error while generating Tokens: {e}", e)
                            return this.BadRequest("Error while logging in.") :>ActionResult
                        | Ok tokens ->
                            // TODO Adjust refresh token per device so user dont get logged out everytime. Rethink deleteRefreshTokenByUserId.
                        let! deleteResult = deleteRefreshTokenByUserId user 
                        match deleteResult with
                        | Error e -> logger.LogError("Error while deleting RefreshToken: {e}", e)
                        | Ok _ -> ()
                        let! newRefreshToken = generateDBRefreshToken user tokens |> addRefreshToken 
                        match newRefreshToken with
                        | Error e ->
                            logger.LogError("Error while saving Refresh token: {e}", e)
                            return this.BadRequest("Error while logging in.") :> ActionResult            
                        | Ok t -> return this.Ok(tokens) :> ActionResult
        }