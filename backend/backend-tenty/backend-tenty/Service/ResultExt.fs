module backend_tenty.Service.ResultExt
    /// Maps the error value of a ``Result`` using the provided function.
    let mapError f = function
        | Ok x -> Ok x
        | Error e -> Error (f e)

    /// Computation expression builder for working ergonomically with ``Result`` values.
    type ResultBuilder() =
        member _.Bind(x, f) = Result.bind f x
        member _.Return x = Ok x
        member _.ReturnFrom(x) = x

    /// Infix bind for chaining ``Result`` computations.
    let (>>=) r f = Result.bind f r
    /// Infix map for transforming the success case of a ``Result``.
    let (|>>) r f = Result.map f r


    /// Instance of the ``Result`` computation expression builder.
    let result = ResultBuilder()

    /// Aggregates a list of ``Result`` values, short-circuiting on the first error.
    let sequenceResultsList (results: Result<'a, 'e> list) : Result<'a list, 'e> =
        let folder acc r =
            match acc, r with
            | Ok xs, Ok x -> Ok (x :: xs)
            | Error e, _ -> Error e
            | _, Error e -> Error e
        List.fold folder (Ok []) results |> Result.map List.rev

