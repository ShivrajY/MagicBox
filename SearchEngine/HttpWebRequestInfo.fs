module HttpWebRequestInfo
open System.Net
open System

type HttpMethod =
    |Get of url:string * queryString:string
    |Post of url:string * postData:byte array

type HttpWebRequestInfo =
    {
      Method:HttpMethod
      Accept:string
      Headers: WebHeaderCollection option
      CookieContainer: CookieContainer option
      TimeoutMilliSec:int option
      UserAgent:string
      Proxy:WebProxy
    }
