module Migrondi.VSCode.Extension

open Fable.Core
open Fable.Core.JsInterop

open Fable.Import.VSCode.Vscode
open Node.Api

open Migrondi.VSCode.Types
open Migrondi.VSCode.Http

let private getOrCreatePath (context: ExtensionContext, channel: OutputChannel) : JS.Promise<string> =
    importMember "./interop"

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
      "migrondi-vscode.new", Commands.New.Command
      "migrondi-vscode.up", Commands.Up.Command
      "migrondi-vscode.down", Commands.Down.Command
      "migrondi-vscode.list", Commands.List.Command ]

let private migrondiExists
    (channel: OutputChannel)
    (downloadIfNotExists: unit -> JS.Promise<unit>)
    (checkandUpdate: unit -> JS.Promise<unit>)
    (err: Node.Base.ErrnoException option)
    =
    promise {
        match err with
        | Some err ->
            channel.appendLine $"Migrondi: Migrondi binary not on path: {err.message}, will download"
            do! downloadIfNotExists ()
        | None ->
            channel.appendLine "Migrondi: Migrondi is on path, checking for updates"
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

let initializeMigrondi filename path setMigrondiVersion channel =
    Promise.create
        (fun resolve reject ->
            let downloadIfNotExists () =
                promise {
                    match! downloadIfNotExists channel filename path with
                    | Some version -> setMigrondiVersion version
                    | None -> ()
                }

            let checkAndUpdate () =
                promise {
                    match! checkandUpdate channel filename path with
                    | Some version -> setMigrondiVersion version
                    | None -> ()
                }

            let accessCallback err =
                migrondiExists channel downloadIfNotExists checkAndUpdate err
                |> Promise.map resolve
                |> Promise.catch reject
                |> Promise.start

            fs.access (U2.Case1 path, accessCallback))

let subscriptions = ResizeArray<Disposable>([])

let activate (context: ExtensionContext) : JS.Promise<unit> =
    printfn "Activating Migrondi"
    let appChannel = window.createOutputChannel ("Migrondi")

    let diagnosticsChannel =
        window.createOutputChannel ("Migrondi: Diagnostics")

    promise {
        let state = (context.globalState :?> Memento)

        let zipFilename =
            getMigrondiBinFileName (os.platform ()) (os.arch ())

        let! basePath = getOrCreatePath (context, diagnosticsChannel)

        let setMigrondiVersion (version: string) =
            state.update ("migrondi-version", version |> box |> Some)
            |> ignore

        try
            updateMigrondiState MigrondiState.Updating
            do! initializeMigrondi zipFilename $"{basePath}/migrondi" setMigrondiVersion diagnosticsChannel
        with
        | err ->
            diagnosticsChannel.appendLine $"Migrondi: Error initializing migrondi: {err.Message}"
            updateMigrondiState MigrondiState.NotReady
            return ()

        do! state.update ("migrondi-path", $"{basePath}/migrondi" |> box |> Some)
        updateMigrondiState MigrondiState.Ready
        diagnosticsChannel.appendLine "Activated Migrondi Successfully"

        subscriptions.AddRange [ for (command, cb) in migrondiCmds do
                                     commands.registerCommand (command, cb context appChannel)
                                 appChannel :?> Disposable
                                 diagnosticsChannel :?> Disposable ]
    }


let deactivate () =
    printfn "Deactivating Migrondi"
    updateMigrondiState MigrondiState.NotReady
    printfn "Disposing: %A" subscriptions

    subscriptions
    |> Seq.iter (fun d -> d.dispose () |> ignore)

    subscriptions.Clear()
