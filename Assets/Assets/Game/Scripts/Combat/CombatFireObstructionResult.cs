using UnityEngine;

public enum CombatFireBlockerKind
{
    None,
    Ship,
    WorldObstacle
}

public readonly struct CombatFireObstructionResult
{
    internal CombatFireObstructionResult(
        bool isBlocked,
        int participatingRayCount,
        int blockedRayCount,
        CombatFireBlockerKind representativeBlockerKind,
        CombatRelationship blockerRelationship,
        string representativeBlockerName,
        Collider representativeCollider,
        Vector3 representativeRayOriginWorld
    )
    {
        ParticipatingRayCount = participatingRayCount;
        BlockedRayCount = blockedRayCount;
        BlockedFraction = participatingRayCount > 0
            ? blockedRayCount / (float)participatingRayCount
            : 0f;
        IsBlocked = isBlocked;
        bool hasBlockedRay = blockedRayCount > 0;
        RepresentativeBlockerKind = hasBlockedRay
            ? representativeBlockerKind
            : CombatFireBlockerKind.None;
        BlockerRelationship = hasBlockedRay
            && representativeBlockerKind == CombatFireBlockerKind.Ship
                ? blockerRelationship
                : CombatRelationship.Unknown;
        RepresentativeBlockerName = hasBlockedRay
            ? representativeBlockerName
            : null;
        RepresentativeCollider = hasBlockedRay
            ? representativeCollider
            : null;
        RepresentativeRayOriginWorld = representativeRayOriginWorld;
    }


    public bool IsBlocked { get; }

    public int ParticipatingRayCount { get; }

    public int BlockedRayCount { get; }

    public float BlockedFraction { get; }

    public CombatFireBlockerKind RepresentativeBlockerKind { get; }

    public CombatRelationship BlockerRelationship { get; }

    public string RepresentativeBlockerName { get; }

    internal Collider RepresentativeCollider { get; }

    internal Vector3 RepresentativeRayOriginWorld { get; }
}
