module SearchParams

let googleSearchParams =
    [
        "q","" //the search query string
        "oq",""  // Shows the original query.
        "num","" //the number of results per page
        "numgm",""//Number of KeyMatch results to return with the results. A value between 0 to 50 can be specified for this option.
        "start","0"//Specifies the index number of the first entry in the result set that is to be returned.
        "rc","" //Request an accurate result count for up to 1M documents.
        "site","" //imits search results to the contents of the specified collection. If a user submits a search query without the site parameter, the entire search index is queried.
        "sort","" //Specifies a sorting method. Results can be sorted by date.
        "client","firefox-a" (* Required parameter. If this parameter does not have a valid value, other parameters in the query string do not work as expected. Set to 'firefox-a' in mozilla firefox A string that indicates a valid front end and the policies defined for it, including KeyMatches, related queries, filters, remove URLs, and OneBox Modules. Notice that the rendering of the front end is determined by the proxystylesheet parameter. Example: client=myfrontend *)
        "cd",""
        "filter","" //Include omitted results if set to 0
        "complete","" //Turn auto-suggest and Google Instant on (=1) or off (=0)
        "nfpr","" //Turn off auto-correction of spelling on=1, off=0
        "ncr","" //No country redirect: Allows you to set the Google country engine you would like to use despite your current geographic location.
        "safe","off" //Turns the adult content filter on or off
        "sa",""//User search behavior parameter sa=N: User searched, sa=X: User clicked on related searches in the SERP
        "source",""//Google navigational parameter specifying where you came from, univ: universal search
        "sourceid","" //When searching with chrome, is set to 'chrome'
        "tlen","" // Specifies the number of bytes that would be used to return the search results title. If titles contain characters that need more bytes per character, for example in utf-8, this parameter can be used to specify a higher number of bytes to get more characters for titles in the search results.
        "tbm",""// Used when you select any of the “special” searches, like image search or video search
        "tbs",""//Also undocumented as `tbm`, allows you to specialize the time frame of the results you want to obtain.
                // Examples: Any time: tbs=qdr:a, Last second: tbs=qdr:s, Last minute: tbs=qdr:n, Last day: tbs=qdr:d,
                //Time range: tbs=cdr:1,cd_min:3/2/1984,cd_max:6/5/1987
                //But the tbs parameter is also used to specify content:
                //Examples: Sites with images: tbs=img:1, Results by reading level, Basic level: tbs=rl:1,rls:0,
                //Results that are translated from another language: tbs=clir:1,
        "hl","" //Language settings passed down by your browser               
        "cr",""//The region the results should come from
        "gr",""//Just as gl shows you how results look in a specified country, gr limits the results to a certain region
        "gcs",""//Limits results to a certain city, you can also use latitude and longitude
        "gpc",""//Limits results to a certain zip code
        "gm",""//Limits results to a certain metropolitan region
        "ie","UTF-8"//Sets the character encoding that is used to interpret the query string.
        "oe","UTF-8"//Sets the character encoding that is used to encode the results.
        "access","a"//Specifies whether to search public content (p), secure content (s), or both (a).
        "biw",""//Browser inner width in pixel
        "bih",""//Browser inner height in pixel
        "as_filetype",""//Specifies a file format to include or exclude in the search results. Modified by the as_ft parameter.
        "as_ft",""//Modifies the as_filetype parameter to specify filetype inclusion and exclusion options. The values for as  ft are: 'i': filetype and 'e': -filetype
        "as_lq",""//Specifies a URL, and causes search results to show pages that link to the that URL. This parameter has the same effect as the link special query term be used when using this parameter.
        "as_occt",""//Specifies where the search engine is to look for the query terms on the page: anywhere on the page, in the title, or in the URL.
        "as_oq","" //Combines the specified terms to the search query in parameter q, with an OR operation. This parameter has the same effect as the OR special query term 
        "as_q",""//Adds the specified query terms to the query terms in parameter q.
    ]
