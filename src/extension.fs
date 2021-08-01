module Migrondi.VSCode.Extension

open Fable.Core
open Fable.Core.JsInterop

open Fable.Import.vscode
open Node.Api

open Migrondi.VSCode.Types

let getOrCreatePath (context: ExtensionContext) : JS.Promise<string> = importMember "./interop.ts"

[<RequireQualifiedAccess>]
type MigrondiState =
    | NotReady
    | Ready
    | Updating
    | ExecutingCommand

let mutable private migrondiState = MigrondiState.NotReady

let updateMigrondiState state =
    if migrondiState <> state then
        migrondiState <- state


let private migrondiCmds =
    [ "migrondi-vscode.init", (fun (_: obj) -> window.showInformationMessage ("init requested") :> obj)
      "migrondi-vscode.new", (fun (_: obj) -> window.showInformationMessage ("new requested") :> obj)
      "migrondi-vscode.up", (fun (_: obj) -> window.showInformationMessage ("up requested") :> obj)
      "migrondi-vscode.down", (fun (_: obj) -> window.showInformationMessage ("down requested") :> obj)
      "migrondi-vscode.list", (fun (_: obj) -> window.showInformationMessage ("list requested") :> obj) ]

let migrondiExists
    (downloadIfNotExists: unit -> unit)
    (checkandUpdate: unit -> unit)
    (err: Node.Base.ErrnoException option)
    =
    match err with
    | Some err ->
        eprintfn $"Migrondi: Migrondi binary not on path: {err.message}"
        updateMigrondiState MigrondiState.Updating
        downloadIfNotExists ()
    | None ->
        printfn "Migrondi: Migrondi is on path, checking for updates"
        checkandUpdate ()

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

let initializeMigrondi path =
    fs.access (U2.Case1 "", migrondiExists ignore ignore)

let activate (context: ExtensionContext) : JS.Promise<unit> =
    printfn "Activating Migrondi"

    promise {
        let migrondiFile =
            getMigrondiBinFileName (os.platform ()) (os.arch ())

        let! basePath = getOrCreatePath context
        initializeMigrondi $"{basePath}/migrondi"

        context.subscriptions.AddRange [ for (command, cb) in migrondiCmds do
                                             commands.registerCommand (command, cb) ]
    }


let deactivate (disposables: Disposable []) =
    printfn "Deactivating Migrondi"

    disposables
    |> Array.iter (fun d -> d.dispose () |> ignore)
