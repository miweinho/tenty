// language: fsharp
module backend_tenty.Model.Conversion.DTOConversion

open System
open System.Drawing
open backend_tenty.Model.Conversion.DBConversions
open backend_tenty.Model.Types
open System.Text.Json
open backend_tenty.Model.Types.String50
open backend_tenty.Model.Validation
open backend_tenty.Model.Validation.ValidateDbData
open backend_tenty.Model.Validation.ValidateClientData
open backend_tenty.Service.GeoJsonService
open backend_tenty.Service.ResultExt

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
        ValidateDbData.validateName userDto.FirstName userDto.MiddleName userDto.LastName,
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
        Zone = featureToDto campZone.Delimitation
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
    let location = featureToDto camp.Location
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

let dbCampToDto (db: DbCamp) : Result<CampDto, string> =
    try
        if String.IsNullOrWhiteSpace(db.Location) then
            Error "DbCamp.Location is empty"
        else
            let geom = geometryFromJson db.Location
            let feature = geometryToFeature geom
            let locDto = featureToDto feature
            Ok {
                Id = db.Id
                OwnerId = db.OwnerId
                CampName = db.CampName
                Location = locDto
                IsSubCamp = db.IsSubCamp
                ParentId = db.ParentId
                CreatedAt = db.CreatedAt
                UpdatedAt = db.UpdatedAt
            }
    with ex ->
        Error ex.Message

let featureDetailToDto (detail: FeatureDetail) : FeatureDetailDto =
    {
        Id = detail.Id
        Title = String50.value detail.Title
        Content = detail.Content
        CreatedAt = detail.CreatedAt
        UpdatedAt = detail.UpdatedAt
    }

let featureDetailDtoToDomain (featureId: int) (detail: FeatureDetailDto) : Result<FeatureDetail, string> =
    result {
        do! validateFeatureDetailDto detail |> Result.map ignore

        let! title =
            match String50.create detail.Title with
            | Some t -> Ok t
            | None -> Error "Invalid title for feature detail"

        return {
            Id = detail.Id
            FeatureId = featureId
            Title = title
            Content = detail.Content
            CreatedAt = detail.CreatedAt
            UpdatedAt = detail.UpdatedAt
        }
    }

let featureToDto (feature: Feature) : FeatureDto =
    {
        Id = feature.Id
        CampId = feature.CampId
        Kind = featureKindToString feature.Kind
        Name = feature.Name |> Option.map String50.value
        Geometry = backend_tenty.Service.GeoJsonService.featureToDto feature.Geometry
        HeadingDeg = feature.HeadingDeg
        LengthM = feature.LengthM
        WidthM = feature.WidthM
        Color = feature.Color |> Option.map ColorTranslator.ToHtml
        Icon = feature.Icon |> Option.map String50.value
        Props = feature.Props
        CreatedAt = feature.CreatedAt
        UpdatedAt = feature.UpdatedAt
        Details = feature.Details |> Option.map (List.map featureDetailToDto)
    }

let featureDtoToDomain (feature: FeatureDto) : Result<Feature, string> =
    result {
        do! validateFeatureKind feature.Kind |> Result.map ignore
        do! validateOptionalString50 feature.Name "feature name" |> Result.map ignore
        do! validateGeoJsonFeatureLocation feature.Geometry |> Result.map ignore
        let! heading = validateHeading feature.HeadingDeg
        let! lengthM = validatePositiveDimension "length" feature.LengthM
        let! widthM = validatePositiveDimension "width" feature.WidthM
        do! validateFeatureColor feature.Color |> Result.map ignore
        do! validatePropsJson feature.Props |> Result.map ignore

        let! validatedDetails =
            feature.Details
            |> Option.defaultValue []
            |> List.map validateFeatureDetailDto
            |> sequenceResultsList

        let! kind = featureKindFromString feature.Kind

        let! name =
            match feature.Name with
            | None -> Ok None
            | Some n ->
                match String50.create n with
                | Some s -> Ok (Some s)
                | None -> Error "Invalid feature name: must be between 1 and 50 characters"

        let! geometry =
            try
                feature.Geometry |> featureFromDto |> Ok
            with ex -> Error ex.Message

        let! color =
            match feature.Color with
            | None -> Ok None
            | Some c ->
                try Ok (Some (ColorTranslator.FromHtml c))
                with ex -> Error $"Invalid color format: {ex.Message}"

        let! props =
            match feature.Props with
            | None -> Ok None
            | Some p when String.IsNullOrWhiteSpace p -> Ok None
            | Some p ->
                try
                    let doc = JsonDocument.Parse(p)
                    Ok (Some (doc.RootElement.Clone()))
                with ex -> Error $"Invalid props json: {ex.Message}"

        let! details =
            validatedDetails
            |> List.map (featureDetailDtoToDomain (feature.Id))
            |> sequenceResultsList

        let! icon = match feature.Icon with
                    | Some s -> Ok (String50.create s)
                    | None -> Error $"Icon missing."

        return ({
            Id = feature.Id
            CampId = feature.CampId
            Kind = kind
            Name = name
            Geometry = geometry
            HeadingDeg = heading
            LengthM = lengthM
            WidthM = widthM
            Color = color
            Icon = icon
            Props = feature.Props
            Details = Some details
            CreatedAt = feature.CreatedAt
            UpdatedAt = feature.UpdatedAt
        })
        }

let private tryCreateString50 (value: string option) (fieldName: string) : Result<String50 option, string> =
    match value with
    | Some v ->
        match String50.create v with
        | Some sv -> Ok (Some sv)
        | None -> Error $"{fieldName} is invalid"
    | None -> Ok None

let private optionToResult errorMsg opt =
    match opt with
    | Some v -> Ok v
    | None -> Error errorMsg
