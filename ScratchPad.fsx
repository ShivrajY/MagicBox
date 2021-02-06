#r @"C:\Users\supre\.nuget\packages\fsharp.data\3.3.3\lib\netstandard2.0\FSharp.Data.dll"
open System
open FSharp.Data


//let google (urlStringFormat:string) (keyword:string) =
//    let escaped = System.Net.WebUtility.UrlEncode(keyword)
//    let url = String.Format(urlStringFormat,escaped)
//    let results = HtmlDocument.Load(url)
//    let links = 
//        results.Descendants ["a"]
//        |> Seq.choose (fun x -> 
//               x.TryGetAttribute("href")
//               |> Option.map (fun a -> x.InnerText(), a.Value())
//        )
//    let searchResults =
//        links
//        |> Seq.filter (fun (name, url) -> 
//                        name <> "Cached" && name <> "Similar" && url.StartsWith("/url?"))
//        |> Seq.map (fun (name, url) -> name, url.Substring(0, url.IndexOf("&sa=")).Replace("/url?q=", ""))
//        |> Seq.toArray
//    searchResults

