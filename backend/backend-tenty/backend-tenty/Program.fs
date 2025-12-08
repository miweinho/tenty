namespace backend_tenty
#nowarn "20"

open System
open System.Text.Json.Serialization
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.IdentityModel.Tokens
open Microsoft.AspNetCore.Builder
open System.Text
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.OpenApi.Models
open backend_tenty.Model.Types

module Program =
    let exitCode = 0

    [<EntryPoint>]
    let main args =
        printfn "Starting Tenty Backend..."
        if Database.testConnection() then
            printfn "Database ready"
            let builder = WebApplication.CreateBuilder(args)
            
            //Read information about Jwt
            let cfg = builder.Configuration.GetSection("Jwt")
            let issuer     = cfg["Issuer"]
            let audience   = cfg["Audience"]
            let signingKey = cfg["SigningKey"]
            let signingKeyBytes = Encoding.UTF8.GetBytes(signingKey)
            let key = SymmetricSecurityKey(signingKeyBytes)
            let accessTtl  = TimeSpan.FromMinutes(float cfg["AccessTtlMinutes"])
            let refreshTtl = TimeSpan.FromDays(float cfg["RefreshTtlDays"])
            
            let tokenCfg : TokenConfig =
                {   Issuer = issuer
                    Audience = audience
                    SigningKey = signingKey
                    AccessTtl = accessTtl
                    RefreshTtl = refreshTtl
                    PermVersion = Some 1 }
            builder.Services.AddSingleton<TokenConfig>(tokenCfg) |> ignore
            

            builder.Services.AddControllers().AddJsonOptions(fun options ->
                options.JsonSerializerOptions.NumberHandling <-
                    JsonNumberHandling.AllowNamedFloatingPointLiterals
                options.JsonSerializerOptions.ReferenceHandler <- ReferenceHandler.Preserve
                options.JsonSerializerOptions.MaxDepth <- 128)
            builder.Services.AddSwaggerGen(fun c ->
                c.SwaggerDoc("v1", OpenApiInfo(Title = "Tenty API", Version = "v1"))
    
    // JWT Bearer Authentifizierung hinzufügen
                c.AddSecurityDefinition("Bearer", OpenApiSecurityScheme(
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT"
                ))
                
                c.AddSecurityRequirement(
                    let requirement = OpenApiSecurityRequirement()
                    requirement[OpenApiSecurityScheme(
                        Reference = OpenApiReference(
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        )
                    )] <- [||]
                    requirement
            )
    
              
            )

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(fun o ->
                    o.TokenValidationParameters <- TokenValidationParameters(
                            ValidateIssuer = true,
                            ValidIssuer = issuer,
                            ValidateAudience = true,
                            ValidAudience = audience,
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = key,
                            ValidateLifetime = true,
                            ClockSkew = TimeSpan.FromSeconds 30.0
                            )
                        )
                |> ignore
            
            builder.Services.AddCors(fun opts ->
                opts.AddPolicy("LocalDev", fun p ->
                    p.WithOrigins("http://localhost:4200")
                     .AllowAnyHeader()
                     .AllowAnyMethod()
                     .AllowCredentials()
                    |> ignore)
            )
                


            let app = builder.Build()
            
            app.UseCors("LocalDev")

            app.UseSwagger()
            app.UseSwaggerUI()
            app.UseHttpsRedirection()

            app.UseAuthentication()
            app.UseAuthorization()
            app.MapControllers()

            app.Run()
        else
            printfn "Database connection failed - exiting"

        exitCode
