namespace Magicbox.SearchEngines

open FSharp.Data

module Google =

    let Search (url: string) =
        let doc = HtmlDocument.Load url

        let searchResults =
            doc.Descendants [ "a" ]
            |> Seq.choose (fun x ->
                x.TryGetAttribute("href")
                |> Option.map (fun a -> x.InnerText(), a.Value()))
            |> Seq.toList
            |> List.filter (fun (name, url) ->
                name <> "Cached"
                && name <> "Similar"
                && url.StartsWith("/url?"))
            |> List.map (fun (name, url) ->
                name,
                url
                    .Substring(0, url.IndexOf("&sa="))
                    .Replace("/url?q=", ""))

        searchResults |> List.toSeq
