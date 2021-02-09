module internal SearchEngines.Common
open Common.Network
open System.Net
open FSharp.Data

type ScrapeMethod = 
    |HttpRequest of proxy:WebProxy option * cookies: CookieContainer option
    |Selenium 

type SearchData =
    {
      Query:string
      ScrapeMethod:ScrapeMethod option
      Pages:int option
    }

type SearchEngines =
    |Google
    |Yahoo
    |Bing
    |Yandex
    |DuckDuckGo
    |Ask
    |AOL
    |Rediff
    |IXQuick
    |YouTube
    |SimilarSite
    |Biglobe
    |Goo
    |Orange
   

