module SearchEngine
open System
type SearchEngineInfo =
    {
      Name:string
      Favicon:string //base64 string
      Country:string
      TLD:string
      Type:string
    }
type SearchEngine =
    {
      Name:string
      BaseUrl:string
      QueryFormat:string
      QueryParams:obj array option
      Info:SearchEngineInfo
    }
    member this.getQueryString args = String.Format(this.QueryFormat, args)
