using UnityEngine;

[CreateAssetMenu(
    fileName = "SO_AutoTargetScoring_Foundation",
    menuName = "AgeOfSailRTS/Combat/Auto Target Scoring Profile"
)]
public sealed class AutoTargetScoringProfile : ScriptableObject
{
    // TEMPORARY / PLAYTEST-BOUND / NOT BALANCE-FROZEN.
    // This neutral source reserves visibility as a distinct scoring input
    // without introducing visibility gameplay in Foundation.
    public const float FoundationVisibilityQualityNormalized = 1f;

    public float VisibilityQualityNormalized =>
        FoundationVisibilityQualityNormalized;
}
