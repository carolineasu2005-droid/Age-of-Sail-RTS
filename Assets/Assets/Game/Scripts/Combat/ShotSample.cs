using UnityEngine;

public enum FoundationAmmunitionType
{
    RoundShot
}

public readonly struct ShotSample
{
    internal ShotSample(
        GameObject sourceShipRootIdentity,
        CombatSide side,
        int muzzleSequenceIndex,
        uint broadsideSeed,
        Vector3 originWorld,
        Vector3 aimPlaneSamplePointWorld,
        Vector3 initialVelocityWorld,
        Vector3 gravityWorld,
        float nominalFlightTimeSeconds,
        float waterLevelWorldY,
        float maxLifetimeSeconds,
        FoundationAmmunitionType ammunitionType
    )
    {
        SourceShipRootIdentity = sourceShipRootIdentity;
        Side = side;
        MuzzleSequenceIndex = muzzleSequenceIndex;
        BroadsideSeed = broadsideSeed;
        OriginWorld = originWorld;
        AimPlaneSamplePointWorld = aimPlaneSamplePointWorld;
        InitialVelocityWorld = initialVelocityWorld;
        GravityWorld = gravityWorld;
        NominalFlightTimeSeconds = nominalFlightTimeSeconds;
        WaterLevelWorldY = waterLevelWorldY;
        MaxLifetimeSeconds = maxLifetimeSeconds;
        AmmunitionType = ammunitionType;
    }


    public GameObject SourceShipRootIdentity { get; }

    public CombatSide Side { get; }

    public int MuzzleSequenceIndex { get; }

    public uint BroadsideSeed { get; }

    public Vector3 OriginWorld { get; }

    public Vector3 AimPlaneSamplePointWorld { get; }

    public Vector3 InitialVelocityWorld { get; }

    public Vector3 GravityWorld { get; }

    public float NominalFlightTimeSeconds { get; }

    public float WaterLevelWorldY { get; }

    public float MaxLifetimeSeconds { get; }

    public FoundationAmmunitionType AmmunitionType { get; }
}
