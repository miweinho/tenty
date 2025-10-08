namespace backend_tenty.Controllers

open Microsoft.AspNetCore.Mvc
open backend_tenty
open backend_tenty.Model
open backend_tenty.Database

[<ApiController>]
[<Route("api/[controller]")>]
type CampController() =
    inherit ControllerBase()
    
    [<HttpPost>]
    member this.CreateCamp([<FromBody>] campDto: CampDto) =
        try
            printf "%A" campDto
            match SwissCoordinate.create campDto.Location.x campDto.Location.y with
            | Some location ->
                let x = SwissCoordinate.x location
                let y = SwissCoordinate.y location
                let result = Database.addCamp(campDto.Name, x, y)
                this.Ok(result) :> IActionResult
            | None ->
                this.BadRequest("Ungültige Koordinaten") :> IActionResult
        with
        | ex ->
            this.BadRequest(ex.Message) :> IActionResult
            
            
    [<HttpGet>]
    member this.GetAllCamps() =
        try
            let camps = Database.getAllCamps()
            this.Ok(camps) :> IActionResult
        with
        | ex ->
            this.BadRequest(ex.Message) :> IActionResult
