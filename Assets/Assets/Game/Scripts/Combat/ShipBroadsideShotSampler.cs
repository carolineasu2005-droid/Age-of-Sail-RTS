using System.Collections.Generic;
using UnityEngine;

public enum BroadsideShotSamplingFailure
{
    None,
    InvalidSourceShip,
    InvalidAimBasis,
    MissingConfiguration,
    InvalidMuzzleCollection,
    InvalidMuzzleSocket,
    InvalidWaterline,
    InvalidDispersion,
    InvalidFlightProfile,
    InvalidBallisticSolution
}

public static class ShipBroadsideShotSampler
{
    private const float MinimumFlightTimeSeconds = 0.0001f;


    public static bool TrySample(
        GameObject sourceShipRoot,
        FireAimBasis aimBasis,
        uint broadsideSeed,
        out BroadsideShotSamplingResult result,
        out BroadsideShotSamplingFailure failure
    )
    {
        result = default;
        failure = BroadsideShotSamplingFailure.InvalidSourceShip;

        if (sourceShipRoot == null)
        {
            return false;
        }

        ShipMuzzleSockets muzzleSockets =
            sourceShipRoot.GetComponent<ShipMuzzleSockets>();
        ShipArtDefinition artDefinition =
            sourceShipRoot.GetComponent<ShipArtDefinition>();
        ShipDispersionConfiguration dispersionConfiguration =
            sourceShipRoot.GetComponent<ShipDispersionConfiguration>();
        ShipProjectileFlightConfiguration flightConfiguration =
            sourceShipRoot.GetComponent<
                ShipProjectileFlightConfiguration
            >();

        if (muzzleSockets == null
            || artDefinition == null
            || dispersionConfiguration == null
            || dispersionConfiguration.DispersionProfile == null
            || flightConfiguration == null
            || flightConfiguration.ProjectileFlightProfile == null)
        {
            failure = BroadsideShotSamplingFailure.MissingConfiguration;
            return false;
        }

        if (!TryGetMuzzles(
            muzzleSockets,
            aimBasis.Side,
            out IReadOnlyList<Transform> muzzles
        ))
        {
            failure = BroadsideShotSamplingFailure.InvalidAimBasis;
            return false;
        }

        if (muzzles == null || muzzles.Count == 0)
        {
            failure = BroadsideShotSamplingFailure.InvalidMuzzleCollection;
            return false;
        }

        if (!TrySnapshotMuzzleOrigins(
            sourceShipRoot.transform,
            muzzles,
            out Vector3[] originsWorld
        ))
        {
            failure = BroadsideShotSamplingFailure.InvalidMuzzleSocket;
            return false;
        }

        if (artDefinition.WaterlineReference == null
            || !IsFinite(artDefinition.WaterlineReference.position.y))
        {
            failure = BroadsideShotSamplingFailure.InvalidWaterline;
            return false;
        }

        if (!DispersionGeometry.TryCalculate(
            aimBasis,
            dispersionConfiguration.DispersionProfile,
            out _,
            out DispersionEllipse ellipse
        ))
        {
            failure = BroadsideShotSamplingFailure.InvalidDispersion;
            return false;
        }

        ProjectileFlightProfile flightProfile =
            flightConfiguration.ProjectileFlightProfile;

        if (!TrySnapshotFlightProfile(
            flightProfile,
            out float horizontalSpeedMetersPerSecond,
            out Vector3 gravityWorld,
            out float maxLifetimeSeconds
        ))
        {
            failure = BroadsideShotSamplingFailure.InvalidFlightProfile;
            return false;
        }

        float waterLevelWorldY =
            artDefinition.WaterlineReference.position.y;
        ShotSample[] samples = new ShotSample[originsWorld.Length];
        DeterministicRandom32 random = new DeterministicRandom32(
            broadsideSeed
        );

        for (int index = 0; index < originsWorld.Length; index++)
        {
            if (!DeterministicDispersionSampler.TrySample(
                ref random,
                ellipse,
                out Vector3 samplePointWorld
            ) || !TryCalculateBallistics(
                originsWorld[index],
                samplePointWorld,
                horizontalSpeedMetersPerSecond,
                gravityWorld,
                maxLifetimeSeconds,
                out float flightTimeSeconds,
                out Vector3 initialVelocityWorld
            ))
            {
                failure = BroadsideShotSamplingFailure.InvalidBallisticSolution;
                return false;
            }

            samples[index] = new ShotSample(
                sourceShipRoot,
                aimBasis.Side,
                index,
                broadsideSeed,
                originsWorld[index],
                samplePointWorld,
                initialVelocityWorld,
                gravityWorld,
                flightTimeSeconds,
                waterLevelWorldY,
                maxLifetimeSeconds,
                FoundationAmmunitionType.RoundShot
            );
        }

        result = new BroadsideShotSamplingResult(
            aimBasis.Side,
            broadsideSeed,
            aimBasis,
            ellipse,
            samples
        );
        failure = BroadsideShotSamplingFailure.None;
        return true;
    }


    private static bool TryGetMuzzles(
        ShipMuzzleSockets muzzleSockets,
        CombatSide side,
        out IReadOnlyList<Transform> muzzles
    )
    {
        switch (side)
        {
            case CombatSide.Port:
                muzzles = muzzleSockets.PortMuzzles;
                return true;

            case CombatSide.Starboard:
                muzzles = muzzleSockets.StarboardMuzzles;
                return true;

            default:
                muzzles = null;
                return false;
        }
    }


    private static bool TrySnapshotMuzzleOrigins(
        Transform sourceShipRoot,
        IReadOnlyList<Transform> muzzles,
        out Vector3[] originsWorld
    )
    {
        originsWorld = new Vector3[muzzles.Count];

        for (int index = 0; index < muzzles.Count; index++)
        {
            Transform muzzle = muzzles[index];

            if (muzzle == null
                || !muzzle.IsChildOf(sourceShipRoot)
                || !IsFinite(muzzle.position))
            {
                originsWorld = null;
                return false;
            }

            originsWorld[index] = muzzle.position;
        }

        return true;
    }


    private static bool TrySnapshotFlightProfile(
        ProjectileFlightProfile profile,
        out float horizontalSpeedMetersPerSecond,
        out Vector3 gravityWorld,
        out float maxLifetimeSeconds
    )
    {
        horizontalSpeedMetersPerSecond =
            profile.NominalHorizontalSpeedMetersPerSecond;
        float gravityMagnitudeMetersPerSecondSquared =
            profile.GravityMagnitudeMetersPerSecondSquared;
        maxLifetimeSeconds = profile.MaxLifetimeSeconds;
        gravityWorld = new Vector3(
            0f,
            -gravityMagnitudeMetersPerSecondSquared,
            0f
        );

        return IsFinite(horizontalSpeedMetersPerSecond)
            && horizontalSpeedMetersPerSecond > 0f
            && IsFinite(gravityMagnitudeMetersPerSecondSquared)
            && gravityMagnitudeMetersPerSecondSquared > 0f
            && IsFinite(maxLifetimeSeconds)
            && maxLifetimeSeconds > MinimumFlightTimeSeconds;
    }


    private static bool TryCalculateBallistics(
        Vector3 originWorld,
        Vector3 samplePointWorld,
        float horizontalSpeedMetersPerSecond,
        Vector3 gravityWorld,
        float maxLifetimeSeconds,
        out float flightTimeSeconds,
        out Vector3 initialVelocityWorld
    )
    {
        flightTimeSeconds = 0f;
        initialVelocityWorld = Vector3.zero;
        Vector3 delta = samplePointWorld - originWorld;
        Vector3 horizontalDelta = new Vector3(delta.x, 0f, delta.z);
        float horizontalDistanceMeters = horizontalDelta.magnitude;

        if (!IsFinite(originWorld)
            || !IsFinite(samplePointWorld)
            || !IsFinite(horizontalDistanceMeters))
        {
            return false;
        }

        flightTimeSeconds =
            horizontalDistanceMeters / horizontalSpeedMetersPerSecond;

        if (!IsFinite(flightTimeSeconds)
            || flightTimeSeconds <= MinimumFlightTimeSeconds
            || flightTimeSeconds >= maxLifetimeSeconds)
        {
            flightTimeSeconds = 0f;
            return false;
        }

        initialVelocityWorld = (
            samplePointWorld
            - originWorld
            - 0.5f
                * gravityWorld
                * flightTimeSeconds
                * flightTimeSeconds
        ) / flightTimeSeconds;

        if (!IsFinite(initialVelocityWorld))
        {
            flightTimeSeconds = 0f;
            initialVelocityWorld = Vector3.zero;
            return false;
        }

        return true;
    }


    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x)
            && IsFinite(value.y)
            && IsFinite(value.z);
    }


    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
