namespace backend_tenty
#nowarn "20"

open System
open System.Collections.Generic
open System.IO
open System.Linq
open System.Threading.Tasks
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.HttpsPolicy
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open backend_tenty.Database

module Program =
    let exitCode = 0

    [<EntryPoint>]
    let main args =
        printfn "Starting Tenty Backend..."
        if Database.testConnection() then
            printfn "Database ready"
            let builder = WebApplication.CreateBuilder(args)

            builder.Services.AddControllers()
            builder.Services.AddSwaggerGen()

            let app = builder.Build()

            app.UseSwagger()
            app.UseSwaggerUI()
            app.UseHttpsRedirection()

            app.UseAuthorization()
            app.MapControllers()

            app.Run()
        else
            printfn "Database connection failed - exiting"

        exitCode
