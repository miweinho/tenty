module Tests.Model.Validation.ValidationDbData



open Xunit
open FsCheck
open FsCheck.Xunit
open backend_tenty.Model.Types
open backend_tenty.Model.Validation.ValidateDbData


[<Property>]
let ``valid first+last without middle returns Ok with MiddleName = None`` (f:string) (l:string) =
    match String50.create f, String50.create l with
    | Some f', Some l' ->
        let res = validateName f None l
        res = Ok { FirstName = f'; MiddleName = None; LastName = l' }
    | _ -> true

[<Property>]
let ``valid first+middle+last returns Ok with MiddleName = Some`` (f:string) (m:string) (l:string) =
    match String50.create f, String50.create m, String50.create l with
    | Some f', Some m', Some l' ->
        let res = validateName f (Some m) l
        res = Ok { FirstName = f'; MiddleName = Some m'; LastName = l' }
    | _ -> true

[<Property>]
let ``invalid first or last yields Error`` (f:string) (m:string option) (l:string) =
    if Option.isNone (String50.create f) || Option.isNone (String50.create l) then
        match validateName f m l with Error _ -> true | Ok _ -> false
    else true

[<Property>]
let ``invalid middle treated as missing when first+last valid`` (f:string) (m:string) (l:string) =
    match String50.create f, String50.create m, String50.create l with
    | Some f', None, Some l' ->
        let res = validateName f (Some m) l
        res = Ok { FirstName = f'; MiddleName = None; LastName = l' }
    | _ -> true

[<Fact>]
let ``valid email returns MailInformation`` () =
    match validateMail "john.doe@example.com" with
    | Ok info -> Assert.Equal("john.doe@example.com", info.MailAddress.Address)
    | Error err -> failwithf "Expected Ok but got Error: %s" err

[<Theory>]
[<InlineData("")>]
[<InlineData("not-an-email")>]
[<InlineData("missing-domain@")>]
[<InlineData("@missing-user.com")>]
let ``invalid email yields Error`` (email: string) =
    match validateMail email with
    | Ok info ->
        failwithf "Expected Error for %s but got %s" email info.MailAddress.Address
    | Error msg -> Assert.Contains(email, msg)
