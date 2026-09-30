using UnityEngine;

public static class CombatProjectileContactQuery
{
    private const float MinimumSegmentLengthMeters = 0.000001f;
    private const float ContactTieEpsilon = 0.00001f;
    private const int PhysicalContactMask =
        CombatPhysicsQuery.CombatGeometryMask
        | (1 << CombatObstructionVolume.LayerIndex);


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
        bool hasPhysicalContact = TryFindPhysicalContact(
            shotSample,
            previousPositionWorld,
            segment,
            segmentLengthMeters,
            out RaycastHit physicalHit,
            out ProjectileTerminalContactKind physicalContactKind,
            out float physicalFraction
        );
        bool hasWater = TryFindWaterContact(
            previousPositionWorld,
            nextPositionWorld,
            shotSample.WaterLevelWorldY,
            out Vector3 waterPointWorld,
            out float waterFraction
        );

        if (!hasPhysicalContact && !hasWater)
        {
            return false;
        }

        float timeRangeSeconds = currentTimeSeconds - previousTimeSeconds;

        if (hasPhysicalContact
            && (!hasWater
                || physicalFraction
                    <= waterFraction + ContactTieEpsilon))
        {
            contact = new ProjectileTerminalContact(
                physicalContactKind,
                shotSample,
                physicalHit.point,
                physicalHit.normal,
                physicalHit.collider,
                physicalFraction,
                previousTimeSeconds
                    + timeRangeSeconds * physicalFraction
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


    private static bool TryFindPhysicalContact(
        ShotSample shotSample,
        Vector3 previousPositionWorld,
        Vector3 segment,
        float segmentLengthMeters,
        out RaycastHit physicalHit,
        out ProjectileTerminalContactKind contactKind,
        out float segmentFraction
    )
    {
        physicalHit = default;
        contactKind = default;
        segmentFraction = 0f;

        if (!IsFinite(segmentLengthMeters)
            || segmentLengthMeters <= MinimumSegmentLengthMeters)
        {
            return false;
        }

        Transform sourceRoot = shotSample.SourceShipRootIdentity != null
            ? shotSample.SourceShipRootIdentity.transform
            : null;
        RaycastHit[] hits = CombatPhysicsQuery.RaycastAll(
            previousPositionWorld,
            segment / segmentLengthMeters,
            segmentLengthMeters,
            PhysicalContactMask
        );

        foreach (RaycastHit hit in hits)
        {
            Collider candidate = hit.collider;

            if (candidate == null
                || CombatPhysicsQuery.IsInHierarchy(candidate, sourceRoot))
            {
                continue;
            }

            if (candidate.gameObject.layer
                == CombatPhysicsQuery.CombatGeometryLayer)
            {
                contactKind = ProjectileTerminalContactKind
                    .CombatGeometryContact;
            }
            else if (!CombatObstructionVolume.TryResolve(
                candidate,
                out _
            ))
            {
                continue;
            }
            else
            {
                contactKind = ProjectileTerminalContactKind
                    .WorldObstructionContact;
            }

            physicalHit = hit;
            segmentFraction = Mathf.Clamp01(
                hit.distance / segmentLengthMeters
            );
            return true;
        }

        return false;
    }


    internal static bool TryFindWaterContact(
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
