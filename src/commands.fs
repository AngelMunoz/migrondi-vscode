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

                match stdout with
                | U2.Case1 (stdout) ->
                    let! _ = window.showInformationMessage stdout
                    ()
                | U2.Case2 (stdout) ->
                    let! _ = window.showInformationMessage (stdout.toString (Utf8))
                    ()

                match stderr with
                | U2.Case1 (stderr) ->
                    let! _ = window.showInformationMessage stderr
                    ()
                | U2.Case2 (stderr) ->
                    let! _ = window.showInformationMessage (stderr.toString (Utf8))
                    ()
            | Some error ->
                let! _ = window.showErrorMessage $"Failed to run migrondi: {error.message}"
                ()
        }
        |> ignore

    let Command (context: ExtensionContext) =
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
