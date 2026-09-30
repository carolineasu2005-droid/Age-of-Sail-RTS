using UnityEngine;

public enum CombatAIMovementCommandStatus
{
    None,
    Unavailable,
    InvalidPose,
    PlayerStopped,
    HoldingPose,
    Unchanged,
    Submitted,
    SubmittedBlocked
}

// A command bridge only. ShipDestinationController and its Movement owners
// retain all navigation, maneuver, rudder, and Root pose authority.
public sealed class CombatAIMovementAdapter
{
    private readonly ShipDestinationController destinationController;
    private readonly ShipSailingSpeed sailingSpeed;

    private bool hasIssuedCommand;
    private bool lastCommandBlocked;
    private GameObject lastTargetShipRoot;
    private CombatAIMovementIntent lastIntent;
    private Vector3 lastDestinationWorld;
    private float lastDesiredHeadingDegrees;

    public CombatAIMovementCommandStatus LastCommandStatus { get; private set; }

    public CombatAIMovementAdapter(ShipDestinationController destinationController)
    {
        this.destinationController = destinationController;
        sailingSpeed = destinationController != null
            ? destinationController.GetComponent<ShipSailingSpeed>()
            : null;
    }

    public bool TryApply(
        CombatBroadsidePoseResult pose,
        GameObject targetShipRoot,
        CombatAIProfile profile
    )
    {
        if (destinationController == null || !destinationController.isActiveAndEnabled)
        {
            ResetTacticalState();
            LastCommandStatus = CombatAIMovementCommandStatus.Unavailable;
            return false;
        }

        if (targetShipRoot == null
            || profile == null
            || !profile.IsMovementValid
            || !pose.IsValid
            || !IsFinite(pose.DesiredHeadingDegrees)
            || !IsFinite(pose.DesiredPositionWorld))
        {
            LastCommandStatus = CombatAIMovementCommandStatus.InvalidPose;
            return false;
        }

        if (sailingSpeed != null && sailingSpeed.IsPlayerStopped)
        {
            LastCommandStatus = CombatAIMovementCommandStatus.PlayerStopped;
            return false;
        }

        if (pose.MovementIntent == CombatAIMovementIntent.HoldCombatPose)
        {
            lastTargetShipRoot = targetShipRoot;
            lastIntent = pose.MovementIntent;
            LastCommandStatus = CombatAIMovementCommandStatus.HoldingPose;
            return false;
        }

        Vector3 destinationWorld;
        float requestedHeadingDegrees = pose.DesiredHeadingDegrees;
        switch (pose.MovementIntent)
        {
            case CombatAIMovementIntent.CloseRange:
                if (!pose.ApproachActive
                    || !IsFinite(pose.ApproachHeadingDegrees)
                    || !IsFinite(pose.ApproachDestinationWorld))
                {
                    LastCommandStatus = CombatAIMovementCommandStatus.InvalidPose;
                    return false;
                }

                destinationWorld = pose.ApproachDestinationWorld;
                requestedHeadingDegrees = pose.ApproachHeadingDegrees;
                break;

            case CombatAIMovementIntent.OpenRange:
                destinationWorld = pose.DesiredPositionWorld;
                break;

            case CombatAIMovementIntent.AlignBroadside:
                Vector3 forward = Quaternion.Euler(
                    0f, pose.DesiredHeadingDegrees, 0f
                ) * Vector3.forward;
                destinationWorld = destinationController.transform.position
                    + forward * profile.BroadsideAlignmentLeadDistanceMeters;
                break;

            default:
                LastCommandStatus = CombatAIMovementCommandStatus.InvalidPose;
                return false;
        }

        if (!IsFinite(destinationWorld))
        {
            LastCommandStatus = CombatAIMovementCommandStatus.InvalidPose;
            return false;
        }

        bool blockedNow = destinationController.CurrentNavigationMode
            == ShipDestinationController.NavigationMode.Blocked;
        Vector3 offset = destinationWorld - lastDestinationWorld;
        offset.y = 0f;
        bool materiallyDifferentDestination =
            offset.sqrMagnitude > profile.DestinationUpdateThresholdMeters
                * profile.DestinationUpdateThresholdMeters;
        bool materiallyDifferentHeading = Mathf.Abs(Mathf.DeltaAngle(
            lastDesiredHeadingDegrees,
            requestedHeadingDegrees
        )) > profile.DesiredHeadingUpdateThresholdDegrees;

        if (hasIssuedCommand
            && lastTargetShipRoot == targetShipRoot
            && lastIntent == pose.MovementIntent
            && !materiallyDifferentDestination
            && !materiallyDifferentHeading
            && destinationController.HasDestination
            && !(blockedNow && !lastCommandBlocked))
        {
            lastCommandBlocked = blockedNow;
            LastCommandStatus = CombatAIMovementCommandStatus.Unchanged;
            return false;
        }

        destinationController.SetDestination(
            destinationWorld,
            ShipDestinationController.TurnSelectionMode.Auto,
            WindNavigationAssistMode.Assisted
        );
        lastTargetShipRoot = targetShipRoot;
        lastIntent = pose.MovementIntent;
        lastDestinationWorld = destinationWorld;
        lastDesiredHeadingDegrees = requestedHeadingDegrees;
        lastCommandBlocked = destinationController.CurrentNavigationMode
            == ShipDestinationController.NavigationMode.Blocked;
        hasIssuedCommand = true;
        LastCommandStatus = lastCommandBlocked
            ? CombatAIMovementCommandStatus.SubmittedBlocked
            : CombatAIMovementCommandStatus.Submitted;
        return true;
    }

    public void ResetTacticalState()
    {
        hasIssuedCommand = false;
        lastCommandBlocked = false;
        lastTargetShipRoot = null;
        lastIntent = default;
        lastDestinationWorld = default;
        lastDesiredHeadingDegrees = 0f;
        LastCommandStatus = CombatAIMovementCommandStatus.None;
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
