#r @"C:\Users\supre\.nuget\packages\fsharp.data\3.3.3\lib\netstandard2.0\FSharp.Data.dll"
open System
open FSharp.Data

//2/9/2021
(*

type SearchResult = string * string * string //title, url, description
    
type SearchEngine =
  {
    Query:string
    SearchUrl:string
    Fetch:string->string->SearchResult list //url, 
  }

let escapedQuery query = System.Net.WebUtility.UrlEncode(query)

let googleSearchLink query =
    query, $"https://www.google.com/search?hl=en&access=a&safe=off&num=100&filter=0&client=firefox-a"

let scraper query = 
    fun url -> 
    let q, searchLink = googleSearchLink query

    let headers = [         
        "Accept-Language", "en-US,en;q=0.5"
        "Accept-Encoding", "gzip, deflate, sdch"
        "Connection", "keep-alive" ]

    async{
      let! response = Http.AsyncRequestStream(searchLink,headers = headers, httpMethod="GET", silentHttpErrors = true,
                                         customizeHttpRequest = 
                                          fun req -> req.Accept <- "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8" 
                                                     req)
       
      use stream = response.ResponseStream
      let doc = HtmlDocument.Load(stream)
      

      ()
    }|> Async.Start


*)

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

