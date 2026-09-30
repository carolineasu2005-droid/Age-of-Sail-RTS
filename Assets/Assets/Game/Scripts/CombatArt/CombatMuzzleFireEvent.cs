using UnityEngine;

public readonly struct CombatMuzzleFireEvent
{
    public CombatMuzzleFireEvent(
        Vector3 positionWorld,
        Vector3 directionWorld,
        GameObject sourceShip,
        string muzzleIdentifier,
        Transform muzzleTransform = null
    )
    {
        PositionWorld = positionWorld;
        DirectionWorld = directionWorld;
        SourceShip = sourceShip;
        MuzzleIdentifier = muzzleIdentifier;
        MuzzleTransform = muzzleTransform;
    }


    public Vector3 PositionWorld { get; }

    public Vector3 DirectionWorld { get; }

    public GameObject SourceShip { get; }

    public string MuzzleIdentifier { get; }

    public Transform MuzzleTransform { get; }
}
