namespace Magicbox.SearchEngines

open FSharp.Data

module Aol =
    let Search (url: string) =
        let doc = HtmlDocument.Load url

        doc.CssSelect("h3 > a")
        |> List.choose (fun x ->
            x.TryGetAttribute("href")
            |> Option.map (fun a -> x.InnerText(), a.Value()))
        |> Yahoo.filterHrefs
        |> List.map (fun (name, url) -> name, (Yahoo.sanitizeUrl url))
        |> List.toSeq
