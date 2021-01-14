module SearchDomain
open System
open System.Net
open System.Net.Http

type RotationAlgorithm = 
    |RoundRobin 
    |Random 
    |PriorityQueue

type ScrapingProxies = 
    {
      Proxies: WebProxy list 
      ProxyRotationAlgo: RotationAlgorithm     
    }

type SearchResult =
    {
      LinkText:string
      LinkUrl:string
    }

type DataString = 
    |Html of string
    |Json of string

type DataToSend =
    |QueryString of string
    |PostData of byte array
    
type HttpInfo =
    {
      BaseUrl:string
      Cookies:CookieContainer
      HttpHeaders: WebHeaderCollection
      Method:HttpMethod
      Data: DataToSend
      Proxy:WebProxy option
     }

type ChromeInfo =
    {
      Headless:bool
      ChromeDir:string
      ProfileDir:string   
    }

type DownloadInfo =
    |HttpInfo of HttpInfo
    |ChromeInfo of ChromeInfo
    
type ScrapingInfo =
    {
      Downloader:DownloadInfo->DataString
      Extractor:DataString->SearchResult list
    }
type SearchEngine =
    {
      Name:string
      Country:string
      TopLevelDomain:string
      BaseUrl:string
      Proxies:ScrapingProxies option
    }
    


