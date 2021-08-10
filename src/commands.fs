namespace Migrondi.VSCode.Commands

open Fable.Import.VSCode.Vscode
open Node.Api
open Fable.Core
open Migrondi.VSCode.Types
open Fable.Core.JsInterop

module private Helpers =
    let private execPath (context: ExtensionContext) =
        let state = (context.globalState :?> Memento)

        let path =
            state.get<string> ("migrondi-path")
            |> Option.defaultValue ""

        let version =
            state.get<string> ("migrondi-version")
            |> Option.defaultValue ""

        let ext =
            if ``process``.platform = Node.Base.Platform.Win32 then
                ".exe"
            else
                ""

        $"{path}/{version}/Migrondi{ext}"

    let private getCwd (workspaceFolders: (WorkspaceFolder ResizeArray) option) =
        workspaceFolders
        |> Option.map Seq.tryHead
        |> Option.flatten
        |> Option.map (fun ws -> ws.uri.fsPath)

    let private getMigrondiExecFn
        (execPath: ExtensionContext -> string)
        (getCWD: (WorkspaceFolder ResizeArray) option -> string option)
        : (ExtensionContext * string array) -> JS.Promise<MigrondiJsonOutput array> =
        importMember "./interop"


    let runMigrondi: (ExtensionContext * string array) -> JS.Promise<MigrondiJsonOutput array> =
        getMigrondiExecFn execPath getCwd

    let logErrorToChannel (channel: OutputChannel) err =
        channel.show true
        let err = err |> box |> unbox<string>
        channel.appendLine (err)

    let showMsgBoxHandler (output: MigrondiJsonOutput array) =
        let rows =
            output |> Array.map (fun o -> o.fullContent)

        window.showInformationMessage (System.String.Join('\n', rows))
        |> Promise.ofThenable

    let logToChannel (channel: OutputChannel) (clearBeforeRun: bool) (output: MigrondiJsonOutput array) =
        channel.show true
        if clearBeforeRun then channel.clear ()

        seq {
            for row in output do
                yield! row.fullContent.Split(os.EOL)
        }
        |> Seq.iter (channel.appendLine)


[<RequireQualifiedAccess>]
module Init =
    open Helpers

    let Command (context: ExtensionContext) (channel: OutputChannel) _ : obj option =
        runMigrondi (context, [| "--json"; "init" |])
        |> Promise.map showMsgBoxHandler
        |> Promise.catchEnd (logErrorToChannel channel)
        |> box
        |> Some

[<RequireQualifiedAccess>]
module New =
    open Helpers

    let Command (context: ExtensionContext) (channel: OutputChannel) _ : obj option =
        promise {
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
                |> Promise.ofThenable
                |> Promise.map (Option.defaultValue "")

            return! runMigrondi (context, [| "--json"; "new"; "-n"; value |])
        }
        |> Promise.map showMsgBoxHandler
        |> Promise.catchEnd (logErrorToChannel channel)
        |> box
        |> Some

[<RequireQualifiedAccess>]
module Up =
    open Helpers

    let Command (context: ExtensionContext) (channel: OutputChannel) _ : obj option =
        promise {

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
                |> Promise.ofThenable
                |> Promise.map (Option.defaultValue "")

            let! isDry =
                let quickPickOpts =
                    {| canPickMany = false
                       ignoreFocusOut = true
                       placeHolder = "Yes"
                       title = "Is this a dry run?" |}
                    |> box
                    :?> QuickPickOptions

                window.showQuickPick (U2.Case1(ResizeArray([ "Yes"; "No" ])), quickPickOpts)
                |> Promise.ofThenable
                |> Promise.map (Option.defaultValue "Yes")

            let amount =
                try
                    value |> int
                with
                | _ ->
                    channel.appendLine $"Failed to parse {value} defaulting to 0"
                    0

            let total = if amount > 0 then $"{amount}" else "0"

            let dryRun =
                if isDry.ToLowerInvariant() = "yes" then
                    "true"
                else
                    "false"

            return!
                runMigrondi (
                    context,
                    [| "--json"
                       "up"
                       "--dry-run"
                       dryRun
                       "--total"
                       total |]
                )
        }
        |> Promise.map (logToChannel channel true)
        |> Promise.catchEnd (logErrorToChannel channel)
        |> box
        |> Some

[<RequireQualifiedAccess>]
module Down =
    open Helpers

    let Command (context: ExtensionContext) (channel: OutputChannel) _ : obj option =
        promise {

            let inputOpts =
                {| value = "1"
                   title = "Run Migrations Down"
                   prompt = "Amount of migrations to run, leave -1 to run all"
                   placeHolder = "1 (-1 to run all pending)"
                   ignoreFocusOut = true |}
                |> box
                :?> InputBoxOptions

            let! value =
                window.showInputBox (inputOpts)
                |> Promise.ofThenable
                |> Promise.map (Option.defaultValue "")

            let! isDry =
                let quickPickOpts =
                    {| canPickMany = false
                       ignoreFocusOut = true
                       placeHolder = "Yes"
                       title = "Is this a dry run?" |}
                    |> box
                    :?> QuickPickOptions

                window.showQuickPick (U2.Case1(ResizeArray([ "Yes"; "No" ])), quickPickOpts)
                |> Promise.ofThenable
                |> Promise.map (Option.defaultValue "Yes")

            let amount =
                try
                    value |> int
                with
                | _ ->
                    channel.appendLine $"Failed to parse {value} defaulting to 0"
                    0

            let total = if amount > 0 then $"{amount}" else "0"

            let dryRun =
                if isDry.ToLowerInvariant() = "yes" then
                    "true"
                else
                    "false"

            return!
                runMigrondi (
                    context,
                    [| "--json"
                       "down"
                       "--dry-run"
                       dryRun
                       "--total"
                       total |]
                )
        }
        |> Promise.map (logToChannel channel true)
        |> Promise.catchEnd (logErrorToChannel channel)
        |> box
        |> Some

[<RequireQualifiedAccess>]
module List =
    open Helpers

    let Command (context: ExtensionContext) (channel: OutputChannel) _ : obj option =
        promise {
            let! listType =
                let quickPickOpts =
                    {| canPickMany = false
                       ignoreFocusOut = true
                       placeHolder = "pending or present"
                       value = "pending"
                       title = "List Migrations"
                       prompt = "Which migrations to show?" |}
                    |> box
                    :?> QuickPickOptions

                window.showQuickPick (U2.Case1(ResizeArray([ "Pending"; "Present"; "Both" ])), quickPickOpts)
                |> Promise.ofThenable
                |> Promise.map (Option.defaultValue "")

            let kind = listType.ToLowerInvariant()

            if kind <> "pending"
               && kind <> "present"
               && kind <> "both" then
                do!
                    window.showErrorMessage
                        $"""Invalid list type "{kind}", allowed values are 'Pending', 'Present', or 'Both'"""
                    |> Promise.ofThenable
                    |> Promise.map ignore

                let msg: MigrondiJsonOutput array =
                    [| { fullContent =
                             $"""Invalid list type "{kind}", allowed values are 'Pending', 'Present', or 'Both'"""
                         parts = [] } |]

                return msg
            else
                return! runMigrondi (context, [| "--json"; "list"; "--kind"; kind |])
        }
        |> Promise.map (logToChannel channel true)
        |> Promise.catchEnd (logErrorToChannel channel)
        |> box
        |> Some
