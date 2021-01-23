module Common
open System
open System.Text.RegularExpressions
open FSharp.Collections
open System.Collections.Generic

let castToObjArray x = x |> Array.map (fun y -> box y)

let getFormattedString format (paramArray:obj[])= 
    match (Regex.Matches(format,"\{\d+\}").Count) with
    |curly when curly<= paramArray.Length ->Some (String.Format(format, paramArray))
    |_->None 

let RoundRobin (list:ResizeArray<'a>):'a option =
   if list.Count=0 then None
   else
     let item = list.Item 0
     list.RemoveAt 0
     Some item

let random = new Random()
    
let Random (list:ResizeArray<'a>):'a option=
    match list.Count with
    |0->None
    |_->   
        let index = random.Next(list.Count)
        let item = list.Item index
        list.RemoveAt index
        Some item
