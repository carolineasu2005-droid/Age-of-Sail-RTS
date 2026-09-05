using UnityEngine;

[CreateAssetMenu(
    fileName = "ShipMovementProfile",
    menuName = "AgeOfSailRTS/Sailing/Movement Profile"
)]
public class ShipMovementProfile : ScriptableObject
{
    [Header("Sailing")]

    [Min(0f)]
    public float baseMaxSpeed = 4f;

    [Min(0.01f)]
    public float accelerationTimeConstant = 8f;

    [Min(0.01f)]
    public float naturalDragTimeConstant = 12f;

    [Min(0.01f)]
    public float fullTurnDragTimeConstant = 20f;


    [Header("Turning")]

    [Min(0.01f)]
    public float maxRudderAngle = 30f;

    [Min(0f)]
    public float rudderResponse = 45f;

    [Min(0.01f)]
    public float rudderReferenceSpeed = 3f;

    [Min(0f)]
    public float maxTurnRate = 6f;


    [Header("Tacking")]

    [Min(0f)]
    public float tackYawAssistRate = 7f;
}
