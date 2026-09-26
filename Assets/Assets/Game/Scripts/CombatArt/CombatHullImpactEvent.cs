using UnityEngine;

public readonly struct CombatHullImpactEvent
{
    public CombatHullImpactEvent(
        Vector3 positionWorld,
        Vector3 normalWorld,
        GameObject targetShip,
        CombatHullRegion hitRegion
    )
    {
        PositionWorld = positionWorld;
        NormalWorld = normalWorld;
        TargetShip = targetShip;
        HitRegion = hitRegion;
    }


    public Vector3 PositionWorld { get; }

    public Vector3 NormalWorld { get; }

    public GameObject TargetShip { get; }

    public CombatHullRegion HitRegion { get; }
}
