using UnityEditor;
using UnityEngine;

public static class ShipCombatGeometryGizmos
{
    private static readonly Color BowColor = new Color(0.25f, 0.8f, 1f);
    private static readonly Color MidshipColor = new Color(0.3f, 1f, 0.4f);
    private static readonly Color SternColor = new Color(1f, 0.65f, 0.2f);


    [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
    private static void DrawSelectedShipCombatGeometry(
        ShipCombatGeometry geometry,
        GizmoType gizmoType
    )
    {
        DrawRegion(geometry.BowRegion, BowColor);
        DrawRegion(geometry.MidshipRegion, MidshipColor);
        DrawRegion(geometry.SternRegion, SternColor);
    }


    private static void DrawRegion(CombatHitRegion region, Color color)
    {
        if (region == null || region.QueryCollider == null)
        {
            return;
        }

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousGizmoColor = Gizmos.color;
        Color previousHandleColor = Handles.color;
        BoxCollider boxCollider = region.QueryCollider as BoxCollider;

        Gizmos.color = color;
        Handles.color = color;

        if (boxCollider != null)
        {
            Gizmos.matrix = boxCollider.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
        }
        else
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawWireCube(
                region.QueryCollider.bounds.center,
                region.QueryCollider.bounds.size
            );
        }

        Handles.Label(
            region.QueryCollider.bounds.center,
            region.Region.ToString()
        );

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousGizmoColor;
        Handles.color = previousHandleColor;
    }
}
