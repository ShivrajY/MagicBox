module Search.Common
open Common.Network
open System.Net
open FSharp.Data
open System.IO
open System.Collections.Generic
open System

type DownloadData =
    |Text of string
    |Bytes of byte array

type DownloadMethod =
    |Http of  httpMethod:HttpMethod * headers:Dictionary<string,string> * cookies: CookieContainer * timeout:int 
    |Selenium     

type WebsiteCredentials=
    {
        UserName:string
        Password:string
    }

type WebsiteCssSelectors =
    {
       NameCssSelector:string
       PassCssSelector:string
       SubmitButton:string
       LinkParentCssSelector:string
       LinkCssSelector:string
       TextCssSelector:string
       Filter:string->bool
       Transform:string->string
    }

type Website =
  { 
    Name: string //name of search engine
    SearchUrl: string //eg. https://www.google.com/search
    QueryValues: Dictionary<string,string>  //eg. ["q","keyword";"num",0]
    SearchParam: string  // "q"
    NumParam: string// "num"
  }             
     
        
let getSearchUrl (t:Website) =
    let query = String.Join("&", (t.QueryValues |> Seq.map(fun qvp-> $"{qvp.Key}={WebUtility.UrlEncode(qvp.Value)}")))
    $"{t.SearchUrl}?{query}"
    
(*
   Basic Search
   
   keyword, pages, website, WebsiteCssSelectors, 
   websiteCredentials option, proxy option 
   DownloadMethod 

   -> DownloadData
*)
