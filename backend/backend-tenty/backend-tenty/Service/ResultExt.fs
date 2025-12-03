module backend_tenty.Service.ResultExt
    let mapError f = function
        | Ok x -> Ok x
        | Error e -> Error (f e)
        
    type ResultBuilder() =
        member _.Bind(x, f) = Result.bind f x
        member _.Return x = Ok x
        member _.ReturnFrom(x) = x
    
    let (>>=) r f = Result.bind f r
    let (|>>) r f = Result.map f r
    
    
    let result = ResultBuilder()
    
    //Helper Functions to create CampUsers
    let sequenceResultsList (results: Result<'a, 'e> list) : Result<'a list, 'e> =
        let folder acc r =
            match acc, r with
            | Ok xs, Ok x -> Ok (x :: xs)
            | Error e, _ -> Error e
            | _, Error e -> Error e
        List.fold folder (Ok []) results |> Result.map List.rev

