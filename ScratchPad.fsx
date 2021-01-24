open System.Net
open System
open FSharp.Data
open System.Collections.Generic

#r @"C:\Users\supre\.nuget\packages\fsharp.data\3.3.3\lib\netstandard2.0\FSharp.Data.dll"

let req = WebRequest.Create "https://www.google.com/" :?> HttpWebRequest

req.Accept<-"*/*"
req.CookieContainer<-new CookieContainer()
req.Headers<-WebHeaderCollection()
req.Headers.Add("accept-encoding: gzip, deflate, br")

req.Timeout<-TimeSpan.FromSeconds(10.).TotalMilliseconds |> int
req.Method<-"GET"

let resp = req.GetResponse();
