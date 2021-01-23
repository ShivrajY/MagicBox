module SearchEngine.Downloader

open HOF.Common
open System.Net

let webClient(url:string) =
    use wc = new WebClient()    
    wc.DownloadString(url)

let webClientDownloader url = applyPartial webClient url



