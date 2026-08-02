namespace Magicbox.SearchEngines

open System
open FSharp.Data

module DuckDuckGo =
    let private tryResultUrl (href: string) =
        let normalizedHref = href.Replace("&amp;", "&")

        if normalizedHref.StartsWith("//duckduckgo.com/l/?", StringComparison.Ordinal) then
            normalizedHref.Substring("//duckduckgo.com/l/?".Length)
            |> Url.tryQueryParameter "uddg"
        elif normalizedHref.StartsWith("https://duckduckgo.com/l/?", StringComparison.Ordinal) then
            normalizedHref.Substring("https://duckduckgo.com/l/?".Length)
            |> Url.tryQueryParameter "uddg"
        else
            Some normalizedHref

    let Search (url: string) =
        let doc = HtmlDocument.Load url

        doc.CssSelect("a.result__a")
        |> List.choose (fun x ->
            x.TryGetAttribute("href")
            |> Option.bind (fun a ->
                tryResultUrl (a.Value())
                |> Option.map (fun resultUrl -> x.InnerText(), resultUrl)))
        |> List.toSeq
