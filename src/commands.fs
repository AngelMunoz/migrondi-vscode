namespace Migrondi.VSCode.Commands

open Fable.Import.vscode
open Node.Api
open Fable.Core
open Node.Buffer
open Node.ChildProcess

module private Helpers =
    let execPath (context: ExtensionContext) =
        let path =
            context.globalState.get<string> ("migrondi-path")
            |> Option.defaultValue ""

        let version =
            context.globalState.get<string> ("migrondi-version")
            |> Option.defaultValue ""

        let ext =
            if ``process``.platform = Node.Base.Platform.Win32 then
                ".exe"
            else
                ""

        $"{path}/{version}/Migrondi{ext}"



[<RequireQualifiedAccess>]
module Init =
    open Helpers

    let private initCb (error: ExecError option) (stdout: U2<string, Buffer>) (stderr: U2<string, Buffer>) =
        promise {
            match error with
            | None ->
                let stdout: string = stdout |> box |> unbox
                let stderr: string = stderr |> box |> unbox

                if JsInterop.isNullOrUndefined stdout && stdout <> "" then
                    do!
                        window.showInformationMessage (stdout)
                        |> Promise.map ignore

                if JsInterop.isNullOrUndefined stderr && stderr <> "" then
                    do!
                        window.showErrorMessage (stderr)
                        |> Promise.map ignore
            | Some error ->
                do!
                    window.showErrorMessage $"Failed to run migrondi: {error.message}"
                    |> Promise.map ignore
        }
        |> ignore

    let Command (context: ExtensionContext) (channel: OutputChannel) =
        fun (_: obj) ->
            promise {
                let ws =
                    workspace.workspaceFolders |> Array.tryHead

                match ws with
                | Some ws ->
                    let migrondiExe = execPath context

                    let opts =
                        {| cwd = ws.uri.fsPath |} |> box :?> Node.ChildProcess.ExecOptions

                    childProcess.exec ($"{migrondiExe} init", opts, initCb)
                    |> ignore
                | None -> ()
            }
            :> obj

[<RequireQualifiedAccess>]
module New =
    open Helpers

    let private newCb (error: ExecError option) (stdout: U2<string, Buffer>) (stderr: U2<string, Buffer>) =
        promise {
            match error with
            | None ->
                let stdout: string = stdout |> box |> unbox
                let stderr: string = stderr |> box |> unbox

                if JsInterop.isNullOrUndefined stdout && stdout <> "" then
                    do!
                        window.showInformationMessage (stdout)
                        |> Promise.map ignore

                if JsInterop.isNullOrUndefined stderr && stderr <> "" then
                    do!
                        window.showErrorMessage (stderr)
                        |> Promise.map ignore
            | Some error -> eprintf $"Migrondi Error: {error}"
        }
        |> ignore

    let Command (context: ExtensionContext) (channel: OutputChannel) =
        fun (_: obj) ->
            promise {
                let ws =
                    workspace.workspaceFolders |> Array.tryHead

                match ws with
                | Some ws ->
                    let migrondiExe = execPath context

                    let inputOpts =
                        {| value = "SampleMigration"
                           title = "New Migration"
                           prompt = "The name of your migration"
                           placeHolder = "create-users-table"
                           ignoreFocusOut = true |}
                        |> box
                        :?> InputBoxOptions

                    let! value =
                        window.showInputBox (inputOpts)
                        |> Promise.map (Option.ofObj >> Option.defaultValue "")

                    let execOpts =
                        {| cwd = ws.uri.fsPath |} |> box :?> Node.ChildProcess.ExecOptions

                    childProcess.exec ($"{migrondiExe} new -n {value}", execOpts, newCb)
                    |> ignore
                | None -> ()
            }
            :> obj

[<RequireQualifiedAccess>]
module Up =
    open Helpers

    let private upCb
        (channel: OutputChannel)
        (error: ExecError option)
        (stdout: U2<string, Buffer>)
        (stderr: U2<string, Buffer>)
        =
        promise {
            channel.show ()
            channel.clear ()

            match error with
            | None ->
                let stdout: string = stdout |> box |> unbox
                let stderr: string = stderr |> box |> unbox

                if not <| JsInterop.isNullOrUndefined stdout
                   && stdout <> "" then
                    let migration =
                        stdout.Trim().Split(path.sep) |> Seq.last

                    channel.appendLine $"{migration}"

                if not <| JsInterop.isNullOrUndefined stderr
                   && stderr <> "" then
                    channel.appendLine $"{stderr}"

            | Some error ->
                eprintf $"Migrondi Error: {error}"
                channel.appendLine $"Migrondi Error: {error}"
        }
        |> ignore

    let Command (context: ExtensionContext) (channel: OutputChannel) _ =
        promise {
            let ws =
                workspace.workspaceFolders |> Array.tryHead

            match ws with
            | Some ws ->
                let migrondiExe = execPath context

                let inputOpts =
                    {| value = "1"
                       title = "Run Migrations Up"
                       prompt = "Amount of migrations to run, leave -1 to run all"
                       placeHolder = "1 (-1 to run all pending)"
                       ignoreFocusOut = true |}
                    |> box
                    :?> InputBoxOptions

                let! value =
                    window.showInputBox (inputOpts)
                    |> Promise.map (Option.ofObj >> Option.defaultValue "")

                let! isDry =
                    let quickPickOpts =
                        {| canPickMany = false
                           ignoreFocusOut = true
                           placeHolder = "Yes"
                           title = "Is this a dry run?" |}
                        |> box
                        :?> QuickPickOptions

                    window.showQuickPick (U2.Case1(ResizeArray([ "Yes"; "No" ])), quickPickOpts)
                    |> Promise.map (Option.ofObj >> Option.defaultValue "")

                let amount =
                    try
                        value |> int
                    with
                    | _ ->
                        channel.appendLine $"Failed to parse {value} defaulting to 0"
                        0

                let execOpts =
                    {| cwd = ws.uri.fsPath |} |> box :?> Node.ChildProcess.ExecOptions

                let total =
                    if amount < 0 then
                        ""
                    else
                        $"-t {amount}"

                let dryRun =
                    if isDry.ToLowerInvariant() = "yes" then
                        "-d true"
                    else
                        ""

                childProcess.exec ($"{migrondiExe} up {total} {dryRun}", execOpts, upCb channel)
                |> ignore
            | None -> ()
        }
        :> obj

[<RequireQualifiedAccess>]
module Down =
    open Helpers

    let private downCb
        (channel: OutputChannel)
        (error: ExecError option)
        (stdout: U2<string, Buffer>)
        (stderr: U2<string, Buffer>)
        =
        promise {
            channel.show ()
            channel.clear ()

            match error with
            | None ->
                let stdout: string = stdout |> box |> unbox
                let stderr: string = stderr |> box |> unbox

                if not <| JsInterop.isNullOrUndefined stdout
                   && stdout <> "" then
                    let migration =
                        stdout.Trim().Split(path.sep) |> Seq.last

                    channel.appendLine $"{migration}"

                if not <| JsInterop.isNullOrUndefined stderr
                   && stderr <> "" then
                    channel.appendLine $"{stderr}"

            | Some error -> channel.appendLine $"Migrondi Error: {error}"
        }
        |> ignore

    let Command (context: ExtensionContext) (channel: OutputChannel) _ =
        promise {
            let ws =
                workspace.workspaceFolders |> Array.tryHead

            match ws with
            | Some ws ->
                let migrondiExe = execPath context

                let inputOpts =
                    {| value = "1"
                       title = "Run Migrations Up"
                       prompt = "Amount of migrations to run, leave -1 to run all"
                       placeHolder = "1 (-1 to run all pending)"
                       ignoreFocusOut = true |}
                    |> box
                    :?> InputBoxOptions

                let! value =
                    window.showInputBox (inputOpts)
                    |> Promise.map (Option.ofObj >> Option.defaultValue "")

                let! isDry =
                    let quickPickOpts =
                        {| canPickMany = false
                           ignoreFocusOut = true
                           placeHolder = "Yes"
                           title = "Is this a dry run?" |}
                        |> box
                        :?> QuickPickOptions

                    window.showQuickPick (U2.Case1(ResizeArray([ "Yes"; "No" ])), quickPickOpts)
                    |> Promise.map (Option.ofObj >> Option.defaultValue "")

                let amount =
                    try
                        value |> int
                    with
                    | _ ->
                        channel.appendLine $"Failed to parse {value} defaulting to 0"
                        0

                let execOpts =
                    {| cwd = ws.uri.fsPath |} |> box :?> Node.ChildProcess.ExecOptions

                let total =
                    if amount < 0 then
                        ""
                    else
                        $"-t {amount}"

                let dryRun =
                    if isDry.ToLowerInvariant() = "yes" then
                        "-d true"
                    else
                        ""

                childProcess.exec ($"{migrondiExe} down {total} {dryRun}", execOpts, downCb channel)
                |> ignore
            | None -> ()
        }
        :> obj
