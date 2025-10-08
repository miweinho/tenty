module backend_tenty.Model

open System
open System.Net.Mail

type String50 = private String50 of string

module String50 =
    let create (input: string) =
        if String.IsNullOrEmpty(input) || input.Length > 50 then
            None
        else
            Some (String50 input)
    
    let value (String50 s) = s
    
type SwissCoordinate = private SwissCoordinate of decimal * decimal

module SwissCoordinate =
    let create (x: decimal) (y: decimal) =
        let isValidSwissCoordinate (xCoord: decimal) (yCoord: decimal) =
            // Swiss CH1903+ / LV95 coordinate ranges
            let xMin = 2480000m
            let xMax = 2840000m
            let yMin = 1070000m
            let yMax = 1300000m
            
            xCoord >= xMin && xCoord <= xMax && 
            yCoord >= yMin && yCoord <= yMax

        if isValidSwissCoordinate x y then
            Some (SwissCoordinate (x, y))
        else
            None
        // Zugriffs-Funktionen
    let x (SwissCoordinate (xCoord, _)) = xCoord
    let y (SwissCoordinate (_, yCoord)) = yCoord
    let value (SwissCoordinate (xCoord, yCoord)) = (xCoord, yCoord)


type NameInformation = {
    FirstName : String50
    MiddleName: String50 option
    LastName: String50
}

type MailInformation = {
    MailAddress : MailAddress
    
}

type User = {
    Id: Guid
    NameInformation : NameInformation
    MailInformation : MailInformation
}

type UserPermission =
    | Read
    | Write
    | Admin
    
type CampUser = {
    User: User
    Permission: UserPermission   
}


type Camp = {
    Id: int
    //owner: User
    Name: string
    Location: SwissCoordinate
    //UserList: CampUser list
    Delimitation: SwissCoordinate list
}

type SwissCoordinateDto = {
    x: decimal
    y: decimal
}

type CampUserDto = {
    UserId: string
    Permission: string
}

type UserDto = {
    Id: string
    FirstName: string
    MiddleName: string option
    LastName: string
    Email: string
    
}

type CampDto = {
    Id: string
    Name: string
    Location: SwissCoordinateDto
    Delimitation: SwissCoordinateDto array
    //owner: User
    //UserList: CampUserDto list
}
