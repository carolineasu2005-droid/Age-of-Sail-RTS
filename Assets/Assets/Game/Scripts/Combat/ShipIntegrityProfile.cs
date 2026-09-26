using UnityEngine;

[CreateAssetMenu(
    fileName = "SO_ShipIntegrity_Foundation",
    menuName = "AgeOfSailRTS/Combat/Ship Integrity Profile"
)]
public sealed class ShipIntegrityProfile : ScriptableObject
{
    // TEMPORARY / PLAYTEST-BOUND / NOT BALANCE-FROZEN.
    // These values exist only to establish the Foundation ship-wide
    // Integrity and lifecycle contract. They are not final combat balance.

    [SerializeField]
    [Min(0.01f)]
    private float maximumIntegrity = 1000f;

    [SerializeField]
    [Range(0f, 1f)]
    private float combatDisabledThresholdNormalized = 0.25f;

    [SerializeField]
    [Range(0f, 1f)]
    private float sinkingThresholdNormalized = 0.05f;


    public float MaximumIntegrity => maximumIntegrity;

    public float CombatDisabledThresholdNormalized =>
        combatDisabledThresholdNormalized;

    public float SinkingThresholdNormalized =>
        sinkingThresholdNormalized;
}
