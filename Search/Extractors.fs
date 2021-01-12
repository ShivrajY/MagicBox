module Extractors

open System.Text.RegularExpressions

let private extractor method text = method text

let private regex string =
    let ex = "(http|ftp|https)://([\w_-]+(?:(?:\.[\w_-]+)+))([\w.,@?^=%&:/~+#-]*[\w@?^=%&/~+#-])?"
    let re:MatchCollection = Regex.Matches(string, ex)
    seq{for m in re -> m.ToString()}
    |>Set.ofSeq

let regexExtractor text = extractor regex text

