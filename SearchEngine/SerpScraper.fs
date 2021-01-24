module SerpScraper

open System
open System.Collections.Generic
open FSharp.Data
open HttpWebRequestInfo
open System.Text.Json
open System.Text

type SearchResult = Dictionary<string*Type,obj>

type Downloader =
    |HttpWebRequest of HttpWebRequestInfo
    |FSharpData
    |Chrome
    |HtmlAgilityPack

type SearchCriteria =
    |RegularExpression of pattern:string
    |CSS of string
    |XPath of string

type WebData =
    |Html of string 
    |JSON of JsonDocument
    |HtmlDoc of FSharp.Data.HtmlDocument
    |Binary of byte array
    |ChromeDriver 
    
type Extracts = Extract of WebData * SearchCriteria

type Serializes =
    |SaveFile of filePath:string
    |JsonObject of JsonDocument
    
type Setting =
    { 
       Link:Uri      
       Downloader : Downloader -> WebData
       Extractor : Extracts -> Result<SearchResult,string>
       Serializer : Serializes -> Result<string,string>
    }

let Scrape setting =
   ()
