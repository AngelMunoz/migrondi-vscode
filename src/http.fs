module Migrondi.VSCode.Http

open Fable.Core
open Fable.Core.JsInterop
open Migrondi.VSCode.Types
open Thoth.Json
open Node.Api

let private axios: Fable.Import.Axios.AxiosStatic = importDefault "axios"

let private access (pathLike: string) : JS.Promise<unit> = importMember "fs/promises"
let private downloadAndExtract (url: string, extractTo: string) : JS.Promise<unit> = importMember "./interop"

let private decodeReleases data =
    match Decode.fromValue "" Release.Decoder data with
    | Ok result -> Some result
    | Error err ->
        eprintfn $"{err}"
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


let private getLatestRelease () =
    axios.get "https://api.github.com/repos/AngelMunoz/Migrondi/releases/latest"
    |> Promise.map (fun result -> decodeReleases result.data)

let private getAssetWithName name (release: JS.Promise<Release option>) =
    release
    |> Promise.map (
        Option.map
            (fun release ->
                release.assets
                |> List.tryFind (fun asset -> asset.name = name)
                |> Option.map (fun asset -> asset, release.tag_name))
    )

let downloadIfNotExists binaryName downloadPath =
    promise {
        let! asset =
            getLatestRelease ()
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

let checkandUpdate binaryName downloadPath =
    promise {
        let! asset =
            getLatestRelease ()
            |> getAssetWithName binaryName
            |> Promise.map (Option.flatten)

        match asset with
        | Some (asset, version) ->
            try
                do! access $"{downloadPath}/{version}"
                return Some version
            with
            | _ -> return! downloadIfNotExists binaryName downloadPath
        | None -> return None
    }
