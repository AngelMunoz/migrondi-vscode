module Migrondi.VSCode.Extension

open Fable.Core
open Fable.Core.JsInterop

open Fable.Import.vscode
open Node.Api

open Migrondi.VSCode.Types
open Migrondi.VSCode.Http

let private getOrCreatePath (context: ExtensionContext) : JS.Promise<string> = importMember "./interop"

[<RequireQualifiedAccess>]
type private MigrondiState =
    | NotReady
    | Ready
    | Updating
    | ExecutingCommand

    member this.AsString() =
        match this with
        | NotReady -> "NotReady"
        | Ready -> "Ready"
        | Updating -> "Updating"
        | ExecutingCommand -> "ExecutingCommand"

    static member FromString(s: string) =
        match s with
        | "NotReady" -> NotReady
        | "Ready" -> Ready
        | "Updating" -> Updating
        | "ExecutingCommand" -> ExecutingCommand
        | _ -> failwith "Invalid state"

let mutable private migrondiState = MigrondiState.NotReady

let private updateMigrondiState state =
    if migrondiState <> state then
        migrondiState <- state

let private migrondiCmds =
    [ "migrondi-vscode.init", Commands.Init.Command
      "migrondi-vscode.new", Commands.New.Command ]

let private migrondiExists
    (downloadIfNotExists: unit -> JS.Promise<unit>)
    (checkandUpdate: unit -> JS.Promise<unit>)
    (err: Node.Base.ErrnoException option)
    =
    promise {
        match err with
        | Some err ->
            printfn $"Migrondi: Migrondi binary not on path: {err.message}, will download"
            do! downloadIfNotExists ()
        | None ->
            printfn "Migrondi: Migrondi is on path, checking for updates"
            do! checkandUpdate ()
    }

let private getMigrondiBinFileName platform arch =
    let platform =
        match platform with
        | Node.Base.Darwin -> "osx"
        | Node.Base.Linux -> "linux"
        | Node.Base.Win32 -> "win10"
        | platform -> failwith $"Platform: {platform} not supported"

    let arch =
        match arch with
        | Node.Base.Arch.Arm64 -> "arm64"
        | Node.Base.Arch.X64 -> "x64"
        | arch -> failwith $"Arch: {arch} not supported"

    $"{platform}-{arch}.zip"

let initializeMigrondi filename path setMigrondiVersion =
    Promise.create
        (fun resolve reject ->
            let downloadIfNotExists () =
                promise {
                    match! downloadIfNotExists filename path with
                    | Some version -> setMigrondiVersion version
                    | None -> ()
                }

            let checkAndUpdate () =
                promise {
                    match! checkandUpdate filename path with
                    | Some version -> setMigrondiVersion version
                    | None -> ()
                }

            let accessCallback err =
                migrondiExists downloadIfNotExists checkAndUpdate err
                |> Promise.map resolve
                |> Promise.catch reject
                |> Promise.start

            fs.access (U2.Case1 path, accessCallback))

let activate (context: ExtensionContext) : JS.Promise<unit> =
    printfn "Activating Migrondi"

    promise {
        let zipFilename =
            getMigrondiBinFileName (os.platform ()) (os.arch ())

        let! basePath = getOrCreatePath context

        let setMigrondiVersion version =
            context.globalState.update ("migrondi-version", version)
            |> ignore

        try
            updateMigrondiState MigrondiState.Updating
            do! initializeMigrondi zipFilename $"{basePath}/migrondi" setMigrondiVersion
        with
        | err ->
            eprintfn $"Migrondi: Error initializing migrondi: {err.Message}"
            updateMigrondiState MigrondiState.NotReady
            return ()

        do! context.globalState.update ("migrondi-path", $"{basePath}/migrondi")
        updateMigrondiState MigrondiState.Ready

        context.subscriptions.AddRange [ for (command, cb) in migrondiCmds do
                                             commands.registerCommand (command, cb context) ]
    }


let deactivate (disposables: Disposable []) =
    printfn "Deactivating Migrondi"
    updateMigrondiState MigrondiState.NotReady

    disposables
    |> Array.iter (fun d -> d.dispose () |> ignore)
