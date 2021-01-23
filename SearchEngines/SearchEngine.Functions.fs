module SearchEngine.Scraper

open SearchEngine.Types
open Common

let SearchUrl searchEngine = 
    searchEngine.QueryArguments|>castToObjArray
    |>getFormattedString searchEngine.QueryStringFormat 

