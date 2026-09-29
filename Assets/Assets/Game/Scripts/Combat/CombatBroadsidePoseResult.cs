using UnityEngine;

public enum CombatRangeState
{
    TooClose,
    InBand,
    TooFar
}

public enum CombatAIMovementIntent
{
    CloseRange,
    OpenRange,
    AlignBroadside,
    HoldCombatPose
}

public readonly struct CombatBroadsidePoseCandidate
{
    internal CombatBroadsidePoseCandidate(
        CombatSide side,
        float desiredHeadingDegrees,
        Vector3 desiredPositionWorld,
        float signedHeadingDeltaDegrees
    )
    {
        Side = side;
        DesiredHeadingDegrees = desiredHeadingDegrees;
        DesiredPositionWorld = desiredPositionWorld;
        SignedHeadingDeltaDegrees = signedHeadingDeltaDegrees;
        HeadingCostDegrees = Mathf.Abs(signedHeadingDeltaDegrees);
        IsValid = true;
    }

    public CombatSide Side { get; }
    public float DesiredHeadingDegrees { get; }
    public Vector3 DesiredPositionWorld { get; }
    public float SignedHeadingDeltaDegrees { get; }
    public float HeadingCostDegrees { get; }
    public bool? NavigationFeasible => null;
    public bool IsValid { get; }
}

public readonly struct CombatBroadsidePoseResult
{
    internal CombatBroadsidePoseResult(
        CombatBroadsidePoseCandidate portCandidate,
        CombatBroadsidePoseCandidate starboardCandidate,
        CombatSide preferredSide,
        CombatRangeState rangeState,
        CombatAIMovementIntent movementIntent,
        float currentRangeMeters,
        float maximumWeaponRangeMeters,
        float desiredRangeMeters
    )
    {
        PortCandidate = portCandidate;
        StarboardCandidate = starboardCandidate;
        PreferredSide = preferredSide;
        RangeState = rangeState;
        MovementIntent = movementIntent;
        CurrentRangeMeters = currentRangeMeters;
        MaximumWeaponRangeMeters = maximumWeaponRangeMeters;
        DesiredRangeMeters = desiredRangeMeters;
        IsValid = true;
    }

    public CombatBroadsidePoseCandidate PortCandidate { get; }
    public CombatBroadsidePoseCandidate StarboardCandidate { get; }
    public CombatSide PreferredSide { get; }
    public Vector3 DesiredPositionWorld => PreferredSide == CombatSide.Port
        ? PortCandidate.DesiredPositionWorld
        : StarboardCandidate.DesiredPositionWorld;
    public float DesiredHeadingDegrees => PreferredSide == CombatSide.Port
        ? PortCandidate.DesiredHeadingDegrees
        : StarboardCandidate.DesiredHeadingDegrees;
    public CombatRangeState RangeState { get; }
    public CombatAIMovementIntent MovementIntent { get; }
    public float CurrentRangeMeters { get; }
    public float MaximumWeaponRangeMeters { get; }
    public float DesiredRangeMeters { get; }
    public bool IsValid { get; }
}
