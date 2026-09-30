using UnityEngine;

[CreateAssetMenu(
    fileName = "SO_CombatAI_Foundation",
    menuName = "AgeOfSailRTS/Combat/Combat AI Profile"
)]
public sealed class CombatAIProfile : ScriptableObject
{
    // Temporary search policy. This is not cannon range.
    [SerializeField, Min(0.01f)]
    private float thinkIntervalSeconds = 0.25f;

    [SerializeField, Min(0.01f)]
    private float targetAcquisitionRadiusMeters = 1000f;

    [SerializeField, Range(0f, 1f)]
    private float desiredCombatRangeRatio = 0.70f;

    [SerializeField, Range(0f, 1f)]
    private float preferredRangeBandMinRatio = 0.55f;

    [SerializeField, Range(0f, 1f)]
    private float preferredRangeBandMaxRatio = 0.80f;

    [SerializeField, Range(0f, 180f)]
    private float broadsideSideSwitchAdvantageDegrees = 20f;

    // PLAYTEST-BOUND: begin a broadside attack leg while still closing range.
    [SerializeField, Range(0f, 90f)]
    private float broadsideApproachAngleDegrees = 35f;

    [SerializeField, Min(0.01f)]
    private float broadsideApproachLeadDistanceMeters = 120f;

    [SerializeField, Min(0.01f)]
    private float broadsideAlignmentLeadDistanceMeters = 40f;

    [SerializeField, Min(0.01f)]
    private float destinationUpdateThresholdMeters = 5f;

    [SerializeField, Range(0f, 180f)]
    private float desiredHeadingUpdateThresholdDegrees = 5f;

    public float ThinkIntervalSeconds => thinkIntervalSeconds;
    public float TargetAcquisitionRadiusMeters => targetAcquisitionRadiusMeters;
    public float DesiredCombatRangeRatio => desiredCombatRangeRatio;
    public float PreferredRangeBandMinRatio => preferredRangeBandMinRatio;
    public float PreferredRangeBandMaxRatio => preferredRangeBandMaxRatio;
    public float BroadsideSideSwitchAdvantageDegrees =>
        broadsideSideSwitchAdvantageDegrees;
    public float BroadsideApproachAngleDegrees =>
        broadsideApproachAngleDegrees;
    public float BroadsideApproachLeadDistanceMeters =>
        broadsideApproachLeadDistanceMeters;
    public float BroadsideAlignmentLeadDistanceMeters =>
        broadsideAlignmentLeadDistanceMeters;
    public float DestinationUpdateThresholdMeters =>
        destinationUpdateThresholdMeters;
    public float DesiredHeadingUpdateThresholdDegrees =>
        desiredHeadingUpdateThresholdDegrees;

    public bool IsValid =>
        IsFinite(thinkIntervalSeconds) && thinkIntervalSeconds > 0f
        && IsFinite(targetAcquisitionRadiusMeters)
        && targetAcquisitionRadiusMeters > 0f;

    public bool IsPoseValid =>
        IsValid
        && IsFinite(desiredCombatRangeRatio)
        && IsFinite(preferredRangeBandMinRatio)
        && IsFinite(preferredRangeBandMaxRatio)
        && IsFinite(broadsideSideSwitchAdvantageDegrees)
        && IsFinite(broadsideApproachAngleDegrees)
        && IsFinite(broadsideApproachLeadDistanceMeters)
        && preferredRangeBandMinRatio > 0f
        && preferredRangeBandMinRatio <= desiredCombatRangeRatio
        && desiredCombatRangeRatio <= preferredRangeBandMaxRatio
        && preferredRangeBandMaxRatio <= 1f
        && broadsideSideSwitchAdvantageDegrees >= 0f
        && broadsideSideSwitchAdvantageDegrees <= 180f
        && broadsideApproachAngleDegrees >= 0f
        && broadsideApproachAngleDegrees <= 90f
        && broadsideApproachLeadDistanceMeters > 0f;

    public bool IsMovementValid =>
        IsPoseValid
        && IsFinite(broadsideAlignmentLeadDistanceMeters)
        && broadsideAlignmentLeadDistanceMeters > 0f
        && IsFinite(destinationUpdateThresholdMeters)
        && destinationUpdateThresholdMeters > 0f
        && IsFinite(desiredHeadingUpdateThresholdDegrees)
        && desiredHeadingUpdateThresholdDegrees >= 0f
        && desiredHeadingUpdateThresholdDegrees <= 180f;

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
