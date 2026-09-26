using UnityEngine;

[CreateAssetMenu(
    fileName = "SO_ShipSinkingPresentation_Foundation",
    menuName = "AgeOfSailRTS/Combat/Ship Sinking Presentation Profile"
)]
public sealed class ShipSinkingPresentationProfile : ScriptableObject
{
    // TEMPORARY / PLAYTEST-BOUND / PRESENTATION-ONLY / NOT BALANCE-FROZEN.
    // These values provide only the Foundation defeated-state visual response.

    [SerializeField]
    [Min(0.01f)]
    private float sinkDurationSeconds = 8f;

    [SerializeField]
    [Min(0.01f)]
    private float sinkDepthMeters = 6f;

    [SerializeField]
    [Range(-15f, 15f)]
    private float optionalRollDegrees = 8f;


    public float SinkDurationSeconds => sinkDurationSeconds;

    public float SinkDepthMeters => sinkDepthMeters;

    public float OptionalRollDegrees => optionalRollDegrees;
}
