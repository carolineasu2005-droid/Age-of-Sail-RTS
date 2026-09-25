using UnityEngine;

public readonly struct CombatHitContext
{
    internal CombatHitContext(
        ShotSample sourceShot,
        GameObject targetShipRoot,
        ShipCombatGeometry targetCombatGeometry,
        CombatHitRegion hitRegion,
        Vector3 worldHitPoint,
        Vector3 worldHitNormal,
        Vector3 incomingVelocityWorld
    )
    {
        SourceShot = sourceShot;
        TargetShipRoot = targetShipRoot;
        TargetCombatGeometry = targetCombatGeometry;
        HitRegion = hitRegion;
        Region = hitRegion.Region;
        WorldHitPoint = worldHitPoint;
        WorldHitNormal = worldHitNormal;
        IncomingVelocityWorld = incomingVelocityWorld;
        IncomingDirectionWorld = incomingVelocityWorld.normalized;
        AmmunitionType = sourceShot.AmmunitionType;
    }


    public ShotSample SourceShot { get; }

    public GameObject TargetShipRoot { get; }

    public ShipCombatGeometry TargetCombatGeometry { get; }

    public CombatHitRegion HitRegion { get; }

    public CombatHullRegion Region { get; }

    public Vector3 WorldHitPoint { get; }

    public Vector3 WorldHitNormal { get; }

    public Vector3 IncomingVelocityWorld { get; }

    public Vector3 IncomingDirectionWorld { get; }

    public FoundationAmmunitionType AmmunitionType { get; }
}
