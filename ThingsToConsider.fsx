open System.Collections.Generic
open System.Collections
open System

#I @"C:\Users\supre\.nuget\packages\"
#r @"fspickler\5.3.2\lib\netstandard2.0\FsPickler.dll"
#r @"fspickler.json\5.3.2\lib\netstandard2.0\FsPickler.Json.dll"
#r @"fsharp.data\3.3.3\lib\netstandard2.0\FSharp.Data.DesignTime.dll"

#load "Common\Common.Network.fs"
#load "Common\Common.Types.fs"
#load "Search\Search.Common.fs"

open System.Net
open FSharp.Data
open MBrace.FsPickler.Json
open MBrace.FsPickler
open System.IO
open Search.Common


//let add a b = a+b
//let s = FsPickler.CreateJsonSerializer(indent = true)
//let ms = new MemoryStream()
//let tw = new StreamWriter(ms) :> TextWriter
//let test = s.Serialize(tw,add,leaveOpen=true)
//ms.Position<-0L
//let tr = new StreamReader(ms) :> TextReader
//let text = tr.ReadToEnd()
//ms.Position <- 0L

//let add' = s.Deserialize<int->int->int>(tr) 
//let x = add' 1 3
//______________________________________________________________________________.
(*Serialize using pickler*)
//let s = FsPickler.CreateJsonSerializer(indent = true)
//let x = s.PickleToString add
//let y = s.UnPickleOfString<int->int->int> x
//let z = y 1 2
////..........................................................................
//let xx = FSharp.Data.Http.Request("http://www.google.com/search?q=test");

//let reqStr = s.PickleToString xx
//_______________________________________________________________________________
(* Serializing SearchEngine record...*)
//let se:Website = 
//    {
//      Name="Google"
//      SearchUrl = "http://www.google.com/search"
//      SearchParam="q"
//      NumParam = "num"
//      QueryValues= ["q","keyword";"num","0"]|>dict|>Dictionary<string,string>
//    }  
      
//let s = FsPickler.CreateJsonSerializer(indent = true)
//let x = s.PickleTyped se
//let y = s.UnPickleTyped x
//---------------------------------------------------------------------------------
(* Search.Common - Website Record *)
//let qv = Dictionary<string,string>()
//qv.Add("q", "keyword")
//qv.Add("num","0")

//let w = 
//    {
//      Name="Google"
//      SearchUrl = "http://www.google.com/search"
//      QueryValues = qv
//      SearchParam = "q"
//      NumParam = "num"      
//    }

//printfn "%s" (w.GetUrl())

//---------------------------------------------------------------





