namespace Magicbox.SearchEngines

open FSharp.Data
open System.Text

module Yahoo =
    let subStringBetween (source: string) (token1: string) (token2: string) =
        let pos1 = source.IndexOf(token1) + token1.Length
        let pos2 = source.IndexOf(token2)

        if (pos1 > 0 && pos2 > 0 && pos2 > pos1) then
            source.Substring(pos1, pos2 - pos1)
        else
            ""

    let sanitizeUrl (url: string) =
        let replaceStrings =
            [ "/RU=", ""
              "/RK", ""
              "%3a", ":"
              "%2f", "/"
              "%3f", "?" ]

        let url' = subStringBetween url "/RU=" "/RK"
        let sb = new StringBuilder(url')

        replaceStrings
        |> List.fold (fun (state: StringBuilder) (x, y) -> state.Replace(x, y)) sb
        |> fun x -> x.ToString()

    let filterHrefs (urls: (string * string) list) =
        urls
        |> List.filter (fun (name, url) ->
            let index1 = url.IndexOf("/RU=")
            let index2 = url.IndexOf("/RK")
            (index1 > 0 && index2 > 0 && index2 > index1))

    let Search (url: string) =
        let doc = HtmlDocument.Load url

        let searchResults =
            doc.Descendants [ "a" ]
            |> Seq.choose (fun x ->
                x.TryGetAttribute("href")
                |> Option.map (fun a -> x.InnerText(), a.Value()))
            |> Seq.toList
            |> filterHrefs
            |> List.map (fun (name, url) ->
                let url' = sanitizeUrl url
                name, url')

        searchResults |> List.toSeq
