using UnityEngine;

[CreateAssetMenu(
    fileName = "SO_CombatDamage_Foundation",
    menuName = "AgeOfSailRTS/Combat/Combat Damage Profile"
)]
public sealed class CombatDamageProfile : ScriptableObject
{
    // TEMPORARY / PLAYTEST-BOUND / NOT BALANCE-FROZEN.
    // These values establish only the Foundation Round Shot damage handoff.
    // They are not final weapon balance or region-specific hit points.

    [SerializeField]
    [Min(0.01f)]
    private float roundShotBaseDamage = 100f;

    [SerializeField]
    [Min(0.01f)]
    private float bowMultiplier = 1f;

    [SerializeField]
    [Min(0.01f)]
    private float midshipMultiplier = 1f;

    [SerializeField]
    [Min(0.01f)]
    private float sternMultiplier = 1f;


    public float RoundShotBaseDamage => roundShotBaseDamage;

    public float BowMultiplier => bowMultiplier;

    public float MidshipMultiplier => midshipMultiplier;

    public float SternMultiplier => sternMultiplier;


    public float GetRegionMultiplier(CombatHullRegion region)
    {
        switch (region)
        {
            case CombatHullRegion.Bow:
                return bowMultiplier;
            case CombatHullRegion.Midship:
                return midshipMultiplier;
            case CombatHullRegion.Stern:
                return sternMultiplier;
            default:
                return float.NaN;
        }
    }
}
