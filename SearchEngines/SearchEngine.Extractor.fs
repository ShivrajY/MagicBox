module SearchEngine.Extractor
open HOF

open System.Text.RegularExpressions

let private regex string =
    let ex = "(http|ftp|https)://([\w_-]+(?:(?:\.[\w_-]+)+))([\w.,@?^=%&:/~+#-]*[\w@?^=%&/~+#-])?"
    let re:MatchCollection = Regex.Matches(string, ex)
    seq{for m in re -> m.ToString()}
    |>Set.ofSeq

let regexExtractor text = method regex text

