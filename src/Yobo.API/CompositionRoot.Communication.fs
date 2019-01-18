module Yobo.API.CompositionRoot.Communication

open System
open FSharp.Rop
open Yobo.Shared.Communication

let private toAsync f = async { return f }

module Security =
    open Yobo.Shared.Auth
    open Yobo.Shared.Auth.Domain

    let handleForUser f (user:LoggedUser, param)  =
        let handleFn = Services.CommandHandler.handleForUser user.Id
        f handleFn param

    let onlyForLogged (sp:SecuredParam<_>) =
        match sp.Token |> Services.Users.authorizator.ValidateToken with
        | Some claims -> claims |> Yobo.API.Auth.Functions.claimsToUser |> fun user -> (user, sp.Param) |> Ok
        | None -> AuthError.InvalidOrExpiredToken |> ServerError.AuthError |> Error

    let onlyForAdmin (sp:SecuredParam<_>) =
        sp |> onlyForLogged
        >>= (fun (u,p) ->
            match u with
            | { IsAdmin = true } -> (u, p) |> Ok
            | _ -> AuthError.InvalidOrExpiredToken |> ServerError.AuthError |> Error
        )

module Auth =
    open Yobo.API.Auth.Functions
    open Yobo.Libraries.Security 
    open Yobo.API.CompositionRoot
    open Yobo.API
    open Yobo.Shared.Auth.Domain

    let private adminUser =
        {
            Id = Guid("f65203d4-60dd-4580-a31c-e538807ef720")
            Email = Configuration.Admin.email
            FirstName = "Admin"
            LastName = "Admin"
            IsAdmin = true
        } : LoggedUser

    let private loginWithAdmin email pwd =
        if email = Configuration.Admin.email && pwd = Configuration.Admin.password then Ok adminUser
        else
            Services.Users.authenticator.Login email pwd

    let api : Yobo.Shared.Auth.Communication.API = {
        GetToken = getToken loginWithAdmin (Services.Users.authorizator.CreateToken >> fun x -> x.AccessToken) >> toAsync
        RefreshToken = refreshToken Services.Users.authorizator.ValidateToken (Services.Users.authorizator.CreateToken >> fun x -> x.AccessToken) >> toAsync
        GetUserByToken = getUser Services.Users.authorizator.ValidateToken >> toAsync
        ResendActivation = resendActivation Services.CommandHandler.handleAnonymous >> toAsync
        Register = register Services.CommandHandler.handleAnonymous Password.createHash >> toAsync
        ActivateAccount = activateAccount Services.CommandHandler.handleAnonymous Services.Users.authenticator.GetByActivationKey >> toAsync
    }

module Admin =
    open Yobo.API.Admin.Functions
    open Yobo.API.CompositionRoot

    let api : Yobo.Shared.Admin.Communication.API = {
        GetAllUsers = fun x -> x |> Security.onlyForAdmin <!> snd >>= Services.Users.queries.GetAll |> toAsync
        AddCredits = fun x -> x |> Security.onlyForAdmin >>= Security.handleForUser addCredits |> toAsync
        GetLessonsForDateRange = fun x -> x |> Security.onlyForAdmin <!> snd >>= Services.Lessons.queries.GetAllForDateRange |> toAsync
        AddLessons = fun x -> x |> Security.onlyForAdmin >>= Security.handleForUser addLessons |> toAsync
    }

module Calendar =
    open Yobo.API.CompositionRoot
    open Yobo.Shared.Calendar.Domain

    let api : Yobo.Shared.Calendar.Communication.API = {
        GetLessonsForDateRange =
            (fun x ->
                x |> Security.onlyForLogged
                >>= (fun (u,p) ->
                    p |> Services.Lessons.queries.GetAllForDateRange <!> List.map (Lesson.FromAdminLesson u.Id)
                ) 
                |> toAsync
            )
    }