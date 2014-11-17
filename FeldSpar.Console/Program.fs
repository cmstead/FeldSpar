﻿namespace FeldSpar.Console
open System
open FeldSpar.Framework
open FeldSpar.Framework.ConsoleRunner
open FeldSpar.Framework.Engine
open FeldSpar.Framework.TestSummaryUtilities
open FeldSpar.Framework.Verification.ApprovalsSupport

open ApprovalTests
open Nessos.UnionArgParser

type CommandArguments =
    | [<Mandatory>][<AltCommandLine("--a")>]Test_Assembly of string
    | [<AltCommandLine("--r")>]Report_Location of string
    | [<AltCommandLine("--v")>]Verbose
    | [<AltCommandLine("--ve")>]Verbose_Errors
    | [<AltCommandLine("--al")>]Auto_Loop
    | [<AltCommandLine("--p")>]Pause
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Report_Location _ -> "This flag indicates that a JSON report is to be generated at the given location"
            | Test_Assembly _ -> "This is the location of the test library. It can be a *.dll or a *.exe file"
            | Verbose -> "This prints to the console all events while running."
            | Verbose_Errors -> "This prints only failing tests to console. It is ignored if \"verbose\" is used."
            | Auto_Loop -> "This makes the command contiuously run executing on every compile."
            | Pause -> "This makes the console wait for key press inorder to exit. This is automaticly in effect if \"auto-loop\" is used"

type Launcher () =
    inherit MarshalByRefObject ()

    let runTests savePath runner tests =
        printfn "Running Tests"

        let testsFeldSpar = tests |> List.map runner
        match savePath with
        | Some(path) ->
            let jsonFeldSpar = testsFeldSpar |> List.map buildOutputReport |> List.map JSONFormat

            IO.File.WriteAllText(path, jsonFeldSpar |> List.reduce (fun a b -> a + Environment.NewLine + b))
        | _ -> ()

        printfn "Done!"

        testsFeldSpar

    let run args =
        try
            let parser = UnionArgParser<CommandArguments>()

            let args = parser.Parse(args)

            let assebmlyValidation a =
                let ext = IO.Path.GetExtension a
                if not ([".exe"; ".dll"] |> List.exists (fun e -> e = ext)) then failwith "invalid file extension must be *.dll or *.exe"

                if not (IO.FileInfo(a).Exists) then failwith (sprintf "%A must exist" a)

                a

            let pause = args.Contains (<@ Pause @>) || args.Contains (<@ Auto_Loop @>)

            let tests = args.PostProcessResults(<@ Test_Assembly @>, assebmlyValidation )

            let savePath =
                let saveJSONReport = args.Contains <@ Report_Location @>

                if saveJSONReport
                then
                    let pathValue = args.GetResult <@ Report_Location @>
                    let fName = System.IO.Path.GetFileName pathValue

                    if fName = null
                    then raise (ArgumentException("reportlocation must contain a valid file name"))

                    let fileInfo = IO.FileInfo(pathValue)

                    if fileInfo.Exists
                    then fileInfo.Delete ()

                    Some(fileInfo.FullName)
                else
                    None

            let runner = 
                if args.Contains <@ Verbose @>
                then runAndReportAll
                elif args.Contains <@ Verbose_Errors @>
                then runAndReportFailure
                else runAndReportNone

            let autoLoop = args.Contains <@ Auto_Loop @> 
            if autoLoop 
            then
                let paths = tests
                
                for path in paths do
                    let fileName = IO.Path.GetFileName path
                    let path = IO.Path.GetDirectoryName path
                    let watcherA = new IO.FileSystemWatcher(path, fileName)

                    let changed (ar:IO.FileSystemEventArgs) = 
                        Threading.Thread.Sleep 100
                        [ar.FullPath] |> runTests savePath runner |> ignore

                    let created (ar:IO.FileSystemEventArgs) = 
                        Threading.Thread.Sleep 100
                        [ar.FullPath] |> runTests savePath runner |> ignore

                    watcherA.Changed.Add changed
                    watcherA.Created.Add created

                    watcherA.EnableRaisingEvents <- true

            let results = runTests savePath runner tests |> List.collect(fun (_, r) -> r)

            if pause then Console.ReadKey true |> ignore
            
            if autoLoop then 0
            else
                results 
                    |> List.filter
                        (
                            fun { TestDescription = _;  TestCanonicalizedName = _; TestResults = r } -> 
                                
                                match r with
                                | Failure(Ignored(_)) -> false
                                | Failure(_) -> true
                                | _ -> false
                        )

                    |> List.length

        with
        | ex -> 
            printfn "%A" (ex.Message)
            -1

    member this.Run args = 
        run args

module Program =
    [<EntryPoint>]
    let public main args = 
        let applicationName = "FeldSparConsole"
        let path = IO.Path.GetDirectoryName(Reflection.Assembly.GetExecutingAssembly().Location)
        let appDomain = AppDomain.CreateDomain(applicationName, null, path, path, true)

        let launcherType = typeof<Launcher>
        let sandBoxAssemblyName = launcherType.Assembly.FullName
        let sandBoxTypeName = launcherType.FullName

        let sandbox = appDomain.CreateInstanceAndUnwrap(sandBoxAssemblyName, sandBoxTypeName) :?> Launcher

        sandbox.Run args
