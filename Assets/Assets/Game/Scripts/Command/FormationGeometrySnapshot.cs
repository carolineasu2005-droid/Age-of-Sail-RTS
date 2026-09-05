using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

public readonly struct FormationGeometryMember
{
    public ShipDestinationController Ship { get; }

    public float LocalX { get; }

    public float LocalZ { get; }


    public FormationGeometryMember(
        ShipDestinationController ship,
        float localX,
        float localZ
    )
    {
        Ship = ship;
        LocalX = localX;
        LocalZ = localZ;
    }
}


public sealed class FormationGeometrySnapshot
{
    private const float DirectionThresholdSquared = 0.0001f;

    private readonly ReadOnlyCollection<FormationGeometryMember> members;


    public Vector3 FormationCenter { get; }

    public float FormationHeading { get; }

    public Vector3 FormationForward { get; }

    public Vector3 FormationRight { get; }

    public float BoundsMinX { get; }

    public float BoundsMaxX { get; }

    public float BoundsMinZ { get; }

    public float BoundsMaxZ { get; }

    public IReadOnlyList<FormationGeometryMember> Members => members;


    private FormationGeometrySnapshot(
        Vector3 formationCenter,
        float formationHeading,
        Vector3 formationForward,
        Vector3 formationRight,
        float boundsMinX,
        float boundsMaxX,
        float boundsMinZ,
        float boundsMaxZ,
        List<FormationGeometryMember> members
    )
    {
        FormationCenter = formationCenter;
        FormationHeading = formationHeading;
        FormationForward = formationForward;
        FormationRight = formationRight;
        BoundsMinX = boundsMinX;
        BoundsMaxX = boundsMaxX;
        BoundsMinZ = boundsMinZ;
        BoundsMaxZ = boundsMaxZ;
        this.members = new List<FormationGeometryMember>(members)
            .AsReadOnly();
    }


    public static bool TryCapture(
        IReadOnlyList<ShipDestinationController> ships,
        ShipDestinationController primarySelectedShip,
        out FormationGeometrySnapshot snapshot
    )
    {
        snapshot = null;

        if (!TryGetOrderedValidShips(ships,
            out List<ShipDestinationController> orderedShips))
        {
            return false;
        }

        Vector3 formationForward = CalculateFormationForward(
            orderedShips,
            primarySelectedShip
        );
        Vector3 formationRight = Vector3.Cross(
            Vector3.up,
            formationForward
        ).normalized;
        Vector3 referenceOrigin = CalculateAveragePosition(orderedShips);

        float boundsMinX = float.PositiveInfinity;
        float boundsMaxX = float.NegativeInfinity;
        float boundsMinZ = float.PositiveInfinity;
        float boundsMaxZ = float.NegativeInfinity;

        foreach (ShipDestinationController ship in orderedShips)
        {
            Vector3 horizontalOffset = ship.transform.position
                - referenceOrigin;
            horizontalOffset.y = 0f;

            float localX = Vector3.Dot(horizontalOffset, formationRight);
            float localZ = Vector3.Dot(horizontalOffset, formationForward);

            boundsMinX = Mathf.Min(boundsMinX, localX);
            boundsMaxX = Mathf.Max(boundsMaxX, localX);
            boundsMinZ = Mathf.Min(boundsMinZ, localZ);
            boundsMaxZ = Mathf.Max(boundsMaxZ, localZ);
        }

        float boundsCenterX = (boundsMinX + boundsMaxX) * 0.5f;
        float boundsCenterZ = (boundsMinZ + boundsMaxZ) * 0.5f;
        Vector3 formationCenter = referenceOrigin
            + formationRight * boundsCenterX
            + formationForward * boundsCenterZ;

        List<FormationGeometryMember> members =
            new List<FormationGeometryMember>(orderedShips.Count);

        foreach (ShipDestinationController ship in orderedShips)
        {
            Vector3 horizontalOffset = ship.transform.position
                - formationCenter;
            horizontalOffset.y = 0f;

            members.Add(new FormationGeometryMember(
                ship,
                Vector3.Dot(horizontalOffset, formationRight),
                Vector3.Dot(horizontalOffset, formationForward)
            ));
        }

        snapshot = new FormationGeometrySnapshot(
            formationCenter,
            GetHeading(formationForward),
            formationForward,
            formationRight,
            boundsMinX,
            boundsMaxX,
            boundsMinZ,
            boundsMaxZ,
            members
        );
        return true;
    }


    public static Vector3 GetSlotWorldPosition(
        Vector3 formationCenter,
        float formationHeading,
        float localX,
        float localZ
    )
    {
        Vector3 formationForward = HeadingToDirection(formationHeading);
        Vector3 formationRight = Vector3.Cross(
            Vector3.up,
            formationForward
        ).normalized;

        return formationCenter
            + formationRight * localX
            + formationForward * localZ;
    }


    private static bool TryGetOrderedValidShips(
        IReadOnlyList<ShipDestinationController> ships,
        out List<ShipDestinationController> orderedShips
    )
    {
        orderedShips = new List<ShipDestinationController>();

        if (ships == null)
        {
            return false;
        }

        HashSet<ShipDestinationController> uniqueShips =
            new HashSet<ShipDestinationController>();

        foreach (ShipDestinationController ship in ships)
        {
            if (ship != null && uniqueShips.Add(ship))
            {
                orderedShips.Add(ship);
            }
        }

        return orderedShips.Count > 0;
    }


    private static Vector3 CalculateFormationForward(
        IReadOnlyList<ShipDestinationController> ships,
        ShipDestinationController primarySelectedShip
    )
    {
        Vector3 forwardSum = Vector3.zero;

        foreach (ShipDestinationController ship in ships)
        {
            Vector3 horizontalForward = GetHorizontalForward(ship);

            if (horizontalForward.sqrMagnitude > DirectionThresholdSquared)
            {
                forwardSum += horizontalForward.normalized;
            }
        }

        if (forwardSum.sqrMagnitude > DirectionThresholdSquared)
        {
            return forwardSum.normalized;
        }

        Vector3 fallbackForward = GetHorizontalForward(
            primarySelectedShip
        );

        if (fallbackForward.sqrMagnitude <= DirectionThresholdSquared)
        {
            fallbackForward = GetHorizontalForward(ships[0]);
        }

        return fallbackForward.sqrMagnitude > DirectionThresholdSquared
            ? fallbackForward.normalized
            : Vector3.forward;
    }


    private static Vector3 CalculateAveragePosition(
        IReadOnlyList<ShipDestinationController> ships
    )
    {
        Vector3 totalPosition = Vector3.zero;

        foreach (ShipDestinationController ship in ships)
        {
            totalPosition += ship.transform.position;
        }

        return totalPosition / ships.Count;
    }


    private static Vector3 GetHorizontalForward(
        ShipDestinationController ship
    )
    {
        return ship == null
            ? Vector3.zero
            : Vector3.ProjectOnPlane(ship.transform.forward, Vector3.up);
    }


    private static float GetHeading(Vector3 forward)
    {
        return Mathf.Repeat(
            Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg,
            360f
        );
    }


    private static Vector3 HeadingToDirection(float heading)
    {
        return Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
    }
}
