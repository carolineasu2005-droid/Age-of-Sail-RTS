using UnityEngine;

[CreateAssetMenu(
    fileName = "SO_CombatRaking_Foundation",
    menuName = "AgeOfSailRTS/Combat/Combat Raking Profile"
)]
public sealed class CombatRakingProfile : ScriptableObject
{
    // TEMPORARY / PLAYTEST-BOUND / NOT BALANCE-FROZEN.
    // These values configure stateless Raking geometry evaluation; Damage
    // consumes only the resulting multiplier through the registered resolver.

    [SerializeField]
    [Range(0f, 90f)]
    private float rakingHalfAngleDegrees = 20f;

    [SerializeField]
    [Min(0f)]
    private float bowDamageMultiplier = 2f;

    [SerializeField]
    [Min(0f)]
    private float sternDamageMultiplier = 2f;


    public float RakingHalfAngleDegrees => rakingHalfAngleDegrees;

    public float BowDamageMultiplier => bowDamageMultiplier;

    public float SternDamageMultiplier => sternDamageMultiplier;
}
