module Migrondi.VSCode.Http

open Fable.Import.Axios
open Fable.Core
open Fable.Core.JsInterop
open Migrondi.VSCode.Types
open Thoth.Json
open Node.Api

let private downloadFile (url: string, filepath: string) : JS.Promise<unit> = importMember "./interop"

let private axios = Fable.Import.Globals.axios

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

let downloadfile (url: string) (fullPath: string) =
    promise {
        let config =
            {| headers = {| accept = "application/octet-stream" |}
               responseType = "stream" |}
        // do an ugly hack to get the latest release
        let! response = axios.get (url, box config :?> AxiosXHRConfigBase<obj>)
        let stream = fs.createWriteStream fullPath

        (response.data :?> Node.Stream.Readable<string>)
            .pipe (stream)
        |> ignore

        return
            Promise.create
                (fun resolve reject ->
                    stream
                        .on(
                            "error",
                            fun err ->
                                stream.close ()
                                reject (err)
                        )
                        .on ("finish", resolve)
                    |> ignore)
    }


let downloadIfNotExists binaryName downloadPath =
    promise {
        let! result = axios.get "https://api.github.com/repos/AngelMunoz/Migrondi/releases/latest"

        match decodeReleases result.data with
        | Some release ->
            match release.assets
                  |> List.tryFind (fun asset -> asset.name = binaryName) with
            | Some asset ->
                do!
                    downloadFile (
                        $"https://api.github.com/repos/AngelMunoz/Migrondi/releases/assets/{asset.id}",
                        downloadPath
                    )

                ()
            | None -> ()
        | None -> ()
    }

let checkandUpdate () = ()
