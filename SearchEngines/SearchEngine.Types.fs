module SearchEngine.Types
open System
open System.Net
open System.Net.Http
open Common
open System.Collections.Generic

type SearchEngine =
   {
      Name:string
      Country:string
      BaseUrl:string
      QueryStringFormat:string     
      QueryArguments:string array
   }

type Proxy =
    {
      Host:string
      Port:int
      Credential:NetworkCredential option
      BypassLocal:bool
    }
    member this.WebProxy() =
        let p = new WebProxy(this.Host, this.Port)
        p.BypassProxyOnLocal <- this.BypassLocal
        match this.Credential with
        |Some c -> p.Credentials<-c
        |None -> p.Credentials<-CredentialCache.DefaultCredentials
        p


type SerpScraperSetting =
    {
     SearchEngine:SearchEngine
     Proxies:Proxy ResizeArray
     HttpHeaders:(string*string) list

    
    }

