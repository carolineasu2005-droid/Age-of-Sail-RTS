using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class ShipFollowReplayTests
{
    private readonly List<GameObject> created = new();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject item in created)
            if (item != null) Object.DestroyImmediate(item);
        created.Clear();
    }

    [TestCase(100d, 2f, 5f, 2f, ShipFollowSpeedState.CatchUp)]
    [TestCase(100d, 6f, 2f, 6f, ShipFollowSpeedState.CatchUp)]
    [TestCase(80d, 6f, 2f, 4f, ShipFollowSpeedState.Closing)]
    [TestCase(70d, 6f, 2f, 2f, ShipFollowSpeedState.Following)]
    [TestCase(60d, 2f, 5f, 2f, ShipFollowSpeedState.Following)]
    [TestCase(60d, 6f, 2f, 2f, ShipFollowSpeedState.Following)]
    [TestCase(44d, 6f, 2f, 0f, ShipFollowSpeedState.TooCloseHold)]
    public void SpeedPolicy_UsesOwnCapabilityAndFrozenBands(double gap, float capability,
        float leaderSpeed, float expected, ShipFollowSpeedState expectedState)
    {
        float cap = ShipFollowReplayMath.EvaluateSpeed(gap, capability, leaderSpeed,
            45f, 70f, 90f, out var state);
        Assert.That(cap, Is.EqualTo(expected).Within(0.0001f));
        Assert.That(cap, Is.LessThanOrEqualTo(capability));
        Assert.That(state, Is.EqualTo(expectedState));
    }

    [Test]
    public void Projection_ClipsBehindCursorAndDoesNotJumpToNearbyCrossingBranch()
    {
        Pair pair = CreatePair(Vector3.zero);
        Record(pair.Leader, new Vector3(0f, 0f, 20f), 0f);
        Record(pair.Leader, new Vector3(20f, 0f, 20f), 0f);
        Record(pair.Leader, new Vector3(20f, 0f, 0f), 0f);
        Record(pair.Leader, Vector3.zero, 0f);
        var recorder = pair.Leader.Recorder;
        Assert.That(ShipFollowReplayMath.TryProjectForward(recorder.Samples, recorder.HeadSample,
            new Vector3(0f, 0f, 5f), 10d, 80d, 6f, out double projected), Is.True);
        Assert.That(projected, Is.EqualTo(10d));
        Assert.That(ShipFollowReplayMath.TryProjectForward(recorder.Samples, recorder.HeadSample,
            Vector3.zero, 0d, 80d, 6f, out double crossing), Is.True);
        Assert.That(crossing, Is.Zero); // The head is also at the origin, 80 m later.
    }

    [Test]
    public void Projection_CannotLeapBeyondLocalForwardWindow()
    {
        Pair pair = CreatePair(Vector3.zero);
        RecordStraight(pair.Leader, 100);
        Assert.That(ShipFollowReplayMath.TryProjectForward(pair.Leader.Recorder.Samples,
            pair.Leader.Recorder.HeadSample, new Vector3(0f, 0f, 100f),
            0d, 25d, 6f, out double projected), Is.False);
        Assert.That(projected, Is.Zero);
    }

    [Test]
    public void Cursor_AndReportedProgressNeverRegressAfterDrift()
    {
        Pair pair = CreatePair(Vector3.zero);
        RecordStraight(pair.Leader, 200);
        pair.Navigation.Tick(0d);
        pair.Follower.Root.transform.position = new Vector3(0f, 0f, 20f);
        pair.Navigation.Tick(1d);
        double cursor = pair.Navigation.TrailCursor;
        Assert.That(cursor, Is.GreaterThan(0d));
        pair.Follower.Root.transform.position = Vector3.zero;
        pair.Navigation.Tick(2d);
        Assert.That(pair.Navigation.TrailCursor, Is.EqualTo(cursor));
        Assert.That(pair.Follower.Follow.ConsumedTrailDistance, Is.EqualTo(cursor));
        Assert.That(pair.Leader.Recorder.TryGetFollowerProgress(pair.Follower.Follow,
            out _, out double reported), Is.True);
        Assert.That(reported, Is.EqualTo(cursor));
    }

    [Test]
    public void Entry_TargetsFrozenStartAndNeverProjectsToHeadBeforeArrival()
    {
        Pair pair = CreatePair(new Vector3(0f, 0f, -150f));
        RecordStraight(pair.Leader, 200);
        pair.Navigation.Tick(0d);
        Assert.That(pair.Navigation.State,
            Is.EqualTo(ShipFollowNavigationController.ReplayState.ApproachingTrailStart));
        Assert.That(pair.Navigation.EntryReached, Is.False);
        Assert.That(pair.Navigation.ReplayTarget, Is.EqualTo(Vector3.zero));
        Assert.That(pair.Navigation.TrailCursor, Is.Zero);
    }

    [Test]
    public void LaterSubscriber_FrozenEntryAndTurnMarkerExcludeEarlierHistory()
    {
        Ship leader = CreateShip(Vector3.zero);
        Ship earlier = CreateShip(new Vector3(0f, 0f, -100f));
        Assert.That(earlier.Follow.TryBeginFollowRelationship(leader.Follow), Is.True);
        Record(leader, new Vector3(0f, 0f, 10f), 10f);
        Record(leader, new Vector3(0f, 0f, 20f), 10f);
        leader.Root.transform.position = new Vector3(0f, 0f, 22f);
        Ship later = CreateShip(new Vector3(0f, 0f, -100f));
        ShipFollowNavigationController navigation = later.Root.AddComponent<ShipFollowNavigationController>();
        Assert.That(later.Follow.TryBeginFollowRelationship(leader.Follow), Is.True);
        Record(leader, new Vector3(100f, 0f, 100f), 10f);
        navigation.Tick(0d);
        Assert.That(later.Follow.FollowStartLeaderDistance, Is.EqualTo(22d));
        Assert.That(navigation.ReplayTarget, Is.EqualTo(new Vector3(0f, 0f, 22f)));
        Assert.That(navigation.TrailCursor, Is.EqualTo(22d));
        Assert.That(later.Follow.FollowStartTurnSequence, Is.EqualTo(1));
    }

    [Test]
    public void ShortStationaryTrail_UsesPhysicalSafetyHoldWithoutPlayerStop()
    {
        Pair pair = CreatePair(new Vector3(0f, 0f, -30f));
        SetPrivate(pair.Leader.Speed, "courseSpeed", 0f);
        pair.Navigation.Tick(0d);
        Assert.That(pair.Navigation.State, Is.EqualTo(ShipFollowNavigationController.ReplayState.TooCloseHold));
        Assert.That(pair.Follower.Speed.FollowSpeedCapActive, Is.True);
        Assert.That(pair.Follower.Speed.FollowSpeedCap, Is.Zero);
        Assert.That(pair.Follower.Speed.IsPlayerStopped, Is.False);
        Assert.That(pair.Follower.Destination.HasDestination, Is.False);
        Assert.That(pair.Navigation.TrailCursor, Is.Zero);
        Assert.That(pair.Follower.Follow.IsFollowing, Is.True);
    }

    [Test]
    public void StraightReplay_UsesLookAheadAndRetargetDeadbandInsteadOfPerSampleCommands()
    {
        Pair pair = CreatePair(Vector3.zero);
        RecordStraight(pair.Leader, 200, 4);
        pair.Navigation.Tick(0d);
        Assert.That(pair.Navigation.ReplayTargetDistance, Is.EqualTo(25d));
        int sequence = pair.Follower.Planner.CommandSequence;
        pair.Follower.Root.transform.position = new Vector3(0f, 0f, 4f);
        pair.Navigation.Tick(1d);
        Assert.That(pair.Follower.Planner.CommandSequence, Is.EqualTo(sequence));
        pair.Navigation.Tick(1.1d); // Cadence also prevents a second think.
        Assert.That(pair.Follower.Planner.CommandSequence, Is.EqualTo(sequence));
        pair.Follower.Root.transform.position = new Vector3(0f, 0f, 20f);
        pair.Navigation.Tick(2d);
        Assert.That(pair.Navigation.ReplayTargetDistance, Is.GreaterThan(25d));
        Assert.That(pair.Follower.Planner.CommandSequence, Is.EqualTo(sequence + 1));
    }

    [Test]
    public void StraightReplay_ClampsTargetAndCursorBeforeUnconsumedTurn()
    {
        Pair pair = CreatePair(Vector3.zero);
        Record(pair.Leader, new Vector3(0f, 0f, 20f), 0f);
        Record(pair.Leader, new Vector3(0f, 0f, 24f), 15f);
        Record(pair.Leader, new Vector3(0f, 0f, 200f), 15f);
        pair.Navigation.Tick(0d);
        Assert.That(pair.Navigation.ActiveTurn.Value.StartDistance, Is.EqualTo(20d));
        Assert.That(pair.Navigation.ReplayTargetDistance, Is.EqualTo(20d));
        pair.Follower.Root.transform.position = new Vector3(0f, 0f, 100f);
        pair.Navigation.Tick(1d);
        Assert.That(pair.Navigation.TrailCursor, Is.LessThanOrEqualTo(20d));
        Assert.That(pair.Navigation.ActiveTurn, Is.Not.Null);
    }

    [Test]
    public void CurvatureWithoutYawMetadata_ClampsAtPhysicalCorner()
    {
        Pair pair = CreatePair(Vector3.zero);
        Record(pair.Leader, new Vector3(0f, 0f, 10f), 0f);
        Record(pair.Leader, new Vector3(10f, 0f, 10f), 0f);
        Record(pair.Leader, new Vector3(20f, 0f, 10f), 0f);
        Assert.That(ShipFollowReplayMath.ClampAtCurvature(pair.Leader.Recorder.Samples,
            pair.Leader.Recorder.HeadSample, 0d, 25d, 10f), Is.EqualTo(10d));
    }

    [TestCase(45f, 90f, ShipManeuverPlanner.ManeuverType.NormalTurn, TurnDirection.Clockwise)]
    [TestCase(180f, 90f, ShipManeuverPlanner.ManeuverType.Tack, TurnDirection.Clockwise)]
    [TestCase(180f, 270f, ShipManeuverPlanner.ManeuverType.Wear, TurnDirection.Clockwise)]
    [TestCase(315f, 90f, ShipManeuverPlanner.ManeuverType.NormalTurn, TurnDirection.CounterClockwise)]
    public void RecordedDirection_UsesFollowerCurrentWindAndPlannerClassification(float end,
        float windFrom, ShipManeuverPlanner.ManeuverType expected, TurnDirection direction)
    {
        Pair pair = CreatePair(Vector3.zero);
        RecordTurn(pair.Leader, end, direction);
        pair.Follower.Wind.windFromDegrees = windFrom;
        SetPrivate(pair.Follower.Speed, "relativeWindAngleSigned", Mathf.DeltaAngle(0f, windFrom));
        pair.Navigation.Tick(0d);
        Assert.That(pair.Navigation.State, Is.EqualTo(ShipFollowNavigationController.ReplayState.ReplayingTurn));
        Assert.That(pair.Follower.Planner.PlannedTurnDirection, Is.EqualTo(direction));
        Assert.That(pair.Follower.Planner.CurrentManeuver, Is.EqualTo(expected));
        Assert.That(pair.Follower.Planner.CommandSequence, Is.EqualTo(1));
        AssertHeading(end, pair.Follower.Planner.TargetHeading);
        Assert.That(pair.Follower.Destination.HasDestination, Is.False);
    }

    [Test]
    public void ComplexTurn_RemainsBlockedAtSameEventAndRetriesWithChangedWind()
    {
        Pair pair = CreatePair(Vector3.zero);
        RecordTurn(pair.Leader, 350f, TurnDirection.Clockwise);
        pair.Navigation.Tick(0d);
        Assert.That(pair.Navigation.State, Is.EqualTo(ShipFollowNavigationController.ReplayState.WaitingForTurnReadiness));
        Assert.That(pair.Follower.Planner.CommandSequence, Is.Zero);
        Assert.That(pair.Follower.Speed.FollowSpeedCap, Is.Zero);
        int sequence = pair.Navigation.ActiveTurn.Value.Sequence;
        pair.Navigation.Tick(1d);
        Assert.That(pair.Navigation.ActiveTurn.Value.Sequence, Is.EqualTo(sequence));
        Assert.That(pair.Navigation.TrailCursor, Is.Zero);
        // 0 -> 350 still crosses both boundaries for most wind choices; 355 puts wind outside it.
        pair.Follower.Wind.windFromDegrees = 355f;
        SetPrivate(pair.Follower.Speed, "relativeWindAngleSigned", -5f);
        pair.Navigation.Tick(2d);
        Assert.That(pair.Navigation.State, Is.EqualTo(ShipFollowNavigationController.ReplayState.ReplayingTurn));
        Assert.That(pair.Navigation.ActiveTurn.Value.Sequence, Is.EqualTo(sequence));
    }

    [Test]
    public void UnavailableTurnOwner_IsHeldAndRetriedWithoutSkipping()
    {
        Pair pair = CreatePair(Vector3.zero);
        RecordTurn(pair.Leader, 180f, TurnDirection.Clockwise);
        pair.Follower.Tacking.enabled = false;
        pair.Navigation.Tick(0d);
        Assert.That(pair.Navigation.State, Is.EqualTo(ShipFollowNavigationController.ReplayState.WaitingForTurnReadiness));
        Assert.That(pair.Navigation.ActiveTurn, Is.Not.Null);
        Assert.That(pair.Follower.Planner.CommandSequence, Is.Zero);
        // Natural drift must not abandon a turn already reached while its owner was unavailable.
        pair.Follower.Root.transform.position = new Vector3(0f, 0f, 12f);
        pair.Follower.Tacking.enabled = true;
        pair.Navigation.Tick(1d);
        Assert.That(pair.Follower.Planner.CurrentManeuver, Is.EqualTo(ShipManeuverPlanner.ManeuverType.Tack));
    }

    [TestCase(TurnDirection.Clockwise, 45f)]
    [TestCase(TurnDirection.Clockwise, 47f)]
    [TestCase(TurnDirection.CounterClockwise, 315f)]
    [TestCase(TurnDirection.CounterClockwise, 313f)]
    public void RecordedTurnCompletion_ConsumesWholeEventInterval(
        TurnDirection direction, float completedYaw)
    {
        Pair pair = CreateTurnIntervalPair(direction);
        ShipFollowTurnEvent turn = pair.Navigation.ActiveTurn.Value;
        Assert.That(turn.StartDistance, Is.EqualTo(100d));
        Assert.That(turn.EndDistance, Is.EqualTo(140d));
        Assert.That(pair.Navigation.TrailCursor, Is.EqualTo(100d));
        CompleteNormalTurn(pair.Follower, completedYaw);
        SetPrivate(pair.Follower.Speed, "currentSpeed", 2.5f);
        Vector3 position = pair.Follower.Root.transform.position;
        Quaternion rotation = pair.Follower.Root.transform.rotation;

        pair.Navigation.Tick(6d);

        Assert.That(pair.Navigation.ActiveTurn, Is.Null);
        AssertConsumedProgress(pair, turn.EndDistance);
        Assert.That(pair.Follower.Root.transform.position, Is.EqualTo(position));
        Assert.That(pair.Follower.Root.transform.rotation, Is.EqualTo(rotation));
        Assert.That(pair.Follower.Speed.CurrentSpeed, Is.EqualTo(2.5f));
        Assert.That(pair.Navigation.HasReplayTarget, Is.False);

        pair.Navigation.Tick(7d);
        AssertConsumedProgress(pair, turn.EndDistance);
        Assert.That(pair.Follower.Destination.HasDestination, Is.True);
        Assert.That(pair.Navigation.ReplayTargetDistance, Is.GreaterThan(turn.EndDistance));
        Assert.That(pair.Leader.Recorder.TryGetSampleAtDistance(pair.Navigation.ReplayTargetDistance,
            out ShipFollowTrailSample target), Is.True);
        Assert.That(pair.Navigation.ReplayTarget, Is.EqualTo(target.WorldPosition));
        // Returning spatially to old geometry must not pull semantic consumption backwards.
        pair.Follower.Root.transform.position = Vector3.zero;
        pair.Navigation.Tick(8d);
        AssertConsumedProgress(pair, turn.EndDistance);
        Assert.That(pair.Navigation.ReplayTargetDistance, Is.GreaterThanOrEqualTo(turn.EndDistance));
    }

    [Test]
    public void CancelledNormalTurn_DoesNotConsumeIntervalAndRetriesSameEvent()
    {
        Pair pair = CreateTurnIntervalPair(TurnDirection.Clockwise);
        ShipFollowTurnEvent turn = pair.Navigation.ActiveTurn.Value;
        pair.Follower.Planner.CancelCurrentManeuver();

        pair.Navigation.Tick(6d);

        Assert.That(pair.Navigation.State,
            Is.EqualTo(ShipFollowNavigationController.ReplayState.WaitingForTurnReadiness));
        Assert.That(pair.Navigation.ActiveTurn.Value.Sequence, Is.EqualTo(turn.Sequence));
        AssertConsumedProgress(pair, turn.StartDistance);
        pair.Navigation.Tick(7d);
        Assert.That(pair.Navigation.ActiveTurn.Value.Sequence, Is.EqualTo(turn.Sequence));
        Assert.That(pair.Follower.Planner.IsActive, Is.True);
        Assert.That(pair.Follower.Planner.CurrentManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.NormalTurn));
        AssertConsumedProgress(pair, turn.StartDistance);
        CompleteNormalTurn(pair.Follower, turn.EndingHeading);
        pair.Navigation.Tick(8d);
        Assert.That(pair.Navigation.ActiveTurn, Is.Null);
        AssertConsumedProgress(pair, turn.EndDistance);
    }

    [Test]
    public void CancelledHeadingOwner_DoesNotConsumeIntervalAndRetriesSameEvent()
    {
        Pair pair = CreateTurnIntervalPair(TurnDirection.Clockwise);
        ShipFollowTurnEvent turn = pair.Navigation.ActiveTurn.Value;
        pair.Follower.Heading.CancelHeadingCommand();
        InvokePrivate(pair.Follower.Planner, "Update");

        pair.Navigation.Tick(6d);

        Assert.That(pair.Navigation.State,
            Is.EqualTo(ShipFollowNavigationController.ReplayState.WaitingForTurnReadiness));
        Assert.That(pair.Navigation.ActiveTurn.Value.Sequence, Is.EqualTo(turn.Sequence));
        AssertConsumedProgress(pair, turn.StartDistance);
        pair.Navigation.Tick(7d);
        Assert.That(pair.Follower.Heading.IsActive, Is.True);
        Assert.That(pair.Navigation.ActiveTurn.Value.Sequence, Is.EqualTo(turn.Sequence));
        AssertConsumedProgress(pair, turn.StartDistance);
        CompleteNormalTurn(pair.Follower, turn.EndingHeading);
        pair.Navigation.Tick(8d);
        Assert.That(pair.Navigation.ActiveTurn, Is.Null);
        AssertConsumedProgress(pair, turn.EndDistance);
    }

    [Test]
    public void ReplacedHeadingOwner_CompletedDifferentEndpointDoesNotConsumeRecordedEvent()
    {
        Pair pair = CreateTurnIntervalPair(TurnDirection.Clockwise);
        ShipFollowTurnEvent turn = pair.Navigation.ActiveTurn.Value;
        int commandSequence = pair.Follower.Planner.CommandSequence;
        pair.Follower.Heading.SetTargetHeading(15f, TurnDirection.Clockwise);
        CompleteNormalTurn(pair.Follower, 15f);
        Assert.That(pair.Follower.Planner.CommandSequence, Is.EqualTo(commandSequence));
        Assert.That(pair.Follower.Planner.CurrentManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.NormalTurn));

        pair.Navigation.Tick(6d);

        Assert.That(pair.Navigation.State,
            Is.EqualTo(ShipFollowNavigationController.ReplayState.WaitingForTurnReadiness));
        Assert.That(pair.Navigation.ActiveTurn.Value.Sequence, Is.EqualTo(turn.Sequence));
        AssertConsumedProgress(pair, turn.StartDistance);
        pair.Navigation.Tick(7d);
        AssertHeading(turn.EndingHeading, pair.Follower.Heading.TargetHeading);
        Assert.That(pair.Follower.Heading.IsActive, Is.True);
        AssertConsumedProgress(pair, turn.StartDistance);
        CompleteNormalTurn(pair.Follower, turn.EndingHeading);
        pair.Navigation.Tick(8d);
        Assert.That(pair.Navigation.ActiveTurn, Is.Null);
        AssertConsumedProgress(pair, turn.EndDistance);
    }

    [Test]
    public void FailedTack_DoesNotConsumeIntervalAndRetriesSameEvent()
    {
        Pair pair = CreatePair(Vector3.zero);
        RecordTurn(pair.Leader, 180f, TurnDirection.Clockwise);
        // Planner can classify a Tack whose owner refuses its no-go starting wind state.
        SetPrivate(pair.Follower.Speed, "relativeWindAngleSigned", 0f);
        pair.Navigation.Tick(0d);
        ShipFollowTurnEvent turn = pair.Navigation.ActiveTurn.Value;
        Assert.That(pair.Follower.Tacking.HasFailed, Is.True);

        pair.Navigation.Tick(1d);

        Assert.That(pair.Navigation.State,
            Is.EqualTo(ShipFollowNavigationController.ReplayState.WaitingForTurnReadiness));
        Assert.That(pair.Navigation.ActiveTurn.Value.Sequence, Is.EqualTo(turn.Sequence));
        AssertConsumedProgress(pair, turn.StartDistance);
        SetPrivate(pair.Follower.Speed, "relativeWindAngleSigned", 90f);
        pair.Navigation.Tick(2d);
        Assert.That(pair.Navigation.ActiveTurn.Value.Sequence, Is.EqualTo(turn.Sequence));
        Assert.That(pair.Follower.Tacking.IsActive, Is.True);
        AssertConsumedProgress(pair, turn.StartDistance);
    }

    [Test]
    public void OpenExtendedTurn_ConsumesLatestIntervalOnlyAfterFinalCompletion()
    {
        Pair pair = CreatePair(Vector3.zero);
        Record(pair.Leader, new Vector3(0f, 0f, 100f), 45f);
        pair.Navigation.Tick(0d);
        int sequence = pair.Navigation.ActiveTurn.Value.Sequence;
        CompleteNormalTurn(pair.Follower, 45f);
        pair.Navigation.Tick(1d);
        Assert.That(pair.Navigation.ActiveTurn.Value.Sequence, Is.EqualTo(sequence));
        Assert.That(pair.Navigation.ActiveTurn.Value.IsComplete, Is.False);
        AssertConsumedProgress(pair, 0d);

        Record(pair.Leader, new Vector3(0f, 0f, 140f), 90f);
        pair.Navigation.Tick(2d);
        Assert.That(pair.Navigation.ActiveTurn.Value.Sequence, Is.EqualTo(sequence));
        Assert.That(pair.Navigation.ActiveTurn.Value.EndDistance, Is.EqualTo(140d));
        Assert.That(pair.Follower.Heading.IsActive, Is.True);
        AssertHeading(90f, pair.Follower.Planner.TargetHeading);
        AssertConsumedProgress(pair, 0d);
        CompleteNormalTurn(pair.Follower, 90f);
        pair.Navigation.Tick(3d);
        Assert.That(pair.Navigation.ActiveTurn.Value.Sequence, Is.EqualTo(sequence));
        AssertConsumedProgress(pair, 0d);

        Record(pair.Leader, new Vector3(0f, 0f, 240f), 90f);
        pair.Navigation.Tick(4d);
        Assert.That(pair.Navigation.ActiveTurn, Is.Null);
        AssertConsumedProgress(pair, 140d);
        pair.Navigation.Tick(5d);
        Assert.That(pair.Navigation.ReplayTargetDistance, Is.GreaterThan(140d));
    }

    [Test]
    public void ReenabledNavigation_ConsumesTurnCompletedBeforeCleanup()
    {
        Pair pair = CreateTurnIntervalPair(TurnDirection.Clockwise);
        ShipFollowTurnEvent turn = pair.Navigation.ActiveTurn.Value;
        CompleteNormalTurn(pair.Follower, turn.EndingHeading);
        pair.Navigation.enabled = false;
        AssertConsumedProgress(pair, turn.StartDistance);
        pair.Navigation.enabled = true;

        pair.Navigation.Tick(6d);

        Assert.That(pair.Navigation.ActiveTurn, Is.Null);
        AssertConsumedProgress(pair, turn.EndDistance);
    }

    [Test]
    public void ConsumedProgress_TrimsOldTurnWithoutChangingNextEventIdentity()
    {
        Pair pair = CreatePair(Vector3.zero);
        Record(pair.Leader, new Vector3(0f, 0f, 4f), 30f);
        Record(pair.Leader, new Vector3(0f, 0f, 20f), 30f);
        Record(pair.Leader, new Vector3(0f, 0f, 100f), 30f);
        Record(pair.Leader, new Vector3(0f, 0f, 104f), 45f);
        Record(pair.Leader, new Vector3(0f, 0f, 200f), 45f);
        pair.Navigation.Tick(0d);
        pair.Follower.Root.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
        InvokePrivate(pair.Follower.Heading, "Update");
        InvokePrivate(pair.Follower.Planner, "Update");
        pair.Navigation.Tick(1d);
        for (int step = 1; step <= 3; step++)
        {
            pair.Follower.Root.transform.position = new Vector3(0f, 0f, step * 20f);
            pair.Navigation.Tick(step + 1d);
        }
        Assert.That(pair.Leader.Recorder.TurnEvents.Count, Is.EqualTo(1));
        Assert.That(pair.Navigation.ActiveTurnIndex, Is.Zero);
        Assert.That(pair.Navigation.ActiveTurn.Value.Sequence, Is.EqualTo(1));
        Assert.That(pair.Navigation.ActiveTurn.Value.StartDistance, Is.EqualTo(100d));
        // Vector projection uses float fractions; 10 micrometers remains below any replay tolerance.
        Assert.That(pair.Navigation.TrailCursor, Is.EqualTo(60d).Within(0.00001d));
        Assert.That(pair.Follower.Follow.ConsumedTrailDistance, Is.EqualTo(pair.Navigation.TrailCursor));
        Assert.That(pair.Leader.Recorder.Samples[0].CumulativeDistance, Is.GreaterThan(4d));
    }

    [Test]
    public void LeaderStopAndResume_PreservesRelationshipAndAutomaticallyRestarts()
    {
        Pair pair = CreatePair(Vector3.zero);
        RecordStraight(pair.Leader, 60);
        SetPrivate(pair.Leader.Speed, "courseSpeed", 0f);
        pair.Navigation.Tick(0d);
        Assert.That(pair.Follower.Follow.IsFollowing, Is.True);
        Assert.That(pair.Follower.Speed.FollowSpeedCap, Is.Zero);
        Assert.That(pair.Follower.Speed.IsPlayerStopped, Is.False);
        RecordStraight(pair.Leader, 140, 10, 70);
        SetPrivate(pair.Leader.Speed, "courseSpeed", 3f);
        pair.Navigation.Tick(1d);
        Assert.That(pair.Follower.Speed.FollowSpeedCap, Is.GreaterThan(0f));
        Assert.That(pair.Follower.Destination.HasDestination, Is.True);
    }

    [Test]
    public void PlayerOrderRetained_CancelImmediatelyReleasesFollowCapAndOwnedDestination()
    {
        Pair pair = CreatePair(Vector3.zero);
        pair.Follower.Speed.DecreasePlayerSpeedOrder();
        pair.Follower.Speed.DecreasePlayerSpeedOrder();
        pair.Follower.Speed.DecreasePlayerSpeedOrder();
        RecordStraight(pair.Leader, 200);
        pair.Navigation.Tick(0d);
        Assert.That(pair.Follower.Speed.PlayerSpeedOrderNormalized, Is.EqualTo(0.25f));
        Assert.That(pair.Follower.Speed.FollowSpeedCap, Is.EqualTo(4f));
        pair.Follower.Follow.CancelFollow();
        Assert.That(pair.Follower.Speed.FollowSpeedCapActive, Is.False);
        Assert.That(pair.Follower.Speed.PlayerSpeedOrderNormalized, Is.EqualTo(0.25f));
        Assert.That(pair.Follower.Destination.HasDestination, Is.False);
        Assert.That(pair.Navigation.State, Is.EqualTo(ShipFollowNavigationController.ReplayState.Inactive));
    }

    [Test]
    public void LostTarget_CancelsEvenDestinationOwnedReplansAndNeverResumesOlderDestination()
    {
        Pair pair = CreatePair(Vector3.zero);
        RecordStraight(pair.Leader, 200);
        pair.Navigation.Tick(0d);
        // Destination can issue planner commands while owning its current target.
        pair.Follower.Planner.ExecuteHeadingCommand(15f, TurnDirection.Clockwise);
        Object.DestroyImmediate(pair.Leader.Root);
        pair.Navigation.Tick(0.01d); // Loss must not wait for think cadence.
        Assert.That(pair.Navigation.State, Is.EqualTo(ShipFollowNavigationController.ReplayState.LostTargetHold));
        Assert.That(pair.Follower.Follow.HasFollowIntent, Is.True);
        Assert.That(pair.Follower.Speed.FollowSpeedCap, Is.Zero);
        Assert.That(pair.Follower.Speed.IsPlayerStopped, Is.False);
        Assert.That(pair.Follower.Destination.HasDestination, Is.False);
        Assert.That(pair.Follower.Planner.IsActive, Is.False);
        Assert.That(pair.Navigation.TrailCursor, Is.Zero);
    }

    [Test]
    public void ReplayTick_DoesNotWriteRootPoseOrCurrentSpeedAndUsesNoPlayerBoundary()
    {
        Pair pair = CreatePair(Vector3.zero);
        RecordStraight(pair.Leader, 200);
        SetPrivate(pair.Follower.Speed, "currentSpeed", 2.5f);
        Vector3 position = pair.Follower.Root.transform.position;
        Quaternion rotation = pair.Follower.Root.transform.rotation;
        pair.Navigation.Tick(0d);
        Assert.That(pair.Follower.Root.transform.position, Is.EqualTo(position));
        Assert.That(pair.Follower.Root.transform.rotation, Is.EqualTo(rotation));
        Assert.That(pair.Follower.Speed.CurrentSpeed, Is.EqualTo(2.5f));
        string folder = Path.Combine(Application.dataPath, "Assets/Game/Scripts/Sailing");
        foreach (string file in Directory.GetFiles(folder, "ShipFollow*.cs"))
        {
            string source = File.ReadAllText(file);
            foreach (string forbidden in new[] { ".StartTack(", ".StartWear(",
                "TrySubmitDirectedHeading", "ShipPlayerCommandInput", "CombatAI", "TeamId" })
                Assert.That(source, Does.Not.Contain(forbidden), file);
            Assert.That(Regex.IsMatch(source,
                @"(?:transform\.(?:position|rotation|eulerAngles)|CurrentSpeed)\s*[+\-*/]?="), Is.False, file);
        }
    }

    [TestCase(6f, 2f, 6f, 2f)]
    [TestCase(2f, 6f, 2f, 2f)]
    public void SpeedMismatch_UsesOwnCapabilityThenLeaderReferenceAtNormalGap(
        float ownCapability, float leaderReference, float catchUpCap, float followingCap)
    {
        Pair pair = CreatePair(Vector3.zero);
        SetPrivate(pair.Follower.Speed, "polarTargetSpeed", ownCapability);
        SetPrivate(pair.Leader.Speed, "courseSpeed", leaderReference);
        RecordStraight(pair.Leader, 200);
        pair.Navigation.Tick(0d);
        Assert.That(pair.Navigation.State, Is.EqualTo(ShipFollowNavigationController.ReplayState.CatchUp));
        Assert.That(pair.Follower.Speed.FollowSpeedCap, Is.EqualTo(catchUpCap));
        for (int step = 1; step <= 7; step++)
        {
            pair.Follower.Root.transform.position = Vector3.forward * (step * 20f);
            pair.Navigation.Tick(step);
            Assert.That(pair.Follower.Speed.FollowSpeedCap, Is.LessThanOrEqualTo(ownCapability));
        }
        Assert.That(pair.Navigation.AlongTrailGap, Is.EqualTo(60d));
        Assert.That(pair.Navigation.State, Is.EqualTo(ShipFollowNavigationController.ReplayState.Following));
        Assert.That(pair.Follower.Speed.FollowSpeedCap, Is.EqualTo(followingCap));
    }

    [Test]
    public void StoppedLeader_RemainingSafeTrailIsConsumedThenResumeUsesSameRelationship()
    {
        Pair pair = CreatePair(Vector3.zero);
        RecordStraight(pair.Leader, 300);
        SetPrivate(pair.Leader.Speed, "courseSpeed", 0f);
        int version = pair.Follower.Follow.RelationshipVersion;
        for (int step = 0; step <= 12; step++)
        {
            pair.Follower.Root.transform.position = Vector3.forward * (step * 20f);
            pair.Navigation.Tick(step);
        }
        Assert.That(pair.Navigation.TrailCursor, Is.EqualTo(240d));
        Assert.That(pair.Navigation.AlongTrailGap, Is.EqualTo(60d));
        Assert.That(pair.Follower.Speed.FollowSpeedCap, Is.Zero);
        Assert.That(pair.Follower.Destination.HasDestination, Is.False);
        Assert.That(pair.Follower.Follow.IsFollowing, Is.True);
        RecordStraight(pair.Leader, 380, 10, 310);
        SetPrivate(pair.Leader.Speed, "courseSpeed", 3f);
        pair.Navigation.Tick(13d);
        Assert.That(pair.Follower.Speed.FollowSpeedCap, Is.GreaterThan(0f));
        Assert.That(pair.Follower.Destination.HasDestination, Is.True);
        Assert.That(pair.Follower.Follow.RelationshipVersion, Is.EqualTo(version));
    }

    private Pair CreatePair(Vector3 followerPosition)
    {
        Ship leader = CreateShip(Vector3.zero);
        Ship follower = CreateShip(followerPosition);
        ShipFollowNavigationController navigation = follower.Root.AddComponent<ShipFollowNavigationController>();
        Assert.That(follower.Follow.TryBeginFollowRelationship(leader.Follow), Is.True);
        return new Pair(leader, follower, navigation);
    }

    private Pair CreateTurnIntervalPair(TurnDirection direction)
    {
        Pair pair = CreatePair(Vector3.zero);
        RecordStraight(pair.Leader, 100);
        float sign = direction == TurnDirection.Clockwise ? 1f : -1f;
        Record(pair.Leader, new Vector3(0f, 0f, 120f), sign * 20f);
        Record(pair.Leader, new Vector3(0f, 0f, 140f), sign * 45f);
        Record(pair.Leader, new Vector3(0f, 0f, 240f), sign * 45f);
        pair.Navigation.Tick(0d);
        for (int step = 1; step <= 5; step++)
        {
            pair.Follower.Root.transform.position = Vector3.forward * (step * 20f);
            pair.Navigation.Tick(step);
        }
        Assert.That(pair.Navigation.State,
            Is.EqualTo(ShipFollowNavigationController.ReplayState.ReplayingTurn));
        Assert.That(pair.Follower.Planner.CurrentManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.NormalTurn));
        Assert.That(pair.Follower.Planner.PlannedTurnDirection, Is.EqualTo(direction));
        return pair;
    }

    private static void CompleteNormalTurn(Ship ship, float completedYaw)
    {
        ship.Root.transform.rotation = Quaternion.Euler(0f, completedYaw, 0f);
        InvokePrivate(ship.Heading, "Update");
        InvokePrivate(ship.Planner, "Update");
        Assert.That(ship.Heading.IsActive, Is.False);
        Assert.That(ship.Planner.IsActive, Is.False);
    }

    private static void AssertConsumedProgress(Pair pair, double expected)
    {
        Assert.That(pair.Navigation.TrailCursor, Is.EqualTo(expected));
        Assert.That(pair.Follower.Follow.ConsumedTrailDistance, Is.EqualTo(expected));
        Assert.That(pair.Leader.Recorder.TryGetFollowerProgress(pair.Follower.Follow,
            out _, out double reported), Is.True);
        Assert.That(reported, Is.EqualTo(expected));
    }

    private static void RecordStraight(Ship ship, int end, int step = 10, int start = 10)
    {
        for (int distance = start; distance <= end; distance += step)
            Record(ship, new Vector3(0f, 0f, distance), 0f);
    }

    private static void RecordTurn(Ship ship, float end, TurnDirection direction)
    {
        float arc = direction == TurnDirection.Clockwise ? Mathf.Repeat(end, 360f) : Mathf.Repeat(-end, 360f);
        int steps = Mathf.CeilToInt(arc / 30f);
        for (int i = 1; i <= steps; i++)
            Record(ship, new Vector3(0f, 0f, i * 4f),
                direction == TurnDirection.Clockwise ? arc * i / steps : -arc * i / steps);
        Record(ship, new Vector3(0f, 0f, 200f), end);
    }

    private static void Record(Ship ship, Vector3 position, float heading)
    {
        ship.Root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, heading, 0f));
        Assert.That(ship.Recorder.CaptureCurrentPose(ship.Recorder.HeadSample.Timestamp + 1d), Is.True);
    }

    private Ship CreateShip(Vector3 position)
    {
        GameObject root = new("Follow Replay Test Root");
        created.Add(root);
        root.transform.position = position;
        Ship ship = new(root);
        ship.Wind.windFromDegrees = 90f;
        SetPrivate(ship.Speed, "globalWind", ship.Wind);
        SetPrivate(ship.Speed, "polarTargetSpeed", 4f);
        SetPrivate(ship.Speed, "courseSpeed", 3f);
        SetPrivate(ship.Speed, "relativeWindAngleSigned", 90f);
        SetPrivate(ship.Turning, "shipSailingSpeed", ship.Speed);
        SetPrivate(ship.Heading, "shipTurning", ship.Turning);
        SetPrivate(ship.Tacking, "shipSailingSpeed", ship.Speed);
        SetPrivate(ship.Tacking, "shipTurning", ship.Turning);
        SetPrivate(ship.Tacking, "headingController", ship.Heading);
        SetPrivate(ship.Wearing, "shipSailingSpeed", ship.Speed);
        SetPrivate(ship.Wearing, "headingController", ship.Heading);
        SetPrivate(ship.Planner, "globalWind", ship.Wind);
        SetPrivate(ship.Planner, "headingController", ship.Heading);
        SetPrivate(ship.Planner, "shipTacking", ship.Tacking);
        SetPrivate(ship.Planner, "shipWearing", ship.Wearing);
        SetPrivate(ship.Destination, "globalWind", ship.Wind);
        SetPrivate(ship.Destination, "maneuverPlanner", ship.Planner);
        return ship;
    }

    private static void SetPrivate(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string name)
    {
        MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, name);
        method.Invoke(target, null);
    }

    private static void AssertHeading(float expected, float actual)
        => Assert.That(Mathf.Abs(Mathf.DeltaAngle(expected, actual)), Is.LessThanOrEqualTo(0.001f));

    private sealed class Pair
    {
        public readonly Ship Leader;
        public readonly Ship Follower;
        public readonly ShipFollowNavigationController Navigation;
        public Pair(Ship leader, Ship follower, ShipFollowNavigationController navigation)
        { Leader = leader; Follower = follower; Navigation = navigation; }
    }

    private sealed class Ship
    {
        public readonly GameObject Root;
        public readonly GlobalWind Wind;
        public readonly ShipSailingSpeed Speed;
        public readonly ShipTurning Turning;
        public readonly ShipHeadingController Heading;
        public readonly ShipTacking Tacking;
        public readonly ShipWearing Wearing;
        public readonly ShipManeuverPlanner Planner;
        public readonly ShipDestinationController Destination;
        public readonly ShipFollowTrailRecorder Recorder;
        public readonly ShipFollowController Follow;
        public Ship(GameObject root)
        {
            Root = root;
            Wind = root.AddComponent<GlobalWind>();
            Speed = root.AddComponent<ShipSailingSpeed>();
            Turning = root.AddComponent<ShipTurning>();
            Heading = root.AddComponent<ShipHeadingController>();
            Tacking = root.AddComponent<ShipTacking>();
            Wearing = root.AddComponent<ShipWearing>();
            Planner = root.AddComponent<ShipManeuverPlanner>();
            Destination = root.AddComponent<ShipDestinationController>();
            Recorder = root.AddComponent<ShipFollowTrailRecorder>();
            Follow = root.AddComponent<ShipFollowController>();
        }
    }
}
