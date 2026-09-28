using System.Collections.Generic;
using UnityEngine;

public static class CombatFireObstructionQuery
{
    public const int ShipCombatGeometryLayer =
        CombatPhysicsQuery.CombatGeometryLayer;
    public const int WorldCombatObstructionLayer =
        CombatObstructionVolume.LayerIndex;
    public const int BlockingLayerMask =
        (1 << ShipCombatGeometryLayer)
        | (1 << WorldCombatObstructionLayer);


    public static bool TryEvaluateTargeted(
        GameObject sourceShipRoot,
        GameObject intendedTargetShipRoot,
        CombatSide side,
        Vector3 aimEndpointWorld,
        CombatObstructionProfile profile,
        out CombatFireObstructionResult result
    )
    {
        return TryEvaluate(
            sourceShipRoot,
            intendedTargetShipRoot,
            side,
            aimEndpointWorld,
            profile,
            false,
            out result
        );
    }


    public static bool TryEvaluateBlindFire(
        GameObject sourceShipRoot,
        CombatSide side,
        Vector3 aimEndpointWorld,
        CombatObstructionProfile profile,
        out CombatFireObstructionResult result
    )
    {
        return TryEvaluate(
            sourceShipRoot,
            null,
            side,
            aimEndpointWorld,
            profile,
            true,
            out result
        );
    }


    public static bool TryClassifyBlockingCollider(
        Collider collider,
        out CombatFireBlockerKind blockerKind
    )
    {
        return TryClassifyBlockingCollider(
            collider,
            out blockerKind,
            out _
        );
    }


    private static bool TryEvaluate(
        GameObject sourceShipRoot,
        GameObject intendedTargetShipRoot,
        CombatSide side,
        Vector3 aimEndpointWorld,
        CombatObstructionProfile profile,
        bool blindFire,
        out CombatFireObstructionResult result
    )
    {
        result = default;

        if (sourceShipRoot == null
            || (!blindFire
                && (intendedTargetShipRoot == null
                    || intendedTargetShipRoot == sourceShipRoot))
            || (side != CombatSide.Port
                && side != CombatSide.Starboard)
            || !IsFinite(aimEndpointWorld)
            || !TryGetParticipatingMuzzles(
                sourceShipRoot,
                side,
                out IReadOnlyList<Transform> muzzles
            ))
        {
            return false;
        }

        ShipCombatAffiliation sourceAffiliation =
            sourceShipRoot.GetComponent<ShipCombatAffiliation>();
        int blockedRayCount = 0;
        bool hasHardBlindBlocker = false;
        CombatFireBlockerKind representativeKind =
            CombatFireBlockerKind.None;
        CombatRelationship representativeRelationship =
            CombatRelationship.Unknown;
        string representativeName = null;
        Collider representativeCollider = null;
        Vector3 representativeOrigin = muzzles[0].position;

        for (int index = 0; index < muzzles.Count; index++)
        {
            Vector3 originWorld = muzzles[index].position;

            if (!TryFindFirstBlocker(
                originWorld,
                aimEndpointWorld,
                sourceShipRoot.transform,
                intendedTargetShipRoot != null
                    ? intendedTargetShipRoot.transform
                    : null,
                sourceAffiliation,
                out Collider blocker,
                out CombatFireBlockerKind blockerKind,
                out CombatRelationship relationship,
                out string blockerName
            ))
            {
                continue;
            }

            blockedRayCount++;
            bool hardBlindBlocker = blockerKind
                == CombatFireBlockerKind.WorldObstacle
                || relationship != CombatRelationship.Friendly;

            if (representativeCollider == null
                || (blindFire
                    && hardBlindBlocker
                    && !hasHardBlindBlocker))
            {
                representativeKind = blockerKind;
                representativeRelationship = relationship;
                representativeName = blockerName;
                representativeCollider = blocker;
                representativeOrigin = originWorld;
            }

            hasHardBlindBlocker |= hardBlindBlocker;
        }

        float blockedFraction = blockedRayCount / (float)muzzles.Count;
        float targetedAllowedFraction = profile != null
            ? profile.TargetedAutoAllowedBlockedRayFraction
            : 0f;
        float blindFriendlyTolerance = profile != null
            ? profile.BlindFireFriendlyEdgeTolerance
            : 0f;
        bool isBlocked = blindFire
            ? hasHardBlindBlocker
                || blockedFraction
                    > blindFriendlyTolerance
            : blockedFraction
                > targetedAllowedFraction;

        result = new CombatFireObstructionResult(
            isBlocked,
            muzzles.Count,
            blockedRayCount,
            representativeKind,
            representativeRelationship,
            representativeName,
            representativeCollider,
            representativeOrigin
        );
        return true;
    }


    private static bool TryFindFirstBlocker(
        Vector3 originWorld,
        Vector3 aimEndpointWorld,
        Transform sourceRoot,
        Transform intendedTargetRoot,
        ShipCombatAffiliation sourceAffiliation,
        out Collider blocker,
        out CombatFireBlockerKind blockerKind,
        out CombatRelationship relationship,
        out string blockerName
    )
    {
        blocker = null;
        blockerKind = CombatFireBlockerKind.None;
        relationship = CombatRelationship.Unknown;
        blockerName = null;
        Vector3 offset = aimEndpointWorld - originWorld;
        float distanceMeters = offset.magnitude;

        if (!IsFinite(originWorld)
            || !IsFinite(distanceMeters)
            || distanceMeters <= Mathf.Epsilon)
        {
            return false;
        }

        RaycastHit[] hits = CombatPhysicsQuery.RaycastAll(
            originWorld,
            offset / distanceMeters,
            distanceMeters,
            BlockingLayerMask
        );

        foreach (RaycastHit hit in hits)
        {
            Collider candidate = hit.collider;

            if (candidate == null
                || CombatPhysicsQuery.IsInHierarchy(
                    candidate,
                    sourceRoot
                )
                || CombatPhysicsQuery.IsInHierarchy(
                    candidate,
                    intendedTargetRoot
                )
                || !TryClassifyBlockingCollider(
                    candidate,
                    out blockerKind,
                    out GameObject blockerShipRoot
                ))
            {
                continue;
            }

            blocker = candidate;

            if (blockerKind == CombatFireBlockerKind.Ship)
            {
                relationship = CombatRelationshipResolver.Resolve(
                    sourceAffiliation,
                    blockerShipRoot.GetComponent<
                        ShipCombatAffiliation
                    >()
                );
                blockerName = blockerShipRoot.name;
            }
            else if (CombatObstructionVolume.TryResolve(
                candidate,
                out CombatObstructionVolume obstruction
            ))
            {
                blockerName = obstruction.transform.root.name;
            }

            return true;
        }

        return false;
    }


    private static bool TryClassifyBlockingCollider(
        Collider collider,
        out CombatFireBlockerKind blockerKind,
        out GameObject blockerShipRoot
    )
    {
        blockerKind = CombatFireBlockerKind.None;
        blockerShipRoot = null;

        if (collider == null)
        {
            return false;
        }

        if (CombatObstructionVolume.TryResolve(collider, out _))
        {
            blockerKind = CombatFireBlockerKind.WorldObstacle;
            return true;
        }

        if (collider.gameObject.layer != ShipCombatGeometryLayer
            || !collider.enabled
            || !collider.isTrigger
            || !collider.TryGetComponent(out CombatHitRegion hitRegion)
            || hitRegion.QueryCollider != collider
            || hitRegion.Owner == null
            || !hitRegion.Owner.TryResolveRegion(collider, out _))
        {
            return false;
        }

        blockerKind = CombatFireBlockerKind.Ship;
        blockerShipRoot = hitRegion.Owner.gameObject;
        return true;
    }


    private static bool TryGetParticipatingMuzzles(
        GameObject sourceShipRoot,
        CombatSide side,
        out IReadOnlyList<Transform> muzzles
    )
    {
        muzzles = null;
        ShipMuzzleSockets sockets =
            sourceShipRoot.GetComponent<ShipMuzzleSockets>();

        if (sockets == null)
        {
            return false;
        }

        muzzles = side == CombatSide.Port
            ? sockets.PortMuzzles
            : sockets.StarboardMuzzles;

        if (muzzles == null || muzzles.Count == 0)
        {
            return false;
        }

        for (int index = 0; index < muzzles.Count; index++)
        {
            Transform muzzle = muzzles[index];

            if (muzzle == null
                || !muzzle.IsChildOf(sourceShipRoot.transform)
                || !IsFinite(muzzle.position))
            {
                return false;
            }
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
