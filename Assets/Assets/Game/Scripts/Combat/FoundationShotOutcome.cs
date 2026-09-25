using UnityEngine;

public enum FoundationShotOutcomeKind
{
    HullHit,
    WaterMiss,
    ExpiredNonHit
}

public readonly struct FoundationShotOutcome
{
    internal FoundationShotOutcome(
        FoundationShotOutcomeKind kind,
        ShotSample sourceShot,
        Vector3 worldPoint,
        Vector3 worldNormal,
        CombatHitContext hitContext,
        bool hasHitContext
    )
    {
        Kind = kind;
        SourceShot = sourceShot;
        WorldPoint = worldPoint;
        WorldNormal = worldNormal;
        HitContext = hitContext;
        HasHitContext = hasHitContext;
    }


    public FoundationShotOutcomeKind Kind { get; }

    public ShotSample SourceShot { get; }

    public Vector3 WorldPoint { get; }

    public Vector3 WorldNormal { get; }

    public CombatHitContext HitContext { get; }

    public bool HasHitContext { get; }

    public bool IsHit => Kind == FoundationShotOutcomeKind.HullHit;
}
