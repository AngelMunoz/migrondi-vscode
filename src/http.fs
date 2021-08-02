module Migrondi.VSCode.Http

open Fable.Core
open Fable.Core.JsInterop
open Migrondi.VSCode.Types
open Thoth.Json
open Node.Api
open Fable.Import.vscode

let private axios: Fable.Import.Axios.AxiosStatic = importDefault "axios"

let private access (pathLike: string) : JS.Promise<unit> = importMember "fs/promises"
let private downloadAndExtract (url: string, extractTo: string) : JS.Promise<unit> = importMember "./interop"

let private decodeReleases (channel: OutputChannel) data =
    match Decode.fromValue "" Release.Decoder data with
    | Ok result -> Some result
    | Error err ->
        channel.appendLine $"{err}"
        None

let private getLastRelease releases =
    releases
    |> List.filter (fun r -> r.prerelease |> not && r.draft |> not)
    |> List.tryHead

let private parseVersion (version: string) =
    let parts = version.Split('.')
    let major = parts.[0].[1]
    let minor = parts.[1]
    let patch = parts.[2]
    (major |> int, minor |> int, patch |> int)


let private getLatestRelease channel =
    axios.get "https://api.github.com/repos/AngelMunoz/Migrondi/releases/latest"
    |> Promise.map (fun result -> decodeReleases channel result.data)

let private getAssetWithName name (release: JS.Promise<Release option>) =
    release
    |> Promise.map (
        Option.map
            (fun release ->
                release.assets
                |> List.tryFind (fun asset -> asset.name = name)
                |> Option.map (fun asset -> asset, release.tag_name))
    )

let downloadIfNotExists channel binaryName downloadPath =
    promise {
        let! asset =
            getLatestRelease channel
            |> getAssetWithName binaryName
            |> Promise.map (Option.flatten)

        match asset with
        | Some (asset, version) ->
            do!
                downloadAndExtract (
                    $"https://api.github.com/repos/AngelMunoz/Migrondi/releases/assets/{asset.id}",
                    $"{downloadPath}/{version}"
                )

            return Some version
        | None ->
            raise (exn "No asset found")
            return None
    }

let checkandUpdate channel binaryName downloadPath =
    promise {
        let! asset =
            getLatestRelease channel
            |> getAssetWithName binaryName
            |> Promise.map (Option.flatten)

        match asset with
        | Some (asset, version) ->
            try
                do! access $"{downloadPath}/{version}"
                return Some version
            with
            | _ -> return! downloadIfNotExists channel binaryName downloadPath
        | None -> return None
    }
