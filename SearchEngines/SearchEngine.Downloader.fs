module SearchEngine.Downloader

open HOF
open System.Net

let webClient(url:string) =
    use wc = new WebClient()    
    wc.DownloadString(url)

let webClientDownload url = method webClient url
let googleSearchUrl keyword = $"https://www.google.com/search?q={keyword}"



