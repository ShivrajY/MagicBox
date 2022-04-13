namespace Magicbox.SearchEngines

open FSharp.Data

module Bing =
    let Search (url: string) =
        let doc = HtmlDocument.Load url

        doc.CssSelect("h2 > a")
        |> List.choose (fun x ->
            x.TryGetAttribute("href")
            |> Option.map (fun a -> x.InnerText(), a.Value()))
        |> List.toSeq
