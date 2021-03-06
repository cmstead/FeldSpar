﻿namespace FeldSpar.Console.Tests
open FeldSpar.Framework
open FeldSpar.Framework.Verification
open ApprovalTests.Reporters

module StandardsVerificationTests =
    type Color = | White | Brown | Black | TooCool
    type TestingType =
        {
            Name : string;
            Age:int;
            Dojo:string*Color
        }
    
    let ``A test to check verification`` = 
        Test((fun env ->
                let itemUnderTest = 
                    sprintf "%A%s"
                        ({
                            Name = "Steven";
                            Age = 38;
                            Dojo = ("Too Cool For School", TooCool)
                        }) "\n"

                verify
                    {
                        let! standardsAreGood = itemUnderTest |> checkAgainstStringStandard env
                        return Success
                    }
            ))

