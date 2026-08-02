namespace Magicbox.SearchEngines

open System
open FSharp.Data

module Google =

    let private tryResultUrl (href: string) =
        if href.StartsWith("/url?", StringComparison.Ordinal) then
            href.Substring("/url?".Length)
            |> Url.tryQueryParameter "q"
        else
            None

    let Search (url: string) =
        let doc = HtmlDocument.Load url

        let searchResults =
            doc.Descendants [ "a" ]
            |> Seq.choose (fun x ->
                x.TryGetAttribute("href")
                |> Option.map (fun a -> x.InnerText(), a.Value()))
            |> Seq.toList
            |> List.choose (fun (name, href) ->
                if name = "Cached" || name = "Similar" then
                    None
                else
                    tryResultUrl href |> Option.map (fun resultUrl -> name, resultUrl))

        searchResults |> List.toSeq
