/// Domain logic for creating, reading, updating, and deleting features.
module backend_tenty.Model.FeatureService

open System.Drawing
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open backend_tenty.Database
open backend_tenty.Model.Conversion.DBConversions
open backend_tenty.Model.Conversion.DTOConversion
open backend_tenty.Model.Errors
open backend_tenty.Model.Types
open backend_tenty.Model.Validation.ValidateClientData
open backend_tenty.Service.ResultExt
open backend_tenty.Service.GeoJsonService

let private mapDbResultToFeatureError featureId (result: Result<'a, string>) : Result<'a, FeatureError> =
    match result with
    | Ok value -> Ok value
    | Error msg when msg.Contains("not found") -> Error (FeatureError.FeatureNotFound featureId)
    | Error msg -> Error (FeatureError.DbError msg)
let private validateAndBuildFeature (dto: CreateFeatureDto) : Result<Feature, FeatureError> =
    result {
        let! validated = validateFeatureDto dto |> Result.mapError FeatureError.InvalidPropertyValue


        let! kind = featureKindFromString dto.Kind |> Result.mapError FeatureError.InvalidPropertyValue

        let! name =
            match validated.Name with
            | Some n ->
                match String50.create n with
                | Some s -> Ok (Some s)
                | None -> Error (FeatureError.InvalidPropertyValue "Invalid feature name: must be between 1 and 50 characters")
            | None -> Ok None

        let! geometry =
            try
                validated.Geometry |> featureFromDto |> Ok
            with ex -> Error (FeatureError.GeometryInvalid ex.Message)

        let! heading = validateHeading validated.HeadingDeg |> Result.mapError FeatureError.InvalidPropertyValue
        let! lengthM = validatePositiveDimension "length" validated.LengthM |> Result.mapError FeatureError.InvalidPropertyValue
        let! widthM = validatePositiveDimension "width" validated.WidthM |> Result.mapError FeatureError.InvalidPropertyValue

        let! color =
            match dto.Color with
            | None -> Ok None
            | Some c ->
                try
                    Ok (Some (ColorTranslator.FromHtml c))
                with ex -> Error (FeatureError.InvalidPropertyValue $"Invalid color format: {ex.Message}")

        let! icon =
            match validated.Icon with
            | Some s ->
                match String50.create s with
                | Some i -> Ok (Some i)
                | None -> Error (FeatureError.InvalidPropertyValue "Invalid icon value")
            | None -> Ok None

        let! details =
            validated.Details
            |> Option.defaultValue []
            |> List.map (featureDetailDtoToDomain 0 >> Result.mapError FeatureError.InvalidPropertyValue)
            |> sequenceResultsList

        let normalizedDetails = if validated.Details.IsSome then Some details else None

        return
            {
                Id = 0
                CampId = validated.CampId
                Kind = kind
                Name = name
                Geometry = geometry
                HeadingDeg = heading
                LengthM = lengthM
                WidthM = widthM
                Color = color
                Icon = icon
                Props = dto.Props
                Details = normalizedDetails
                CreatedAt = None
                UpdatedAt = None
            }
    }

let private validateAndBuildUpdatedFeature (featureId: int) (dto: FeatureDto) : Result<Feature, FeatureError> =
    result {
        // Kind
        let! kind =
            featureKindFromString dto.Kind
            |> Result.mapError FeatureError.InvalidPropertyValue

        // Name
        let! name =
            match dto.Name with
            | Some n ->
                match String50.create n with
                | Some s -> Ok (Some s)
                | None ->
                    Error (FeatureError.InvalidPropertyValue "Invalid feature name: must be between 1 and 50 characters")
            | None -> Ok None

        // Geometrie (GeoJSON -> Domain GeoJsonFeature) über GeoJsonService
        let! geometry =
            try
                dto.Geometry
                |> featureFromDto
                |> Ok
            with ex ->
                Error (FeatureError.GeometryInvalid ex.Message)

        // Heading, Länge, Breite wie beim Create
        let! heading =
            validateHeading dto.HeadingDeg
            |> Result.mapError FeatureError.InvalidPropertyValue

        let! lengthM =
            validatePositiveDimension "length" dto.LengthM
            |> Result.mapError FeatureError.InvalidPropertyValue

        let! widthM =
            validatePositiveDimension "width" dto.WidthM
            |> Result.mapError FeatureError.InvalidPropertyValue

        // Farbe aus HTML-String
        let! color =
            match dto.Color with
            | None -> Ok None
            | Some c ->
                try
                    Ok (Some (ColorTranslator.FromHtml c))
                with ex ->
                    Error (FeatureError.InvalidPropertyValue $"Invalid color format: {ex.Message}")

        // Icon
        let! icon =
            match dto.Icon with
            | Some s ->
                match String50.create s with
                | Some i -> Ok (Some i)
                | None -> Error (FeatureError.InvalidPropertyValue "Invalid icon value")
            | None -> Ok None

        // Details validieren & in Domain-Objekte überführen
        let! details =
            dto.Details
            |> Option.defaultValue []
            |> List.map (featureDetailDtoToDomain featureId >> Result.mapError FeatureError.InvalidPropertyValue)
            |> sequenceResultsList

        let normalizedDetails =
            if dto.Details.IsSome then Some details else None

        // Domain-Feature für das Update bauen
        return
            {
                Id = featureId
                CampId = dto.CampId
                Kind = kind
                Name = name
                Geometry = geometry
                HeadingDeg = heading
                LengthM = lengthM
                WidthM = widthM
                Color = color
                Icon = icon
                Props = dto.Props
                Details = normalizedDetails
                // CreatedAt/UpdatedAt kommen aus DTO – DB kann UpdatedAt selbst setzen,
                // beim Rückladen bekommst du den definitiven Wert.
                CreatedAt = dto.CreatedAt
                UpdatedAt = dto.UpdatedAt
            }
    }


let private loadFeatureWithDetails (featureId: int) : Task<Result<Feature, FeatureError>> =
    taskResult {
        let! dbFeature = getFeatureById featureId |> mapTaskError FeatureError.DbError
        let! feature = dbFeatureToDomain dbFeature |> Result.mapError FeatureError.InvalidPropertyValue |> Task.FromResult

        let! dbDetails = getFeatureDetails featureId |> mapTaskError FeatureError.DbError
        let! details =
            dbDetails
            |> List.map dbFeatureDetailToDomain
            |> sequenceResultsList
            |> Result.mapError FeatureError.InvalidPropertyValue
            |> Task.FromResult

        return { feature with Details = Some details }
    }

let listFeaturesForCamp (campId: int) : Task<Result<Feature list, FeatureError>> =
    taskResult {
        let! dbFeatures = getFeaturesForCamp campId |> mapTaskError FeatureError.DbError

        let mutable acc: Feature list = []
        for db in dbFeatures do
            let! feature =
                taskResult {
                    let! feature = dbFeatureToDomain db |> Result.mapError FeatureError.InvalidPropertyValue |> Task.FromResult
                    let! dbDetails = getFeatureDetails db.Id |> mapTaskError FeatureError.DbError
                    let! details =
                        dbDetails
                        |> List.map dbFeatureDetailToDomain
                        |> sequenceResultsList
                        |> Result.mapError FeatureError.InvalidPropertyValue
                        |> Task.FromResult

                    return { feature with Details = Some details }
                }

            acc <- feature :: acc

        return List.rev acc
    }

let getFeatureByIdWithDetails (featureId: int) : Task<Result<Feature, FeatureError>> =
    loadFeatureWithDetails featureId

let createFeature (dto: CreateFeatureDto) : Task<Result<Feature, FeatureError>> =
    taskResult {
        let! feature = validateAndBuildFeature dto |> Task.FromResult

        let! newId = feature |> domainFeatureToNewDb |> addNewFeature |> mapTaskError FeatureError.DbError

        match feature.Details with
        | Some details ->
            for detail in details do
                let detailWithFeature = { detail with FeatureId = newId }
                let dbDetail = featureDetailToNewDb newId detailWithFeature
                let! _ = addNewFeatureDetail dbDetail |> mapTaskError FeatureError.DbError
                ()
        | None -> ()

        return! loadFeatureWithDetails newId
    }



let deleteFeature (featureId: int) : Task<Result<unit, FeatureError>> =
    taskResult {
        let! _ =
            task {
                let! dbFeature = backend_tenty.Database.getFeatureById featureId
                return mapDbResultToFeatureError featureId dbFeature
            }

        let! _ = deleteFeatureDetails featureId |> mapTaskError FeatureError.DbError

        let! deleted = deleteFeatureById featureId |> mapTaskError FeatureError.DbError
        
        if deleted = 0 then
            return! Error(FeatureError.FeatureNotFound featureId)

        return ()
    }
    
let updateFeature (featureId: int) (dto: FeatureDto) : Task<Result<Feature, FeatureError>> =
    taskResult {
        // 1. Existenz-Check
        let! _ =
            task {
                let! dbFeature = getFeatureById featureId
                return mapDbResultToFeatureError featureId dbFeature
            }

        // 2. Domain-Feature mit bestehender Id bauen
        let! feature =
            validateAndBuildUpdatedFeature featureId dto
            |> Task.FromResult

        // 3. In DbFeature (mit Id) konvertieren und updaten
        let! affected =
            feature
            |> domainFeatureToDb          // wichtig: Variante mit Id
            |> updateDbFeature            // kein Insert, sondern echtes UPDATE
            |> mapTaskError FeatureError.DbError

        match affected with
        | 0 ->
            // nichts geändert -> wie "nicht gefunden"
            return! Error (FeatureError.FeatureNotFound featureId)
        | _ ->
            // 4. Details ersetzen
            let! _ = deleteFeatureDetails featureId |> mapTaskError FeatureError.DbError

            match feature.Details with
            | Some details ->
                for detail in details do
                    let detailWithFeature = { detail with FeatureId = featureId }
                    let dbDetail = featureDetailToNewDb featureId detailWithFeature
                    let! _ = addNewFeatureDetail dbDetail |> mapTaskError FeatureError.DbError
                    ()
            | None -> ()

            // 5. aktualisiertes Feature zurückladen
            return! loadFeatureWithDetails featureId
    }

