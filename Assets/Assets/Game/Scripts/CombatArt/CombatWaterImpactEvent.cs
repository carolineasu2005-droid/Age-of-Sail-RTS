using UnityEngine;

public readonly struct CombatWaterImpactEvent
{
    public CombatWaterImpactEvent(
        Vector3 positionWorld,
        Vector3 normalWorld
    )
    {
        PositionWorld = positionWorld;
        NormalWorld = normalWorld;
    }


    public Vector3 PositionWorld { get; }

    public Vector3 NormalWorld { get; }
}
