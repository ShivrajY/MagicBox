module Common
open System
open System.Text.RegularExpressions

let castToObjArray x = x |> Array.map (fun y -> box y)
///Get formatted string after supplying format and parameters
let getFormattedString format (paramArray:obj[])= 
    match (Regex.Matches(format,"\{\d+\}").Count) with
    |curly when curly<= paramArray.Length ->Some (String.Format(format, paramArray))
    |_->None 
   