using UnityEditor;
using UnityEngine;

public static class ShipExposureReferenceGizmos
{
    private const float RectLineWidth = 3f;
    private const float SightLineSpacing = 4f;

    private static readonly Color RectColor = new Color(1f, 0.25f, 0.85f);
    private static readonly Color AxisColor = new Color(1f, 0.8f, 0.2f);
    private static readonly Color SightColor = new Color(0.65f, 0.8f, 1f);


    [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
    private static void DrawSelectedShipExposure(
        ShipExposureReference exposureReference,
        GizmoType gizmoType
    )
    {
        SceneView sceneView = SceneView.currentDrawingSceneView;

        if (sceneView == null
            || sceneView.camera == null
            || !exposureReference.TryCalculateExposure(
                sceneView.camera.transform.position,
                out ExposureRect exposure
            ))
        {
            return;
        }

        Color previousColor = Handles.color;
        Vector3 horizontalExtent = exposure.HorizontalAxisWorld
            * exposure.WidthMeters
            * 0.5f;
        Vector3 verticalExtent = exposure.VerticalAxisWorld
            * exposure.HeightMeters
            * 0.5f;
        Vector3 lowerLeft = exposure.CenterWorld
            - horizontalExtent
            - verticalExtent;
        Vector3 lowerRight = exposure.CenterWorld
            + horizontalExtent
            - verticalExtent;
        Vector3 upperRight = exposure.CenterWorld
            + horizontalExtent
            + verticalExtent;
        Vector3 upperLeft = exposure.CenterWorld
            - horizontalExtent
            + verticalExtent;

        Handles.color = RectColor;
        Handles.DrawAAPolyLine(
            RectLineWidth,
            lowerLeft,
            lowerRight,
            upperRight,
            upperLeft,
            lowerLeft
        );
        Handles.SphereHandleCap(
            0,
            exposure.CenterWorld,
            Quaternion.identity,
            0.25f,
            EventType.Repaint
        );

        Handles.color = AxisColor;
        Handles.DrawLine(
            exposure.CenterWorld - horizontalExtent,
            exposure.CenterWorld + horizontalExtent
        );
        Handles.DrawLine(
            exposure.CenterWorld - verticalExtent,
            exposure.CenterWorld + verticalExtent
        );
        Handles.DrawLine(
            exposure.CenterWorld,
            exposure.CenterWorld
                + exposure.PlaneNormalWorld
                    * Mathf.Max(exposure.WidthMeters, exposure.HeightMeters)
                    * 0.2f
        );

        Handles.color = SightColor;
        Handles.DrawDottedLine(
            sceneView.camera.transform.position,
            exposure.CenterWorld,
            SightLineSpacing
        );
        Handles.Label(
            upperLeft + exposure.VerticalAxisWorld * 0.35f,
            "Target Exposure (Scene View observer)\n"
                + $"Width: {exposure.WidthMeters:F1} m\n"
                + $"Height: {exposure.HeightMeters:F1} m\n"
                + $"Area: {exposure.AreaSquareMeters:F1} m²"
        );

        Handles.color = previousColor;
    }
}
