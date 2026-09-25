using UnityEngine;

public enum FireAimSourceKind
{
    Targeted,
    BlindFirePoint
}

public enum FireAimBasisFailure
{
    None,
    FireEligibilityRejected,
    BlindFireEligibilityRejected,
    ExposureUnavailable,
    FiniteAimPointRequired,
    InvalidGeometry
}

public readonly struct FireAimBasis
{
    internal FireAimBasis(
        CombatSide side,
        FireAimSourceKind sourceKind,
        Vector3 aimPlaneCenterWorld,
        Vector3 aimDirectionWorld,
        float aimDistanceMeters
    )
    {
        Side = side;
        SourceKind = sourceKind;
        AimPlaneCenterWorld = aimPlaneCenterWorld;
        AimDirectionWorld = aimDirectionWorld;
        AimDistanceMeters = aimDistanceMeters;
    }


    public CombatSide Side { get; }

    public FireAimSourceKind SourceKind { get; }

    public Vector3 AimPlaneCenterWorld { get; }

    public Vector3 AimDirectionWorld { get; }

    public float AimDistanceMeters { get; }
}
