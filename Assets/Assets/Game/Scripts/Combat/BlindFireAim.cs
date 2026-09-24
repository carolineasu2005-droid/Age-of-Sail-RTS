using UnityEngine;

public readonly struct BlindFireAim
{
    internal BlindFireAim(
        bool isValid,
        Vector3 worldAimDirection,
        Vector3? worldAimPoint,
        float? aimPointDistanceMeters
    )
    {
        IsValid = isValid;
        WorldAimDirection = worldAimDirection;
        WorldAimPoint = worldAimPoint;
        AimPointDistanceMeters = aimPointDistanceMeters;
    }


    public bool IsValid { get; }

    public Vector3 WorldAimDirection { get; }

    public Vector3? WorldAimPoint { get; }

    public bool HasWorldAimPoint => WorldAimPoint.HasValue;

    public float? AimPointDistanceMeters { get; }
}
