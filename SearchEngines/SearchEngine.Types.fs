module SearchEngine.Types
open System
open System.Net
open System.Net.Http
open Common

type SearchEngine =
   {
      Name:string
      Country:string
      BaseUrl:string
      QueryStringFormat:string     
      QueryArguments:string array
   }
    // "{%s}/{%s}" (this.BaseUrl) () 


