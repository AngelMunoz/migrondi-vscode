module Migrondi.VSCode.Types

open Thoth.Json

type Asset =
    { url: string
      id: int
      node_id: string
      name: string
      content_type: string
      state: string
      browser_download_url: string }

    static member Decoder: Decoder<Asset> =
        Decode.object
            (fun o ->
                { url = o.Required.Field "url" Decode.string
                  id = o.Required.Field "id" Decode.int
                  node_id = o.Required.Field "node_id" Decode.string
                  name = o.Required.Field "name" Decode.string
                  content_type = o.Required.Field "content_type" Decode.string
                  state = o.Required.Field "state" Decode.string
                  browser_download_url = o.Required.Field "browser_download_url" Decode.string })

type Release =
    { url: string
      assets_url: string
      id: int
      tag_name: string
      name: string
      draft: bool
      prerelease: bool
      assets: Asset list }

    static member Decoder =
        Decode.object
            (fun o ->
                { url = o.Required.Field "url" Decode.string
                  assets_url = o.Required.Field "assets_url" Decode.string
                  id = o.Required.Field "id" Decode.int
                  tag_name = o.Required.Field "tag_name" Decode.string
                  name = o.Required.Field "name" Decode.string
                  draft = o.Required.Field "draft" Decode.bool
                  prerelease = o.Required.Field "prerelease" Decode.bool
                  assets = o.Required.Field "assets" (Decode.list Asset.Decoder) })


type ConsoleOutput =
    | Normal of string
    | Warning of string
    | Danger of string
    | Success of string

    static member Decoder =
        Decode.object
            (fun o ->
                let case = o.Required.Field "Case" Decode.string

                let fields =
                    o.Required.Field "Fields" (Decode.list Decode.string)

                match case with
                | "Normal" -> Normal fields.Head
                | "Warning" -> Warning fields.Head
                | "Danger" -> Danger fields.Head
                | "Success" -> Success fields.Head
                | _ -> failwith "Unknown case")

type MigrondiJsonOutput =
    { fullContent: string
      parts: ConsoleOutput list }

    static member Decoder =
        Decode.object
            (fun o ->
                { fullContent = o.Required.Field "fullContent" Decode.string
                  parts = o.Required.Field "parts" (Decode.list ConsoleOutput.Decoder) })


[<AutoOpen>]
module Utils =
    open Fable.Core
    open Fable.Import.VSCode

    type Foo = JS.PromiseConstructor

    type Promise.PromiseBuilder with
        member x.Source(p: Thenable<'t>) : JS.Promise<'t> = box p |> unbox
        member x.Source(p: JS.Promise<'t>) : JS.Promise<'t> = p

module Promise =

    open Fable.Core
    open Fable.Import.VSCode

    let ofThenable (t: Thenable<'t>) : JS.Promise<'t> = unbox (box t)
    let toThenable (p: JS.Promise<'t>) : Thenable<'t> = unbox (box p)
