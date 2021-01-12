module SearchDomain
open Downloaders
open Extractors

let google = googleSearchUrl>>webClientDownload>>regexExtractor


type test()=
 member this.Show() =
   printfn "In Show"
   (google "test")   
   |>Set.iter (fun x-> printfn "%s" x)
   ()
