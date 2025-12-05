module backend_tenty.Model.AuthService

open System.Security.Cryptography
open System.Text.Json
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open backend_tenty.Database
open backend_tenty.Model.Conversion.DBConversions
open backend_tenty.Model.Types
open System
open System.Text
open System.Security.Claims
open System.IdentityModel.Tokens.Jwt
open Microsoft.IdentityModel.Tokens
open FsToolkit.ErrorHandling
open backend_tenty.Model.Errors

let private generateSecureUrlSafeToken (sizeBytes: int) =
    let bytes = Array.zeroCreate<byte> sizeBytes
    use rng = RandomNumberGenerator.Create()
    rng.GetBytes(bytes)
    Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_')
    
let private buildClaims (user: User) (campRoles: CampRoles option) : Claim[] =
    let baseClaims = [
        Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString())
        Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
        Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())        
    ]
    let campClaim =
        match campRoles with
        | Some cr -> [ Claim("camp_roles", JsonSerializer.Serialize(cr)) ]
        | None -> []
    Array.ofList( baseClaims @ campClaim)
    
let generateTokenPair
    (tokenCfg: TokenConfig)
    (user: User)
    (campRoles: CampRoles option) :TokenPair =
    
    let now = DateTime.UtcNow
    let claims = buildClaims user campRoles
    
    let keyBytes = Encoding.UTF8.GetBytes(tokenCfg.SigningKey)
    let securityKey = SymmetricSecurityKey(keyBytes)
    let signingCredentials = SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256)
    
    let accessExpires = now.Add(tokenCfg.AccessTtl)
    let jwt =
        JwtSecurityToken(
            issuer = tokenCfg.Issuer,
            audience = tokenCfg.Audience,
            claims = claims,
            notBefore = Nullable(now),
            expires = Nullable(accessExpires),
            signingCredentials = signingCredentials
            )
    let accessToken = JwtSecurityTokenHandler().WriteToken(jwt)
    let refreshToken = generateSecureUrlSafeToken 64
    let refreshExpires = now.Add(tokenCfg.RefreshTtl)
    
    {
        AccessToken = accessToken
        AccessExpiresAt = accessExpires
        RefreshToken = refreshToken
        RefreshExpiresAt = refreshExpires
    }
    
    
    
let generateClaims (permissions: CampPermissionSummary list) : CampRoles =
    let empty = { Admin = []; Write = []; Read = [] }
    permissions
    |> List.fold (fun acc p ->
        let campIdInt = userPermissionToInt p.Permission
        match p.Permission with
        | Admin -> { acc with Admin = p.Id :: acc.Admin }
        | Write -> { acc with Write = p.Id :: acc.Write }
        | Read  -> { acc with Read  = p.Id :: acc.Read }
    ) empty
    
let generateTokenForUser (tokenCfg: TokenConfig) (user: User) : Task<Result<TokenPair, string>> =
    taskResult {
        let! permissions = getAllCampsWithPermissionForUser user.Id
        let claims = generateClaims permissions
        return generateTokenPair tokenCfg user (Some claims)
        
    }
     
        
let private hashToken (token: string) : string =
    use sha = SHA256.Create()
    token
    |> Encoding.UTF8.GetBytes
    |> sha.ComputeHash
    |> Convert.ToBase64String
    
let generateDBRefreshToken (user: User) (token: TokenPair) : DbRefreshToken =
    {
        Token = hashToken token.RefreshToken
        UserId = user.Id
        ExpiresAt = token.RefreshExpiresAt
    }
    
let generateHashedRefreshTokenDTO (token: RefreshTokenDTO) : RefreshTokenDTO =
    {
     Token = hashToken token.Token
    }
    
    //Function to parse Claims into CampRoles
let private parseClaim (claim: Claim) : Result<CampRoles, string> =
    try
        let cr = JsonSerializer.Deserialize<CampRoles>(claim.Value)
        if isNull (box cr) then
            Error "No Camp Roles provided."
        else
            Ok cr
     
        
    with
    | :? JsonException as ex -> Error $"invalid camp_roles json: {ex.Message}"
    | ex -> Error $"error parsing camp_roles {ex.Message}"
    
let checkClaimForPermission (claim: Claim) (id: int) (permission: UserPermission): Result<Boolean, string> =
    try
        match parseClaim claim with
        | Error e -> Error $"Error while checking claim for permission: {e}"
        | Ok campRoles ->
            let result =
                match permission with
                | Admin -> campRoles.Admin |> List.exists (fun elem -> elem = id)
                | Write -> campRoles.Write |> List.exists (fun elem -> elem = id) || campRoles.Admin |> List.exists (fun elem -> elem = id)
                | Read -> campRoles.Write |> List.exists (fun elem -> elem = id) || campRoles.Read |> List.exists( fun elem -> elem = id) || campRoles.Admin |> List.exists (fun elem -> elem = id)
            match result with
            | true -> Ok result
            | false -> Error "Unauthorized"
                
    with
    | ex -> Error $"Error while checking for valid permission in claim: {ex.Message}"
    
let parseUserIdFromToken (httpContext: HttpContext) : Result<int, string> =
    let userIdClaim =
        match httpContext.User.FindFirst("sub") with
        | null -> httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
        | c -> c
        
    match userIdClaim with
    | null -> Error "Cant find claim in token."
    | userIdClaim ->
        match System.Int32.TryParse(userIdClaim.Value) with
        | (true, userId) ->
            Ok userId
        | (false, _) ->
            Error "No valid id in token."
        
    
let processLogin (tokenCfg: TokenConfig) (dto: LoginDTO): Task<Result<TokenPair, AuthError>> =
        taskResult {
            let! dbUser = loadUserByMail dto.MailAddress |> mapTaskError AuthError.DbError
            let! user = dbUserToDomain dbUser |> Result.mapError AuthError.UserParseError
            let! tokens = generateTokenForUser tokenCfg user |> mapTaskError AuthError.TokenGenerationError
            do! deleteRefreshTokenByUserId user |> mapTaskError AuthError.DbError |> TaskResult.ignore
            let dbRefresh = generateDBRefreshToken user tokens
            let! _ = addRefreshToken dbRefresh |> mapTaskError AuthError.DbError
            return tokens
        }
        
    
    