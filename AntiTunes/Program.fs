// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.
open System
open System.Web
open System.IO
open System.Net
open HtmlAgilityPack

[<EntryPoint>]
let main argv = 
    let iTunesUA = "iTunes/10.1 (Windows; U; Microsoft Windows XP Home Edition Service Pack 2 (Build 2600)) DPI/96"
    let endPoints = [
        "http://itunes.apple.com/podcast/id";
        "http://itunes.apple.com/WebObjects/DZR.woa/wa/viewPodcast?id="
    ]

    let parseQueryString (url:Uri):Map<string, string> =
        let q = HttpUtility.ParseQueryString(url.Query)
        q.AllKeys
        |> Seq.map (fun k -> k, q.[k])
        |> Map.ofSeq
    
    let parseHtml str =
        let dom = new HtmlAgilityPack.HtmlDocument()
        dom.LoadHtml str
        dom

    let tryFirst seq =
        if (Seq.isEmpty seq) then
            None
        else 
            Some (Seq.head seq)

    let getPodcastId url =
        let _url = new Uri(url)
        let query = parseQueryString _url

        if query.ContainsKey "id" then
            Some <| query.["id"]
        else
            (_url.LocalPath.Split [| '/' |])
            |> Seq.tryFind (fun s -> s.Length > 2 && s.Substring(0, 2) = "id")
            |> Option.map (fun s -> s.Substring(2))

    let getFeedUrlFromEndPoint id (endpoint:string) =
        async {
            try
                let url = endpoint + id
                let req : HttpWebRequest = WebRequest.Create(url) :?> HttpWebRequest
                req.UserAgent <- iTunesUA
                req.AllowAutoRedirect <- true
                req.MaximumAutomaticRedirections <- 99

                let! resp = Async.AwaitTask (req.GetResponseAsync())
                let _resp = resp :?> HttpWebResponse

                let sr = new StreamReader (_resp.GetResponseStream())
                let! html = Async.AwaitTask (sr.ReadToEndAsync())
                let dom = parseHtml html
                let buttons = dom.DocumentNode.SelectNodes("//button[@feed-url]")
                let _buttons = if buttons = null then Seq.empty else buttons :> seq<HtmlNode>

                let result = (_buttons
                    |> Seq.choose (fun n -> 
                        let value = n.GetAttributeValue("feed-url", "")
                        if String.IsNullOrEmpty value then
                            None
                        else
                            Some value))

                return tryFirst result
            with
            | exn -> return None
        }

    let getFeedUrl id =
        async {
            let! results = Async.Parallel (Seq.map (fun e -> getFeedUrlFromEndPoint id e) endPoints)
            let success = Seq.choose (fun x -> x) results
            return tryFirst success
        }
        
    let convertItunesUrl url = 
        async {
          match (getPodcastId url) with
          | None    -> return None
          | Some id -> return! (getFeedUrl id)
        }

    let run url = 
        async {
            let! result = convertItunesUrl url
            match result with
            | None   -> printf "failed"
            | Some x -> printf "succeeded: %A" x
        }
        
    "https://itunes.apple.com/nz/podcast/the-issues-podcast/id664643600"
    |> run
    |> Async.RunSynchronously
    |> ignore
    

    0 // return an integer exit code
