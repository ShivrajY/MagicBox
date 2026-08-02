namespace Magicbox.SearchEngines

open FSharp.Data

module Brave =
    let Search (url: string) =
        let doc = HtmlDocument.Load url

        doc.CssSelect(".result-wrapper .result-content > a")
        |> List.choose (fun x ->
            let title =
                x.CssSelect("div.title")
                |> List.tryHead
                |> Option.map (fun node -> node.InnerText())

            x.TryGetAttribute("href")
            |> Option.bind (fun a -> title |> Option.map (fun resultTitle -> resultTitle, a.Value())))
        |> List.toSeq
