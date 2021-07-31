[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Extension

open System
open Fable.Core
open Fable.Core.JsInterop
open Fable.Import.vscode
open Ionide.VSCode.Helpers
open global.Node.ChildProcess

let activate (context: ExtensionContext) : unit =
    printfn "Activating Fable extension"

    let showMessage () =
        Fable.Import.vscode.window.showInformationMessage ("Hello World!")

    let disposable =
        Fable.Import.vscode.commands.registerCommand (
            "fsharp-fable-sample.helloWorld",
            (fun _ -> showMessage () :> obj)
        )

    context.subscriptions.Add disposable

let deactivate (disposables: Disposable []) =
    printfn "Deactivating Fable extension"

    disposables
    |> Array.iter (fun d -> d.dispose () |> ignore)
