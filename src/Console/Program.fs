﻿open Crawler.Crawler
open Crawler.DownloadActor
open Crawler.ParseActor
open Crawler.CrawlerTypes
open Akka.FSharp
open System

(*
    1. Статистика для сайта (кол-во страниц, изображений, байт).
    2. Как понять, что исследование сайта закончилось.
    3. прогресс бар?
    4. не посещать уже посещенные страницы
    5. http client для сайта
    6. асинхронный запрос
    7. распараллеливание (очередь акторов загрузки и парсинга)
*)


let getSiteAddress args = if args = null || (Array.length args) = 0 then "https://www.eurosport.ru/" else args.[0]

let printColorMessage (msg: string) color =
    Console.ForegroundColor <- color
    Console.WriteLine msg
    Console.ResetColor()

let printDocResult result =
    let { DocumentUri = uri; Size = size } = result
    let msg = String.Format("Document is downloaded. URI: {0} Size {1}", uri, size)
    printColorMessage msg ConsoleColor.Blue

let printImgResult result =
    let { ImageUri = uri; Size = size } = result
    let msg = String.Format("Image is downloaded. URI: {0} Size {1}", uri, size)
    printColorMessage msg ConsoleColor.Green

let printErrorResult result =
    let { RootUri = uri; Reason = reason } = result
    let msg = String.Format("Download is failed. URI: {0} Reason {1}", uri, reason)
    printColorMessage msg ConsoleColor.Red

let printCrawlResult = function
    | DocumentResult d -> printDocResult d
    | ImageResult i -> printImgResult i
    | FailedResult e -> printErrorResult e

let printUnknownType obj =
    let msg = String.Format("Message type {0} is not supported", obj.GetType())
    printColorMessage msg ConsoleColor.Yellow
    

[<EntryPoint>]
let main argv =
    let system = System.create "consoleSystem" <| Configuration.load()

    let downloaderRef = spawn system "downloadActor" <| actorOf2 downloadActor
    let parserRef = spawn system "parseActor" <| actorOf2 parseActor
    let crawlerRef = spawn system "crawlerActor" <| actorOf2 (crawlerActor downloaderRef parserRef)

    let coordinatorRef = spawn system "coordinatorActor" <| fun mailbox ->
        let runCrawler uri = crawlerRef <! { CrawlDocumentJob.Initiator = mailbox.Self; CrawlDocumentJob.DocumentUri = new Uri(uri) }
        let rec loop() =
            actor {
                let! msg = mailbox.Receive()
                match box msg with
                | :? string as uri -> uri |> runCrawler |> ignore
                | :? CrawlResult as crawlResult -> crawlResult |> printCrawlResult |> ignore
                | obj -> obj |> printUnknownType 
                return! loop()
            }
        loop()

    //"https://www.eurosport.ru/" "https://www.mirf.ru/" "https://docs.microsoft.com/ru-ru/"
    argv
    |> getSiteAddress
    |> (<!) coordinatorRef 

    System.Console.ReadKey() |> ignore
    0
