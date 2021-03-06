﻿namespace FeldSpar.Framework.Verification
open FeldSpar.Framework
open FeldSpar.Framework.TestSummaryUtilities

(*
    This would not be possible without the help of Llewellyn Falco and his Approval Tests
    Almost all this code was lifted from the Open Source OO version of Approval Tests.
    Please Check them out at:
        https://github.com/approvals/ApprovalTests.Net/
*)
module ApprovalsSupport = 
    open ApprovalTests.Core
    open ApprovalTests.Reporters
    open System.IO

    let thanksUrl = "https://github.com/approvals/ApprovalTests.Net/"

    type FindReporterResult =
        | FoundReporter of IApprovalFailureReporter
        | Searching

    let private writeTo fullPath writer result =
        Directory.CreateDirectory (Path.GetDirectoryName (fullPath)) |> ignore
        do writer fullPath result
        fullPath        

    let private writeBinaryTo fullPath result =
        let writer path toWrite = File.WriteAllBytes(path, toWrite)
        result |> writeTo fullPath writer

    let private writeTextTo fullPath result =
        let writer path toWrite = File.WriteAllText(path, toWrite, System.Text.Encoding.UTF8)
        result |> writeTo fullPath writer

    let private getStringFileWriter result = 
        { new IApprovalWriter with 
            member this.GetApprovalFilename(baseName) = sprintf "%s.approved.txt" baseName
            member this.GetReceivedFilename(baseName) = sprintf "%s.recieved.txt" baseName
            member this.WriteReceivedFile(fullPathForRecievedFile) = 
                result |> writeTextTo fullPathForRecievedFile
        }

    let private getBinaryFileWriter extentionWithoutDot result =
        { new IApprovalWriter with
            member this.GetApprovalFilename(baseName) = sprintf "%s.approved.%s" baseName extentionWithoutDot
            member this.GetReceivedFilename(baseName) = sprintf "%s.recieved.%s" baseName extentionWithoutDot
            member this.WriteReceivedFile(fullPathForRecievedFile) = 
                result |> writeBinaryTo fullPathForRecievedFile
        }

    let private getBinaryStreamWriter extentionWithoutDot (result:Stream) =
        let length = int result.Length
        let data : byte array = Array.zeroCreate length

        result.Read(data, 0, data.Length) |> ignore
        getBinaryFileWriter extentionWithoutDot data

    let getPath (env:TestEnvironment) =
        let trace = System.Diagnostics.StackTrace(true)

        let (|GetFileName|_|) (frame:System.Diagnostics.StackFrame) =
            if (frame.GetMethod ()).DeclaringType.Assembly = env.Assembly
            then Some(frame.GetFileName ())
            else None

        let rec find frames = 
            match frames with
            | [] -> env.GoldStandardPath
            | (GetFileName name)::_ -> 
                let dir = System.IO.FileInfo(name).Directory
                if dir.Exists
                then dir.FullName + "\\"
                else env.GoldStandardPath

            | _::tail -> find tail

        find (trace.GetFrames () |> Seq.toList)

    let private getNamer (env:TestEnvironment) = 

        let path = getPath env

        {  new IApprovalNamer with
            member this.SourcePath with get () = path
            member this.Name with get () = env.CanonicalizedName
        }

    let createReporter<'a when 'a:> IApprovalFailureReporter> () =
        System.Activator.CreateInstance<'a>() :> IApprovalFailureReporter

    let private buildReporter (getReporters: (unit -> IApprovalFailureReporter) List) =
        let reporters = getReporters |> List.map (fun getter -> getter())

        if  reporters.IsEmpty
        then QuietReporter() :> IApprovalFailureReporter
        else
            MultiReporter(reporters |> List.toSeq) :> IApprovalFailureReporter

    let getReporter (env : TestEnvironment)= 
        match env.Reporters with
        | [] -> 
            QuietReporter() :> IApprovalFailureReporter
        | reporters -> reporters |> buildReporter

    let addReporter<'a when 'a :> IApprovalFailureReporter> (env:TestEnvironment) =
        let reporter = fun () -> System.Activator.CreateInstance<'a>() :> IApprovalFailureReporter

        { env with
            Reporters = reporter :: env.Reporters
        }

    let getStringFileApprover env result =
        ApprovalTests.Approvers.FileApprover(getStringFileWriter result, getNamer env) :> IApprovalApprover

    let getBinaryFileApprover env extentionWithoutDot result =
        ApprovalTests.Approvers.FileApprover(getBinaryFileWriter extentionWithoutDot result, getNamer env)

    let getStreamFileApprover env extentionWithoutDot (result:Stream) =
        ApprovalTests.Approvers.FileApprover(getBinaryStreamWriter extentionWithoutDot result, getNamer env)

    let findFirstReporter<'a when 'a :> IApprovalFailureReporter> findReporterResult =
        match findReporterResult with
        | FoundReporter(_) -> findReporterResult
        | _ ->
            try
                let reporter = createReporter<'a> ()
                FoundReporter(reporter)
            with
            | _ ->
                Searching

    let unWrapReporter findReporterResult =
        match findReporterResult with
        | FoundReporter(reporter) -> 
            reporter
        | _ -> createReporter<ApprovalTests.Reporters.QuietReporter> ()
