using UnityEngine;

public enum ProjectileTerminalContactKind
{
    CombatGeometryContact,
    WaterContact,
    ExpiredSafetyFallback
}

public readonly struct ProjectileTerminalContact
{
    internal ProjectileTerminalContact(
        ProjectileTerminalContactKind kind,
        ShotSample shotSample,
        Vector3 pointWorld,
        Vector3 normalWorld,
        Collider contactedCollider,
        float segmentFraction,
        float elapsedFlightTimeSeconds
    )
    {
        Kind = kind;
        ShotSample = shotSample;
        PointWorld = pointWorld;
        NormalWorld = normalWorld;
        ContactedCollider = contactedCollider;
        SegmentFraction = segmentFraction;
        ElapsedFlightTimeSeconds = elapsedFlightTimeSeconds;
    }


    public ProjectileTerminalContactKind Kind { get; }

    public ShotSample ShotSample { get; }

    public Vector3 PointWorld { get; }

    public Vector3 NormalWorld { get; }

    public Collider ContactedCollider { get; }

    public float SegmentFraction { get; }

    public float ElapsedFlightTimeSeconds { get; }
}
