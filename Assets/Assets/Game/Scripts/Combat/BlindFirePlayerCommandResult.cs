using System;
using UnityEngine;

[Flags]
public enum BlindFirePlayerCommandFailure
{
    None = 0,
    SelectionInvalid = 1 << 0,
    EligibilityUnavailable = 1 << 1,
    ExecutionRejected = 1 << 2
}

public readonly struct BlindFirePlayerCommandResult
{
    internal BlindFirePlayerCommandResult(
        bool attempted,
        GameObject shooterShipRoot,
        Vector3 worldAimPoint,
        bool eligibilityAvailable,
        BlindFireEligibilityResult eligibility,
        BlindFireExecutionResult execution,
        BlindFirePlayerCommandFailure failureReasons
    )
    {
        Attempted = attempted;
        ShooterShipRoot = shooterShipRoot;
        WorldAimPoint = worldAimPoint;
        EligibilityAvailable = eligibilityAvailable;
        Eligibility = eligibility;
        Execution = execution;
        FailureReasons = failureReasons;
    }


    public bool Attempted { get; }

    public GameObject ShooterShipRoot { get; }

    public Vector3 WorldAimPoint { get; }

    public bool EligibilityAvailable { get; }

    public BlindFireEligibilityResult Eligibility { get; }

    public BlindFireExecutionResult Execution { get; }

    public BlindFirePlayerCommandFailure FailureReasons { get; }

    public bool Accepted => Attempted && Execution.Accepted;
}
