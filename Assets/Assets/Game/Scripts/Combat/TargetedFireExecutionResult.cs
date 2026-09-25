using System;
using UnityEngine;

[Flags]
public enum TargetedFireExecutionFailure
{
    None = 0,
    InvalidDependencies = 1 << 0,
    EligibilityUnavailable = 1 << 1,
    EligibilityRejected = 1 << 2,
    AimBasisUnavailable = 1 << 3,
    BroadsideExecutionRejected = 1 << 4
}

public readonly struct TargetedFireExecutionResult
{
    internal TargetedFireExecutionResult(
        bool accepted,
        GameObject targetShipRoot,
        FireEligibilityResult eligibility,
        FireAimBasisFailure aimBasisFailure,
        BroadsideFireExecutionResult broadsideExecution,
        TargetedFireExecutionFailure failureReasons
    )
    {
        Accepted = accepted;
        TargetShipRoot = targetShipRoot;
        Eligibility = eligibility;
        AimBasisFailure = aimBasisFailure;
        BroadsideExecution = broadsideExecution;
        FailureReasons = failureReasons;
    }


    public bool Accepted { get; }

    public GameObject TargetShipRoot { get; }

    public FireEligibilityResult Eligibility { get; }

    public FireAimBasisFailure AimBasisFailure { get; }

    public BroadsideFireExecutionResult BroadsideExecution { get; }

    public TargetedFireExecutionFailure FailureReasons { get; }
}
