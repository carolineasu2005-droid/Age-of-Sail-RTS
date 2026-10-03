using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShipFollowNavigationController : MonoBehaviour
{
    public enum ReplayState
    {
        Inactive, ApproachingTrailStart, CatchUp, Closing, Following, TooCloseHold,
        ReplayingTurn, WaitingForTurnReadiness, LostTargetHold
    }

    [Header("Experimental Trail Navigation")]
    [SerializeField, Min(0.01f)] private float navigationThinkInterval = 0.20f;
    [SerializeField, Min(1f)] private float straightLookAhead = 25f;
    [SerializeField, Min(1f)] private float destinationRetargetThreshold = 10f;
    [SerializeField, Min(0.1f)] private float trailArrivalTolerance = 6f;
    [SerializeField, Min(0.1f)] private float curvatureHeadingThreshold = 10f;
    [SerializeField, Min(0.1f)] private float turnHeadingTolerance = 1f;

    [Header("Experimental Follow Spacing")]
    [SerializeField, Min(1f)] private float desiredFollowGap = 60f;
    [SerializeField, Min(0f)] private float tooCloseGap = 45f;
    [SerializeField, Min(1f)] private float followingBandMaximum = 70f;
    [SerializeField, Min(1f)] private float catchUpGap = 90f;

    private ShipFollowController follow;
    private ShipFollowController subscribedFollow;
    private ShipSailingSpeed speed;
    private ShipDestinationController destination;
    private ShipManeuverPlanner planner;
    private ShipFollowTrailRecorder recorder;
    private int relationshipVersion = -1;
    private int nextTurnSequence;
    private bool entryReached;
    private bool ownsDestination;
    private bool ownsPlanner;
    private int ownedPlannerSequence;
    private bool hasReplayTarget;
    private Vector3 replayTarget;
    private double replayTargetDistance;
    private double cursor;
    private double nextThinkTime;
    private bool turnCommandIssued;
    private int enteredTurnSequence = -1;
    private int executedTurnSequence = -1;
    private ShipManeuverPlanner.ManeuverType issuedTurnManeuver;
    private float commandedTurnHeading;
    private ShipFollowTurnEvent activeTurn;
    private int activeTurnIndex = -1;
    private ReplayState state;
    private ShipFollowSpeedState speedState;
    private float requestedSpeedCap;
    private double alongTrailGap;

    public ReplayState State => state;
    public ShipFollowSpeedState SpeedState => speedState;
    public double TrailCursor => cursor;
    public double FollowerTrailProgress => cursor;
    public double AlongTrailGap => alongTrailGap;
    public bool EntryReached => entryReached;
    public bool HasReplayTarget => hasReplayTarget;
    public Vector3 ReplayTarget => replayTarget;
    public double ReplayTargetDistance => replayTargetDistance;
    public int ActiveTurnIndex => activeTurnIndex;
    public ShipFollowTurnEvent? ActiveTurn => activeTurnIndex >= 0 ? activeTurn : (ShipFollowTurnEvent?)null;
    public float RequestedFollowSpeedCap => requestedSpeedCap;
    public float DesiredFollowGap => desiredFollowGap;

    // Deterministic observation/command boundary. Update uses game time; never trail timestamps.
    public bool Tick(double currentTime)
    {
        ResolveReferences();
        if (!isActiveAndEnabled || double.IsNaN(currentTime) || double.IsInfinity(currentTime))
            return false;
        if (follow == null || speed == null || destination == null || planner == null)
            return false;
        follow.RefreshFollowRelationship();
        if (!follow.HasFollowIntent)
        {
            EndReplay();
            return false;
        }
        if (!follow.IsFollowing)
        {
            HoldLostTarget();
            return true;
        }
        if (relationshipVersion != follow.RelationshipVersion || recorder != follow.TargetTrailRecorder)
            BeginReplay();
        if (currentTime < nextThinkTime) return false;
        nextThinkTime = currentTime + Mathf.Max(0.01f, navigationThinkInterval);
        if (recorder == null || !recorder.IsRecording) return false;

        alongTrailGap = Math.Max(0d, recorder.HeadDistance - cursor);
        ApplyPropulsion();
        if (!entryReached)
        {
            if (ShipFollowReplayMath.HorizontalDistance(transform.position,
                follow.FollowStartSample.WorldPosition) > trailArrivalTolerance)
            {
                state = speedState == ShipFollowSpeedState.TooCloseHold
                    ? ReplayState.TooCloseHold : ReplayState.ApproachingTrailStart;
                if (requestedSpeedCap > 0f)
                    RequestDestination(follow.FollowStartSample.WorldPosition,
                        follow.FollowStartLeaderDistance);
                else HoldStraightMovement();
                return true;
            }
            entryReached = true;
        }

        activeTurnIndex = FindNextTurn();
        double turnBoundary = activeTurnIndex >= 0
            ? Math.Max(follow.FollowStartLeaderDistance, activeTurn.StartDistance)
            : recorder.HeadDistance;
        double projectionLimit = Math.Min(turnBoundary, cursor + straightLookAhead);
        projectionLimit = ShipFollowReplayMath.ClampAtCurvature(recorder.Samples,
            recorder.HeadSample, cursor, projectionLimit, curvatureHeadingThreshold);
        if (!turnCommandIssued && ShipFollowReplayMath.TryProjectForward(recorder.Samples,
            recorder.HeadSample, transform.position, cursor, projectionLimit,
            trailArrivalTolerance, out double progress))
        {
            if (follow.ReportConsumedTrailDistance(progress)) cursor = progress;
        }
        // A progress report can trim old events and change their list indices.
        activeTurnIndex = FindNextTurn();
        alongTrailGap = Math.Max(0d, recorder.HeadDistance - cursor);
        ApplyPropulsion();
        double spacingTarget = Math.Max(follow.FollowStartLeaderDistance,
            recorder.HeadDistance - desiredFollowGap);
        if (activeTurnIndex >= 0)
        {
            turnBoundary = Math.Max(follow.FollowStartLeaderDistance, activeTurn.StartDistance);
            if (turnBoundary < cursor)
            {
                WaitForTurn(); // Invalid history must not request a destination behind consumed progress.
                return true;
            }
            if (!TryGetTurnStartPosition(turnBoundary, out Vector3 turnStart))
            {
                WaitForTurn();
                return true;
            }
            if (turnCommandIssued || enteredTurnSequence == activeTurn.Sequence
                || (spacingTarget >= turnBoundary
                && ShipFollowReplayMath.HorizontalDistance(transform.position,
                    turnStart) <= trailArrivalTolerance))
            {
                ReplayTurn();
                return true;
            }
        }
        if (requestedSpeedCap <= 0f || spacingTarget <= cursor)
        {
            SetSpacingState();
            HoldStraightMovement();
            return true;
        }

        double targetDistance = Math.Min(spacingTarget, cursor + straightLookAhead);
        if (activeTurnIndex >= 0) targetDistance = Math.Min(targetDistance, turnBoundary);
        targetDistance = ShipFollowReplayMath.ClampAtCurvature(recorder.Samples,
            recorder.HeadSample, cursor, targetDistance, curvatureHeadingThreshold);
        if (recorder.TryGetSampleAtDistance(targetDistance, out ShipFollowTrailSample target))
        {
            SetSpacingState();
            RequestDestination(target.WorldPosition, targetDistance);
        }
        return true;
    }

    private void BeginReplay()
    {
        ClearOwnedMovement();
        recorder = follow.TargetTrailRecorder;
        relationshipVersion = follow.RelationshipVersion;
        cursor = follow.ConsumedTrailDistance;
        nextTurnSequence = follow.FollowStartTurnSequence;
        entryReached = cursor > follow.FollowStartLeaderDistance;
        turnCommandIssued = false;
        enteredTurnSequence = -1;
        executedTurnSequence = -1;
        activeTurnIndex = -1;
        nextThinkTime = double.NegativeInfinity;
        // Acquire individual Movement intent once; no formation or player input command.
        destination.ClearDestination();
        ownedPlannerSequence = planner.CommandSequence;
        ownsPlanner = true;
        state = ReplayState.ApproachingTrailStart;
        SetSpeedCap(0f);
    }

    private void ApplyPropulsion()
    {
        double gap = alongTrailGap;
        float separation = ShipFollowReplayMath.HorizontalDistance(transform.position,
            follow.FollowTarget.transform.position);
        if (!entryReached) gap = separation;
        float close = Mathf.Max(0f, tooCloseGap);
        float normal = Mathf.Max(close, followingBandMaximum);
        float catchUp = Mathf.Max(normal + 0.01f, catchUpGap);
        float cap = ShipFollowReplayMath.EvaluateSpeed(gap, speed.AvailableTargetSpeed,
            follow.FollowTarget.GetComponent<ShipSailingSpeed>().CourseSpeed,
            close, normal, catchUp, out speedState);
        if ((!entryReached || recorder.HeadDistance - follow.FollowStartLeaderDistance < desiredFollowGap)
            && separation < close)
        {
            speedState = ShipFollowSpeedState.TooCloseHold;
            cap = 0f;
        }
        SetSpeedCap(cap);
    }

    private int FindNextTurn()
    {
        for (int i = 0; i < recorder.TurnEvents.Count; i++)
        {
            ShipFollowTurnEvent turn = recorder.TurnEvents[i];
            if (turn.Sequence < nextTurnSequence) continue;
            activeTurn = turn;
            return i;
        }
        return -1;
    }

    private bool TryGetTurnStartPosition(double distance, out Vector3 position)
    {
        position = follow.FollowStartSample.WorldPosition;
        if (distance == follow.FollowStartLeaderDistance) return true;
        if (!recorder.TryGetSampleAtDistance(distance, out ShipFollowTrailSample sample)) return false;
        position = sample.WorldPosition;
        return true;
    }

    private void ReplayTurn()
    {
        enteredTurnSequence = activeTurn.Sequence;
        if (turnCommandIssued)
        {
            if (planner.CommandSequence != ownedPlannerSequence)
            {
                turnCommandIssued = false;
                WaitForTurn();
                return;
            }
            if (planner.IsActive)
            {
                state = ReplayState.ReplayingTurn;
                return;
            }
            if (!TurnOwnerCompleted())
            {
                turnCommandIssued = false;
                WaitForTurn();
                return;
            }
            if (Mathf.Abs(Mathf.DeltaAngle(commandedTurnHeading, activeTurn.EndingHeading)) <= turnHeadingTolerance)
            {
                if (!activeTurn.IsComplete)
                {
                    state = ReplayState.ReplayingTurn;
                    return;
                }
                if (!TryConsumeCompletedTurn() && follow.IsFollowing) WaitForTurn();
                return; // Resume post-event geometric correction on the next think.
            }
            turnCommandIssued = false; // Open recorded event extended after our previous execution.
        }
        if (speedState == ShipFollowSpeedState.TooCloseHold)
        {
            state = ReplayState.TooCloseHold;
            HoldStraightMovement();
            return;
        }
        // Re-enable can resume a turn this owner already physically completed before cleanup.
        if (executedTurnSequence == activeTurn.Sequence && TurnOwnerCompleted()
            && Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, activeTurn.EndingHeading)) <= turnHeadingTolerance)
        {
            if (activeTurn.IsComplete)
            {
                if (!TryConsumeCompletedTurn() && follow.IsFollowing) WaitForTurn();
            }
            else state = ReplayState.ReplayingTurn;
            return;
        }
        if (planner.IsActive && planner.CurrentManeuver != ShipManeuverPlanner.ManeuverType.NormalTurn)
        {
            state = ReplayState.WaitingForTurnReadiness;
            return;
        }
        if (!planner.TryPreviewDirectedHeading(activeTurn.EndingHeading, activeTurn.Direction,
            out ShipManeuverPlanner.ManeuverType maneuver) || !IsTurnReady(maneuver))
        {
            WaitForTurn();
            return;
        }
        ClearOwnedMovement();
        // Classification is the follower planner's decision under its current wind and yaw.
        planner.ExecuteHeadingCommand(activeTurn.EndingHeading, activeTurn.Direction);
        commandedTurnHeading = activeTurn.EndingHeading;
        ownedPlannerSequence = planner.CommandSequence;
        ownsPlanner = true;
        turnCommandIssued = true;
        executedTurnSequence = activeTurn.Sequence;
        issuedTurnManeuver = maneuver;
        state = ReplayState.ReplayingTurn;
    }

    private bool TryConsumeCompletedTurn()
    {
        // A successfully reproduced turn consumes its recorded interval, not physical Root travel.
        // Reporting can trim/reindex history; retain the stable event value until acceptance.
        ShipFollowTurnEvent completedTurn = activeTurn;
        double consumed = Math.Max(cursor, completedTurn.EndDistance);
        if (!follow.ReportConsumedTrailDistance(consumed)) return false;

        cursor = consumed;
        nextTurnSequence = completedTurn.Sequence + 1;
        activeTurnIndex = -1;
        turnCommandIssued = false;
        alongTrailGap = Math.Max(0d, recorder.HeadDistance - cursor);
        ApplyPropulsion();
        SetSpacingState();
        return true;
    }

    private bool TurnOwnerCompleted()
    {
        // Cancellation preserves CommandSequence but clears CurrentManeuver; it is not completion.
        if (planner.CommandSequence != ownedPlannerSequence
            || planner.CurrentManeuver != issuedTurnManeuver) return false;
        if (issuedTurnManeuver == ShipManeuverPlanner.ManeuverType.NormalTurn)
        {
            ShipHeadingController owner = GetComponent<ShipHeadingController>();
            return owner != null && owner.HasCompletedCommand
                && Mathf.Abs(Mathf.DeltaAngle(owner.TargetHeading, commandedTurnHeading)) <= turnHeadingTolerance;
        }
        if (issuedTurnManeuver == ShipManeuverPlanner.ManeuverType.Tack)
        {
            ShipTacking owner = GetComponent<ShipTacking>();
            return owner != null && owner.IsCompleted;
        }
        if (issuedTurnManeuver == ShipManeuverPlanner.ManeuverType.Wear)
        {
            ShipWearing owner = GetComponent<ShipWearing>();
            return owner != null && owner.IsCompleted;
        }
        return false;
    }

    private bool IsTurnReady(ShipManeuverPlanner.ManeuverType maneuver)
    {
        if (!planner.CanExecuteCoordinatedManeuver(maneuver)) return false;
        if (maneuver == ShipManeuverPlanner.ManeuverType.NormalTurn) return true;
        if (maneuver == ShipManeuverPlanner.ManeuverType.Tack)
        {
            ShipTacking owner = GetComponent<ShipTacking>();
            return owner != null && owner.isActiveAndEnabled;
        }
        if (maneuver == ShipManeuverPlanner.ManeuverType.Wear)
        {
            ShipWearing owner = GetComponent<ShipWearing>();
            return owner != null && owner.isActiveAndEnabled;
        }
        return false;
    }

    private void WaitForTurn()
    {
        ClearOwnedMovement();
        SetSpeedCap(0f);
        state = ReplayState.WaitingForTurnReadiness;
    }

    private void RequestDestination(Vector3 target, double distance)
    {
        bool transition = !hasReplayTarget;
        bool moved = hasReplayTarget && ShipFollowReplayMath.HorizontalDistance(replayTarget, target)
            >= destinationRetargetThreshold;
        bool arrived = hasReplayTarget && ShipFollowReplayMath.HorizontalDistance(transform.position,
            replayTarget) <= trailArrivalTolerance;
        bool blocked = destination.CurrentNavigationMode == ShipDestinationController.NavigationMode.Blocked;
        if (!transition && !moved && !arrived && !blocked) return;
        if (planner.IsActive && planner.CurrentManeuver != ShipManeuverPlanner.ManeuverType.NormalTurn)
            return; // Preserve an existing Tack/Wear owner instead of repeatedly resetting it.
        if (ShipFollowReplayMath.HorizontalDistance(transform.position, target) <= trailArrivalTolerance)
            return;
        destination.SetDestination(target, ShipDestinationController.TurnSelectionMode.Auto,
            WindNavigationAssistMode.Manual);
        replayTarget = target;
        replayTargetDistance = distance;
        hasReplayTarget = true;
        ownsDestination = true;
        ownsPlanner = true;
        ownedPlannerSequence = planner.CommandSequence;
    }

    private void HoldStraightMovement()
    {
        if (!planner.IsActive || planner.CurrentManeuver == ShipManeuverPlanner.ManeuverType.NormalTurn)
            ClearOwnedMovement(); // An active special maneuver retains its existing minimum authority.
    }

    private void ClearOwnedMovement()
    {
        if (ownsDestination && destination != null)
        {
            // Destination's own heading replans may increment the planner sequence.
            destination.ClearDestination();
        }
        else if (planner != null && ownsPlanner && planner.CommandSequence == ownedPlannerSequence
            && planner.IsActive) planner.CancelCurrentManeuver();
        ownsDestination = ownsPlanner = hasReplayTarget = false;
    }

    private void HoldLostTarget()
    {
        ClearOwnedMovement();
        SetSpeedCap(0f);
        activeTurnIndex = -1;
        state = ReplayState.LostTargetHold;
    }

    private void EndReplay()
    {
        ClearOwnedMovement();
        if (speed != null) speed.ClearFollowMaximumTargetSpeed();
        recorder = null;
        relationshipVersion = -1;
        activeTurnIndex = -1;
        turnCommandIssued = false;
        requestedSpeedCap = 0f;
        state = ReplayState.Inactive;
    }

    private void SetSpeedCap(float cap)
    {
        requestedSpeedCap = cap;
        speed.SetFollowMaximumTargetSpeed(cap);
    }

    private void SetSpacingState()
    {
        state = speedState switch
        {
            ShipFollowSpeedState.CatchUp => ReplayState.CatchUp,
            ShipFollowSpeedState.Closing => ReplayState.Closing,
            ShipFollowSpeedState.Following => ReplayState.Following,
            _ => ReplayState.TooCloseHold
        };
    }

    private void HandleRelationshipChanged()
    {
        if (!isActiveAndEnabled || speed == null || destination == null || planner == null) return;
        if (!follow.HasFollowIntent) EndReplay();
        else if (!follow.IsFollowing) HoldLostTarget();
        else BeginReplay();
    }

    private void ResolveReferences()
    {
        if (follow == null) follow = GetComponent<ShipFollowController>();
        if (speed == null) speed = GetComponent<ShipSailingSpeed>();
        if (destination == null) destination = GetComponent<ShipDestinationController>();
        if (planner == null) planner = GetComponent<ShipManeuverPlanner>();
        if (subscribedFollow != follow)
        {
            if (subscribedFollow != null) subscribedFollow.RelationshipChanged -= HandleRelationshipChanged;
            subscribedFollow = follow;
            if (subscribedFollow != null) subscribedFollow.RelationshipChanged += HandleRelationshipChanged;
        }
    }

    private void Awake() => ResolveReferences();
    private void OnEnable() => ResolveReferences();
    private void Update() => Tick(Time.timeAsDouble);
    private void OnDisable()
    {
        ClearOwnedMovement();
        if (speed != null) speed.ClearFollowMaximumTargetSpeed();
        requestedSpeedCap = 0f;
        turnCommandIssued = false;
        nextThinkTime = double.NegativeInfinity;
        state = ReplayState.Inactive;
        if (subscribedFollow != null) subscribedFollow.RelationshipChanged -= HandleRelationshipChanged;
        subscribedFollow = null;
    }
}
