namespace Magicbox.SearchEngines

open System

module internal Url =
    let tryQueryParameter (parameterName: string) (query: string) =
        query.Replace("&amp;", "&").Split([| '&' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.tryPick (fun parameter ->
            let separatorIndex = parameter.IndexOf('=')

            if separatorIndex <= 0 then
                None
            elif parameter.Substring(0, separatorIndex) <> parameterName then
                None
            else
                let value = parameter.Substring(separatorIndex + 1).Replace("+", " ")

                try
                    Some(Uri.UnescapeDataString(value))
                with :? UriFormatException ->
                    None)
