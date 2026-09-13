using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ShipMuzzleSocketGizmos
{
    private const float SocketRadius = 0.16f;
    private const float DirectionLength = 2f;

    private static readonly Color PortColor = new Color(0.95f, 0.3f, 0.25f);
    private static readonly Color StarboardColor = new Color(0.2f, 0.65f, 1f);


    [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
    private static void DrawSelectedShipMuzzleSockets(
        ShipMuzzleSockets muzzleSockets,
        GizmoType gizmoType
    )
    {
        DrawCollection(muzzleSockets.PortMuzzles, "P", PortColor);
        DrawCollection(muzzleSockets.StarboardMuzzles, "S", StarboardColor);
    }


    private static void DrawCollection(
        IReadOnlyList<Transform> sockets,
        string sidePrefix,
        Color color
    )
    {
        Gizmos.color = color;

        for (int index = 0; index < sockets.Count; index++)
        {
            Transform socket = sockets[index];

            if (socket == null)
            {
                continue;
            }

            Vector3 position = socket.position;
            Gizmos.DrawSphere(position, SocketRadius);
            Gizmos.DrawLine(
                position,
                position + socket.forward * DirectionLength
            );
            Handles.Label(position, sidePrefix + (index + 1).ToString("00"));
        }
    }
}
