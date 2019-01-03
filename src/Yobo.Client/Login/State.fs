module Yobo.Client.Login.State

open Yobo.Client.Login.Domain
open Elmish
open Yobo.Client.Http

let update (msg : Msg) (state : State) : State * Cmd<Msg> =
    match msg with
    | Login -> { state with IsLogging = true }, (state.Login |> Cmd.ofAsyncResult loginAPI.Login LoginDone)
    | ResendActivation id -> state, (id |> Cmd.ofAsyncResult loginAPI.ResendActivation ResendActivationDone)
    | LoginDone res -> { state with IsLogging = false; LoginResult = Some res }, Cmd.none
    | ResendActivationDone res -> { state with ResendActivationResult = Some res }, Cmd.none
    | ChangeEmail v -> { state with Login = { state.Login with Email = v }}, Cmd.none
    | ChangePassword v -> { state with Login = { state.Login with Password = v }}, Cmd.none