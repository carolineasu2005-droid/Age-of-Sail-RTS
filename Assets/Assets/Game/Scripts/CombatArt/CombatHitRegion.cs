using UnityEngine;

public enum CombatHullRegion
{
    Bow,
    Midship,
    Stern
}

[DisallowMultipleComponent]
public sealed class CombatHitRegion : MonoBehaviour
{
    [SerializeField]
    private CombatHullRegion region;

    [SerializeField]
    private ShipCombatGeometry owner;

    [SerializeField]
    private Collider queryCollider;


    public CombatHullRegion Region => region;

    public ShipCombatGeometry Owner => owner;

    public Collider QueryCollider => queryCollider;
}
