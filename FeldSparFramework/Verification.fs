﻿namespace FeldSpar.Framework.Verification

open System
open ApprovalsSupport
open FeldSpar.Framework
open FeldSpar.Framework.TestResultUtilities
open ApprovalTests.Core

[<AutoOpen>]
module Checks =
    let isTrue failure success =
        if success
        then Success
        else Failure(failure)

    let expectsToBe expected message actual =
        if expected = actual
        then Success
        else
            let failureMessage = 
                try
                    sprintf message expected actual
                with
                    e -> raise e
                    
            Failure(ExpectationFailure(failureMessage))

    let private checkStandardsAndReport env (reporter:IApprovalFailureReporter) (approver:IApprovalApprover) =
        if(approver.Approve ())
        then
            do approver.CleanUpAfterSucess(reporter) 
            Success
        else 
            do approver.ReportFailure (reporter)
                                                                
            match reporter with
            | :? IReporterWithApprovalPower as approvalReporter -> 
                if approvalReporter.ApprovedWhenReported ()
                then do approver.CleanUpAfterSucess(reporter)
            | _ -> ()

            Failure(StandardNotMet)
    
    let private checkAgainstStandard env (approver:IApprovalApprover) =
        let repoter = getReporter env
        checkStandardsAndReport env repoter approver

    let checkAgainstStringStandard env result =
        let approver = getStringFileApprover env result
        checkAgainstStandard env approver

    let checkAgainstStandardObjectAsString env result =
        checkAgainstStringStandard env (sprintf "%A" result)

    let checkAgainstStandardBinary  env extentionWithoutDot results =
        let reporter = getReporter env
        let approver = getBinaryFileApprover env extentionWithoutDot results
        checkStandardsAndReport env reporter approver

    let checkAgainstStandardStream env extentionWithoutDot results =
        let reporter = getReporter env
        let approver = getStreamFileApprover env extentionWithoutDot results
        checkStandardsAndReport env reporter approver

    (* Does not work not sure why
    let checkStandardsAgainstString env result =
        let approver = getStringFileApprover env result
        let reporter = getConfiguredReporter ()

        checkStandardsAndReport env reporter approver

    let checkStandardsAgainstObjectAsString env result =
        let approver = getStringFileApprover env (sprintf "%A" result)
        let reporter = getConfiguredReporter ()

        checkStandardsAndReport env reporter approver

    let checkStandardsAgainstBinary env extentionWithoutDot results =
        let approver = getBinaryFileApprover env extentionWithoutDot results
        let reporter = getConfiguredReporter ()

        checkStandardsAndReport env reporter approver

    let checkStandardsAgainstStream env extentionWithoutDot results =
        let approver = getBinaryFileApprover env extentionWithoutDot results
        let reporter = getConfiguredReporter ()

        checkStandardsAndReport env reporter approver
    //*)

    type Validation () =
        member this.Bind(m, f) = 
            match m with
            | Success -> f(m)
            | failure -> failure

        member this.Zero () =
            indeterminateTest

        member this.Delay(f) = f()
        member this.Combine(m, f) =
            match m with
            | Success -> f(m)
            | failure -> failure

        member this.Return(result) = result
        member this.ReturnFor(result) = result

    let verify = Validation()