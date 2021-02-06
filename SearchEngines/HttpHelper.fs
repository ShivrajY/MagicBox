module HttpHelper
open HttpWebRequestInfo
open System
open System.Net

let private combineUrl (baseUrl:string) (query:string) = $"{baseUrl.TrimEnd('/')}/{query}"
           
let CreateHttpRequest (info:HttpWebRequestInfo):HttpWebRequest =
    let method = info.Method
    let req = 
     match method with 
      |Get (baseUrl, query)->
       let url = combineUrl baseUrl query
       let r = WebRequest.Create(url) :?> HttpWebRequest
       r.Method <- "GET"
       r
      |Post (url, postData)->
       let r = WebRequest.Create url :?> HttpWebRequest
       r.Method <- "POST"
       let len = postData.Length
       let s = r.GetRequestStream()
       s.Write(postData,0,len);
       r
    req.Accept <- info.Accept
    req.UserAgent<-info.UserAgent
    match info.Headers with
    |Some h->(req.Headers<-h)
    |None->()
    match info.CookieContainer with
    |Some c -> req.CookieContainer<-c
    |None->()
    match info.Proxy with
    |Some p->req.Proxy <- p
    |None->()
    match info.TimeoutMilliSec with
    |Some t-> req.Timeout<-t
    |None->()       

    req
