module SearchEngines.Common
open Common.Network

type SearchEngine =
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

type ScrapeMethod = 
    |HttpRequest
    |Webbrowser
    |Selenium

type SearchData =
    {
      Query:string
      ScrapeMethod:ScrapeMethod option
      Pages:int option
    }
