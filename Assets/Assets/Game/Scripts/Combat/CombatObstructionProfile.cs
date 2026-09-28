using UnityEngine;

[CreateAssetMenu(
    fileName = "SO_CombatObstruction_Foundation",
    menuName = "AgeOfSailRTS/Combat/Combat Obstruction Profile"
)]
public sealed class CombatObstructionProfile : ScriptableObject
{
    // TEMPORARY / PLAYTEST-BOUND / NOT BALANCE-FROZEN.

    [SerializeField]
    [Range(0f, 1f)]
    private float targetedAutoAllowedBlockedRayFraction;

    [SerializeField]
    [Range(0f, 1f)]
    private float blindFireFriendlyEdgeTolerance;


    public float TargetedAutoAllowedBlockedRayFraction =>
        targetedAutoAllowedBlockedRayFraction;

    public float BlindFireFriendlyEdgeTolerance =>
        blindFireFriendlyEdgeTolerance;
}
