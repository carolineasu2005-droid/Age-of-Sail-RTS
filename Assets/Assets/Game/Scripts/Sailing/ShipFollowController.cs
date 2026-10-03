using System;
using System.Collections.Generic;
using UnityEngine;

// Experimental relationship only. ShipCommandDispatcher owns the player command boundary.
[DisallowMultipleComponent]
public sealed class ShipFollowController : MonoBehaviour
{
    public enum RelationshipState
    {
        None,
        Following,
        TargetLost
    }

    private RelationshipState state;
    private ShipFollowController followTarget;
    private ShipFollowTrailRecorder targetTrailRecorder;
    private double followStartLeaderDistance;
    private double consumedTrailDistance;
    private ShipFollowTrailSample followStartSample;
    private int followStartTurnSequence;
    private int relationshipVersion;

    public event Action RelationshipChanged;

    public RelationshipState State => state;
    public bool IsFollowing => state == RelationshipState.Following;
    public bool HasFollowIntent => state != RelationshipState.None;
    public ShipFollowController FollowTarget => followTarget;
    public ShipFollowTrailRecorder TargetTrailRecorder => targetTrailRecorder;
    public double FollowStartLeaderDistance => followStartLeaderDistance;
    public double ConsumedTrailDistance => consumedTrailDistance;
    public ShipFollowTrailSample FollowStartSample => followStartSample;
    public int FollowStartTurnSequence => followStartTurnSequence;
    public int RelationshipVersion => relationshipVersion;

    public bool TryBeginFollowRelationship(ShipFollowController target)
    {
        // Entire preflight precedes replacing the old relationship/subscription.
        if (!CanBeginFollowRelationship(target))
        {
            return false;
        }

        ShipFollowTrailRecorder recorder = target.GetComponent<ShipFollowTrailRecorder>();
        if (recorder == null || !recorder.isActiveAndEnabled)
        {
            return false;
        }

        if (IsFollowing && followTarget == target && targetTrailRecorder == recorder
            && recorder.TryGetFollowerProgress(this, out _, out _))
        {
            return true;
        }

        if (!recorder.TrySubscribe(this, out double startDistance))
        {
            return false;
        }

        if (targetTrailRecorder != null && targetTrailRecorder != recorder)
        {
            targetTrailRecorder.Unsubscribe(this);
        }

        followTarget = target;
        targetTrailRecorder = recorder;
        followStartLeaderDistance = startDistance;
        consumedTrailDistance = startDistance;
        followStartSample = recorder.HeadSample;
        followStartTurnSequence = recorder.SubscriptionTurnSequence;
        state = RelationshipState.Following;
        relationshipVersion++;
        RelationshipChanged?.Invoke();
        return true;
    }

    public void CancelFollow()
    {
        bool changed = HasFollowIntent;
        if (targetTrailRecorder != null)
        {
            targetTrailRecorder.Unsubscribe(this);
        }

        state = RelationshipState.None;
        followTarget = null;
        targetTrailRecorder = null;
        followStartLeaderDistance = 0d;
        consumedTrailDistance = 0d;
        followStartSample = default;
        followStartTurnSequence = 0;
        if (changed)
        {
            relationshipVersion++;
            RelationshipChanged?.Invoke();
        }
    }

    public bool ReportConsumedTrailDistance(double distance)
    {
        RefreshFollowRelationship();
        if (!IsFollowing || !targetTrailRecorder.TryReportProgress(this, distance))
        {
            return false;
        }

        consumedTrailDistance = distance;
        return true;
    }

    // Deterministic lifecycle boundary, also polled in Update. It issues no Movement command.
    public void RefreshFollowRelationship()
    {
        if (!IsFollowing)
        {
            return;
        }

        if (!IsMovementEntityValid())
        {
            CancelFollow();
        }
        else if (followTarget == null || !followTarget.IsMovementEntityValid()
            || targetTrailRecorder == null || !targetTrailRecorder.isActiveAndEnabled
            || !targetTrailRecorder.TryGetFollowerProgress(this, out _, out _))
        {
            LoseTarget();
        }
    }

    internal bool IsMovementEntityValid()
    {
        return this != null && isActiveAndEnabled
            && IsActive(GetComponent<ShipDestinationController>())
            && IsActive(GetComponent<ShipSailingSpeed>())
            && IsActive(GetComponent<ShipTurning>())
            && IsActive(GetComponent<ShipHeadingController>())
            && IsActive(GetComponent<ShipManeuverPlanner>())
            && IsActive(GetComponent<ShipFollowTrailRecorder>());
    }

    internal void OnTrailRecorderUnavailable(ShipFollowTrailRecorder recorder)
    {
        if (IsFollowing && targetTrailRecorder == recorder)
        {
            LoseTarget();
        }
    }

    private void LoseTarget()
    {
        if (targetTrailRecorder != null)
        {
            targetTrailRecorder.Unsubscribe(this);
        }

        followTarget = null;
        targetTrailRecorder = null;
        state = RelationshipState.TargetLost;
        // Retain start/progress for later LostTargetHold; explicit Cancel clears them.
        relationshipVersion++;
        RelationshipChanged?.Invoke();
    }

    internal bool CanBeginFollowRelationship(ShipFollowController target)
    {
        if (!IsMovementEntityValid() || target == null || target == this
            || !target.IsMovementEntityValid() || WouldCreateCycle(target)) return false;
        ShipFollowTrailRecorder recorder = target.GetComponent<ShipFollowTrailRecorder>();
        return recorder != null && recorder.isActiveAndEnabled && recorder.CanSubscribe(this);
    }

    internal bool WouldCreateCycle(ShipFollowController target)
    {
        HashSet<ShipFollowController> visited = new();
        for (ShipFollowController current = target; current != null;
            current = current.FollowTarget)
        {
            if (current == this || !visited.Add(current))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsActive(Behaviour component)
        => component != null && component.isActiveAndEnabled;

    private void Update() => RefreshFollowRelationship();
    private void OnDisable() => CancelFollow();
    private void OnDestroy() => CancelFollow();
}
