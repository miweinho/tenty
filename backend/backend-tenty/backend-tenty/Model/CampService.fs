module backend_tenty.Model.CampService

open System
open System.Drawing
open System.Threading.Tasks
open backend_tenty.Model.Conversion.DBConversions
open backend_tenty.Model.Types
open backend_tenty.Database
open backend_tenty.Service.ResultExt
open backend_tenty.Model.Errors
open FsToolkit.ErrorHandling
open backend_tenty.Service.GeoJsonService
open backend_tenty.Model.Validation.ValidateClientData

// Build a Camp from Several DBCacmp Compoennts.



let loadDbUsersByCampUsers (dbCampUsers: DbCampUser list) : Task<Result<(DbCampUser * DbUser) list, string>> =
    taskResult {
        let mutable acc: (DbCampUser * DbUser) list = []
        for cu in dbCampUsers do
            let! user = loadUserById cu.UserId
            acc <- (cu, user) :: acc
        return List.rev acc    
}


//let buildCamp (dbCamp: DbCamp, dbCampUser: DbCampUser list option, dbCampZone: DbCampZone list) : Camp =
//camp: DbCamp (id, ownerid, campname, location, issubcamp, parentid) userlist -> CampUser Table (User entsprechend laden) / delimitation -> / campdetail -> campDetail table + detaillist entries / shownMaps -> db shown map table / CampZones -> camp
//TODO: Refactor to reduce complexity.
let loadAndBuildCamp(campId: int) : Task<Result<Camp, CampLoadError>> =
    taskResult {
    let! dbCamp = getCampById campId
                  |> mapTaskError CampLoadError.DbError
    let! dbOwner = loadUserById dbCamp.OwnerId
                   |> mapTaskError CampLoadError.DbError
    let! owner = dbUserToDomain dbOwner
                 |> Result.mapError CampLoadError.InvalidPropertyValue
    
    let! dbCampUser = getCampUserForCamp campId
                      |> mapTaskError CampLoadError.DbError
    let! campName =
        match String50.create dbCamp.CampName with
        | Some name -> Ok name
        | None -> Error (CampLoadError.InvalidPropertyValue dbCamp.CampName)
    let! dbCampDelimitationList = getCampZonesForCamp (campId, true) |> mapTaskError CampLoadError.DbError
    let! campDelimitation = dbCampDelimitationList[0] |> dbCampZoneToDomain |> mapError CampLoadError.InvalidPropertyValue
    let! dbCampZonesList =
        getCampZonesForCamp(campId, false)
        |> mapTaskError CampLoadError.DbError
    let! campZones =
        dbCampZonesList
        |> List.map (dbCampZoneToDomain >> Result.mapError CampLoadError.DbError)
        |> sequenceResultsList
        |> Task.FromResult
    
    let! dbCampDetails = getCampDetailForCamp campId |> mapTaskError CampLoadError.DbError
    let! dbCampDetailListEntries = getDetailListEntriesForCamp campId |> mapTaskError CampLoadError.DbError
    let! campDetails = dbCampDetailsToDomain dbCampDetails dbCampDetailListEntries |> Result.mapError CampLoadError.DbError |> Task.FromResult
    let! dbShownMaps = getShownMaps campId |> mapTaskError CampLoadError.DbError
    let! shownMaps = dbShownMapsToDomain dbShownMaps |> Result.mapError CampLoadError.DbError |> Task.FromResult
    let! dbCampUsers = loadDbUsersByCampUsers dbCampUser |> mapTaskError CampLoadError.DbError
    let! campUsers = dbCampUsers
                     |> List.map (dbCampUserToDomain >> Result.mapError CampLoadError.DbError)
                     |> sequenceResultsList
                     |> Task.FromResult
    let location = dbCamp.Location
                   |> geometryFromJson
                   |> geometryToFeature      
    
    return {
        Id = dbCamp.Id
        Camp.Owner = owner
        Name =  campName
        Location = location
        UserList = campUsers
        Delimitation = campDelimitation
        CampZones = campZones
        IsSubCamp = dbCamp.IsSubCamp
        SubCamp = Some [ ]
        CampDetail = campDetails
        ShownMaps = shownMaps        
    }
    }
    
let private createNewDbCampDto (campDto: CreateCampDto) (ownerId: int) : Task<Result<NewDbCampDto, string>> =
    taskResult {
        let! campName = validateName campDto.Name
        let! location = validateGeoJsonFeatureLocation campDto.Location
        let stringLocation = featureDtoToDbWkt location
        
        let! parentId =
            if campDto.IsSubCamp then
                taskResult {
                    let! possibleCamps = getAllCampsForUser ownerId
                    let! parentCamp = validateParentCamp possibleCamps campDto.ParentId
                    return parentCamp.ParentId
                }
            else
                Task.FromResult(Ok None)
    return {
        OwnerId = ownerId
        CampName = campName
        Location = stringLocation
        IsSubCamp = campDto.IsSubCamp
        //TODO: fix parent id when subcamps are enabled.
        ParentId = None
    }
    }

let private createNewDbCampZoneDto (campZone: CampZoneDto) : Result<NewDbCampZoneDto, string> =
    result {
        let! zoneName = validateName campZone.Name
        let! zone = validateGeoJsonFeatureLocation campZone.Zone
        let color = ColorTranslator.FromHtml campZone.Color
        let dbColor = ColorTranslator.ToHtml color
        let dbZone = featureDtoToDbWkt zone

        return {
            CampId = 0 //Set to zerot to show invalid campId will be set in db transaction.
            Name = zoneName
            IsCampDelimitation = campZone.IsCampDelimitation
            Color = Some dbColor
            Zone = dbZone
        }
    }
    
let private createNewDbCampDetailDto (campDetail: CampDetailDto) : Result<NewDbCampDetailDto, string> =
    result {
        let! startDate = validateDateString campDetail.StartDate
        let! endDate = validateDateString campDetail.EndDate
        let! city = validateName campDetail.City
        
        return {
            CampId = 0 //Set to zerot to show invalid campId will be set in db transaction.
            StartDate = startDate
            EndDate = endDate
            Description = campDetail.Description
            City = Some city 
        }
    }
    
let private createNewDbCampDetailListEntryDto (detailListEntry: DetailListEntryDto) : Result<NewDbDetailListEntryDto, string> =
    result {
        let! title = validateName detailListEntry.Title
        
        return {
            CampDetailsId = 0
            Title = title
            Content = detailListEntry.Content
        }
    }
let private createNewDbShownMapsDto (mapName: string) : Result<NewDbShownMapsDto, string> =
    result {
        let! mapName = validateName mapName
        
        return {
            CampId = 0 //Set to zerot to show invalid campId will be set in db transaction.
            MapName = mapName
        }
    }
    
let private validateNewCamp (campDto: CreateCampDto) (ownerId: int) : Task<Result<NewDbFullCampDto, CampLoadError>> =
    taskResult {
        let! dbCampDto =
            createNewDbCampDto  campDto ownerId
            |> mapTaskError CampLoadError.InvalidPropertyValue
        let! dbDelimitationDto =
            createNewDbCampZoneDto campDto.Delimitation
            |> mapError CampLoadError.InvalidPropertyValue
            |> Task.FromResult
        let! campZones =
            campDto.CampZones
            |> Option.defaultValue []
            |> List.map createNewDbCampZoneDto
            |> sequenceResultsList
            |> mapError CampLoadError.InvalidPropertyValue
            |> Task.FromResult
        let zonesDtoList =
            dbDelimitationDto :: campZones
        let! dbCampDetailDto =
            createNewDbCampDetailDto campDto.CampDetail
            |> mapError CampLoadError.DetailsMissing
            |> Task.FromResult
        let! detailListEntriesDtoList =
            campDto.CampDetail.DetailList
            |> Option.defaultValue []
            |> List.map createNewDbCampDetailListEntryDto
            |> sequenceResultsList
            |> mapError CampLoadError.DetailsMissing
            |> Task.FromResult
        return {
            CampDto = dbCampDto
            CampZones = zonesDtoList
            CampDetails = dbCampDetailDto
            CampDetailListEntries = Some detailListEntriesDtoList
        }
    }
    
    
    
let createCamp (campDto: CreateCampDto) (ownerId: int) : Task<Result<Camp, CampLoadError>> =
    taskResult {
         let! newDbCampDto = validateNewCamp campDto ownerId
         let! newCampId = addNewCamp newDbCampDto |> mapTaskError CampLoadError.DbError
         let! savedCamp = loadAndBuildCamp newCampId
        return savedCamp
    }
//problem im moment:
// uq_camp

