module internal SearchEngine
type SearchEngineInfo =
    {
      Name:string
      Favicon:byte array
      Country:string
      TLD:string
      Type:string
    }
type SearchEngine =
    {
      Name:string
      BaseUrl:string
      QueryFormat:string
      QueryParams:obj array
      Info:SearchEngineInfo
    }

