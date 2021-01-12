module Downloaders

open System.Net

let webClient(url:string) =
    use wc = new WebClient()
    wc.DownloadString(url)


let downloadHtml method url = method url
let webClientDownload url = downloadHtml webClient url
let googleSearchUrl keyword = $"https://www.google.com/search?q={keyword}"



