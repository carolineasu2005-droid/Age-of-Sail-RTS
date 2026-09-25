using UnityEngine;

public static class CombatProjectileContactQuery
{
    private const float MinimumSegmentLengthMeters = 0.000001f;
    private const float ContactTieEpsilon = 0.00001f;


    public static bool TryFindEarliestContact(
        ShotSample shotSample,
        Vector3 previousPositionWorld,
        Vector3 nextPositionWorld,
        float previousTimeSeconds,
        float currentTimeSeconds,
        out ProjectileTerminalContact contact
    )
    {
        contact = default;

        if (!IsFinite(previousPositionWorld)
            || !IsFinite(nextPositionWorld)
            || !IsFinite(previousTimeSeconds)
            || !IsFinite(currentTimeSeconds)
            || currentTimeSeconds < previousTimeSeconds)
        {
            return false;
        }

        Vector3 segment = nextPositionWorld - previousPositionWorld;
        float segmentLengthMeters = segment.magnitude;
        bool hasHull = TryFindHullContact(
            shotSample,
            previousPositionWorld,
            segment,
            segmentLengthMeters,
            out RaycastHit hullHit,
            out float hullFraction
        );
        bool hasWater = TryFindWaterContact(
            previousPositionWorld,
            nextPositionWorld,
            shotSample.WaterLevelWorldY,
            out Vector3 waterPointWorld,
            out float waterFraction
        );

        if (!hasHull && !hasWater)
        {
            return false;
        }

        float timeRangeSeconds = currentTimeSeconds - previousTimeSeconds;

        if (hasHull
            && (!hasWater
                || hullFraction <= waterFraction + ContactTieEpsilon))
        {
            contact = new ProjectileTerminalContact(
                ProjectileTerminalContactKind.CombatGeometryContact,
                shotSample,
                hullHit.point,
                hullHit.normal,
                hullHit.collider,
                hullFraction,
                previousTimeSeconds + timeRangeSeconds * hullFraction
            );
            return true;
        }

        contact = new ProjectileTerminalContact(
            ProjectileTerminalContactKind.WaterContact,
            shotSample,
            waterPointWorld,
            Vector3.up,
            null,
            waterFraction,
            previousTimeSeconds + timeRangeSeconds * waterFraction
        );
        return true;
    }


    private static bool TryFindHullContact(
        ShotSample shotSample,
        Vector3 previousPositionWorld,
        Vector3 segment,
        float segmentLengthMeters,
        out RaycastHit hullHit,
        out float segmentFraction
    )
    {
        hullHit = default;
        segmentFraction = 0f;

        if (!IsFinite(segmentLengthMeters)
            || segmentLengthMeters <= MinimumSegmentLengthMeters)
        {
            return false;
        }

        Transform sourceRoot = shotSample.SourceShipRootIdentity != null
            ? shotSample.SourceShipRootIdentity.transform
            : null;
        RaycastHit[] hits = CombatPhysicsQuery.RaycastAllCombatGeometry(
            previousPositionWorld,
            segment / segmentLengthMeters,
            segmentLengthMeters
        );

        foreach (RaycastHit hit in hits)
        {
            Collider candidate = hit.collider;

            if (candidate == null
                || candidate.gameObject.layer
                    != CombatPhysicsQuery.CombatGeometryLayer
                || CombatPhysicsQuery.IsInHierarchy(candidate, sourceRoot))
            {
                continue;
            }

            hullHit = hit;
            segmentFraction = Mathf.Clamp01(
                hit.distance / segmentLengthMeters
            );
            return true;
        }

        return false;
    }


    private static bool TryFindWaterContact(
        Vector3 previousPositionWorld,
        Vector3 nextPositionWorld,
        float waterLevelWorldY,
        out Vector3 waterPointWorld,
        out float segmentFraction
    )
    {
        waterPointWorld = Vector3.zero;
        segmentFraction = 0f;

        if (!IsFinite(waterLevelWorldY)
            || previousPositionWorld.y <= waterLevelWorldY
            || nextPositionWorld.y > waterLevelWorldY)
        {
            return false;
        }

        float denominator =
            previousPositionWorld.y - nextPositionWorld.y;

        if (!IsFinite(denominator) || denominator <= 0f)
        {
            return false;
        }

        segmentFraction = Mathf.Clamp01(
            (previousPositionWorld.y - waterLevelWorldY) / denominator
        );
        waterPointWorld = Vector3.LerpUnclamped(
            previousPositionWorld,
            nextPositionWorld,
            segmentFraction
        );
        return IsFinite(waterPointWorld);
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
