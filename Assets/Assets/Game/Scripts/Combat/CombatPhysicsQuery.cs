using System;
using UnityEngine;

internal static class CombatPhysicsQuery
{
    internal const int CombatGeometryLayer = 8;
    internal const int CombatGeometryMask = 1 << CombatGeometryLayer;


    internal static RaycastHit[] RaycastAllCombatGeometry(
        Vector3 originWorld,
        Vector3 directionWorld,
        float distanceMeters
    )
    {
        RaycastHit[] hits = Physics.RaycastAll(
            originWorld,
            directionWorld,
            distanceMeters,
            CombatGeometryMask,
            QueryTriggerInteraction.Collide
        );
        Array.Sort(hits, CompareRaycastHits);
        return hits;
    }


    internal static bool IsInHierarchy(
        Collider collider,
        Transform hierarchyRoot
    )
    {
        return collider != null
            && hierarchyRoot != null
            && collider.transform.IsChildOf(hierarchyRoot);
    }


    private static int CompareRaycastHits(
        RaycastHit left,
        RaycastHit right
    )
    {
        int distanceComparison = left.distance.CompareTo(right.distance);

        if (distanceComparison != 0)
        {
            return distanceComparison;
        }

        if (left.collider == null)
        {
            return right.collider == null ? 0 : -1;
        }

        if (right.collider == null)
        {
            return 1;
        }

        return left.collider.GetEntityId().CompareTo(
            right.collider.GetEntityId()
        );
    }
}
