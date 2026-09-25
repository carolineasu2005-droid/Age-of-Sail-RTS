using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu(
    "Age of Sail/Combat/Ship Auto Target Scoring Configuration"
)]
public sealed class ShipAutoTargetScoringConfiguration : MonoBehaviour
{
    [SerializeField]
    private AutoTargetScoringProfile scoringProfile;

    public AutoTargetScoringProfile ScoringProfile => scoringProfile;
}
