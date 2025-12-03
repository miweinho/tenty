module backend_tenty.Model.Conversion.DTOConversion

open System.Drawing
open backend_tenty.Model.Conversion.DBConversions
open backend_tenty.Model.Types
open backend_tenty.Model.Types.String50
open backend_tenty.Model.Validation.ValidateDbData
open backend_tenty.Service.GeoJsonService

let domainUserToDTO (user: User) : UserDto =
    {
        Id = user.Id
        FirstName = string50Value user.NameInformation.FirstName
        MiddleName = string50ValueOpt user.NameInformation.MiddleName
        LastName = string50Value user.NameInformation.LastName
        Email = mailAddressToString user.MailInformation.MailAddress
    }
    
let dtoUserToDomain (userDto: UserDto) : Result<User, string> =
    match
        validateName userDto.FirstName userDto.MiddleName userDto.LastName,
        validateMail userDto.Email
    with
    | Ok nameInfo, Ok mailInfo ->
        Ok {
            Id = userDto.Id
            NameInformation = nameInfo
            MailInformation = mailInfo
        }
    | Error e, _ -> Error ("Name ungültig: " + e)
    | _, Error e -> Error ("E-Mail ungültig: " + e)
    
let domainUserToDto (user: User) : UserDto =
    {
        Id = user.Id
        FirstName = String50.value user.NameInformation.FirstName
        MiddleName = String50.toStringOption user.NameInformation.MiddleName
        LastName = String50.value user.NameInformation.LastName
        Email = (user.MailInformation.MailAddress.ToString())
    }

let campUserToDto (campUser: CampUser) : CampUserDto =
    {
        User = domainUserToDTO campUser.User
        Permission = userPermissionToInt campUser.Permission
    }
    
let campZoneToDto (campZone: CampZone) : CampZoneDto =
    {
        Name = String50.value campZone.Name
        IsCampDelimitation = campZone.IsDelimitation
        Color = ColorTranslator.ToHtml(campZone.Color)
        Zone = serializeFeature campZone.Delimitation
    }
    
let detailListEntryToDto (detailEntry: DetailListEntry) : DetailListEntryDto =
    {
        Title = String50.value detailEntry.Title
        Content = detailEntry.Content
    }

let campDetailToDto (campDetail: CampDetail) : CampDetailDto =
    let detailListDto =
        match campDetail.DetailList with
        | Some list -> List.map (fun el -> detailListEntryToDto el ) list
        | None -> []
    {
        StartDate = campDetail.StartDate.ToString()
        EndDate = campDetail.EndDate.ToString()
        Description = campDetail.Description
        City = campDetail.City
        DetailList = Some detailListDto
    }
    
    
    
let domainCampToDto (camp: Camp) : FullCampDto =
    let ownerDto = domainUserToDTO camp.Owner
    let location = serializeFeature camp.Location
    let campUserDto = List.map (fun el -> campUserToDto el ) camp.UserList
    let campDetailDto = campDetailToDto camp.CampDetail
    let shownMapsDto = List.map (fun el -> String50.value el) camp.ShownMaps
    let campZonesDto = List.map (fun el -> campZoneToDto el) camp.CampZones
    
    {
        Id = camp.Id
        Owner = ownerDto
        Name = String50.value camp.Name
        Location = location
        UserList = campUserDto
        Delimitation = campZoneToDto camp.Delimitation
        CampDetail = campDetailDto
        IsSubCamp = camp.IsSubCamp
        SubCamp = []
        ShownMaps = shownMapsDto
        CampZones = campZonesDto
        
    }
    
    
    

    