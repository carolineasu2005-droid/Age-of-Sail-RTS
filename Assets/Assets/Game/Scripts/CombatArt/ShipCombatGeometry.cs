using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShipCombatGeometry : MonoBehaviour
{
    [Header("Combat Geometry")]

    [SerializeField]
    private Transform mainHullRoot;

    [SerializeField]
    private CombatHitRegion bowRegion;

    [SerializeField]
    private CombatHitRegion midshipRegion;

    [SerializeField]
    private CombatHitRegion sternRegion;


    public Transform MainHullRoot => mainHullRoot;

    public CombatHitRegion BowRegion => bowRegion;

    public CombatHitRegion MidshipRegion => midshipRegion;

    public CombatHitRegion SternRegion => sternRegion;


    public bool TryResolveRegion(
        Collider collider,
        out CombatHitRegion region
    )
    {
        region = null;

        if (collider == null)
        {
            return false;
        }

        if (IsConfiguredMatch(bowRegion, CombatHullRegion.Bow, collider))
        {
            region = bowRegion;
            return true;
        }

        if (IsConfiguredMatch(
            midshipRegion,
            CombatHullRegion.Midship,
            collider
        ))
        {
            region = midshipRegion;
            return true;
        }

        if (IsConfiguredMatch(sternRegion, CombatHullRegion.Stern, collider))
        {
            region = sternRegion;
            return true;
        }

        return false;
    }


    private bool IsConfiguredMatch(
        CombatHitRegion candidate,
        CombatHullRegion expectedRegion,
        Collider collider
    )
    {
        return candidate != null
            && candidate.Owner == this
            && candidate.Region == expectedRegion
            && candidate.QueryCollider == collider;
    }
}
