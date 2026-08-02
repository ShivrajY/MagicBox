namespace Magicbox.SearchEngines

open System
open FSharp.Data

module Google =

    let private tryQueryParameter (parameterName: string) (query: string) =
        query.Split([| '&' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.tryPick (fun parameter ->
            let separatorIndex = parameter.IndexOf('=')

            if separatorIndex <= 0 then
                None
            elif parameter.Substring(0, separatorIndex) <> parameterName then
                None
            else
                let value = parameter.Substring(separatorIndex + 1).Replace("+", " ")

                try
                    Some(Uri.UnescapeDataString(value))
                with :? UriFormatException ->
                    None)

    let private tryResultUrl (href: string) =
        if href.StartsWith("/url?", StringComparison.Ordinal) then
            href.Substring("/url?".Length)
            |> tryQueryParameter "q"
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
