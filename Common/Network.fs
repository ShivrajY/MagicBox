module Common.Network
open System
open System.Net
open FSharp.Data

let getProxyFromString (proxyString:string) (seperator:char) =
    let arr = proxyString.Split([|seperator|], StringSplitOptions.RemoveEmptyEntries)
    match arr with
    |[|host; port|]->
               match Int32.TryParse(port) with
                  |(true,p)->WebProxy(host, p)|>Some
                  |(false,_)->None 
    |[|host; port; name; pass|]-> 
            match Int32.TryParse(port) with
            |(true,p)->let prox = WebProxy(host, p)
                       prox.Credentials <- NetworkCredential(name, pass)
                       Some prox
            |(false,_)->None   
    |_->None  


