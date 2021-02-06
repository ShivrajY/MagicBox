module Google
open SearchEngine
open SerpScraper
open HttpWebRequestInfo
open System
open FSharp.Data

let google (keyword:string) =
    let url = String.Format("http://www.google.com/search?q={0}",Uri.EscapeUriString(keyword))
    let results = FSharp.Data.HtmlDocument.Load(url)
    let links = 
        results.Descendants ["a"]
        |> Seq.choose (fun x -> 
               x.TryGetAttribute("href")
               |> Option.map (fun a -> x.InnerText(), a.Value())
        )
    let searchResults =
        links
        |> Seq.filter (fun (name, url) -> 
                        name <> "Cached" && name <> "Similar" && url.StartsWith("/url?"))
        |> Seq.map (fun (name, url) -> name, url.Substring(0, url.IndexOf("&sa=")).Replace("/url?q=", ""))
        |> Seq.toArray
    searchResults