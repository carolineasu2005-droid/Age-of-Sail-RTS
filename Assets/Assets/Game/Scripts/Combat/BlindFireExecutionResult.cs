using System;

[Flags]
public enum BlindFireExecutionFailure
{
    None = 0,
    InvalidAim = 1 << 0,
    EligibilityUnavailable = 1 << 1,
    EligibilityRejected = 1 << 2,
    BroadsideCommitFailed = 1 << 3
}

public readonly struct BlindFireExecutionResult
{
    internal BlindFireExecutionResult(
        bool accepted,
        CombatSide? side,
        BlindFireAim aim,
        BlindFireEligibilityResult eligibility,
        BlindFireExecutionFailure failureReasons
    )
    {
        Accepted = accepted;
        Side = side;
        Aim = aim;
        Eligibility = eligibility;
        FailureReasons = failureReasons;
    }


    public bool Accepted { get; }

    public CombatSide? Side { get; }

    public BlindFireAim Aim { get; }

    public BlindFireEligibilityResult Eligibility { get; }

    public BlindFireExecutionFailure FailureReasons { get; }
}
