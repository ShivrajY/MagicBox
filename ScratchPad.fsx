#r @"C:\Users\supre\.nuget\packages\fsharp.data\3.3.3\lib\netstandard2.0\FSharp.Data.dll"
open System
open System.IO
open System.Net
open FSharp.Data
open FSharp.Data.HttpRequestHeaders
#load "Common.fs"
#load "HOF.fs"
open Common
open HOF

let format = "this is test {0}{1}{2}"

let arr = [|1;2;3;4|] |> castToObjArray

let str = getFormattedString format arr