module backend_tenty.Model.Validation.ValidateDbData
open System
open System.Net.Mail
open backend_tenty.Model.Types

let validateName(first: string) (middle: string option) (last: string) : Result<NameInformation, string> =
    let firstR = String50.create first
    let middleR = middle |> Option.bind String50.create
    let lastR = String50.create last
    match firstR, middleR, lastR with
    | Some f, None, Some l -> Ok {FirstName = f; MiddleName = None; LastName = l}
    | Some f, Some m, Some l -> Ok {FirstName = f; MiddleName = Some m; LastName = l}
    | _ -> Error "Invalid name parts (empty or >50 chars)"
    
let validateMail (mail: string) : Result<MailInformation, string> =
    try
        Ok {MailAddress = MailAddress(mail)}
    with _ -> Error $"Invalid email: {mail}"
    
    
    

