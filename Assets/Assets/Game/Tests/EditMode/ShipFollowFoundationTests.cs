using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class ShipFollowFoundationTests
{
    private readonly List<GameObject> created = new();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject item in created)
        {
            if (item != null) Object.DestroyImmediate(item);
        }
        created.Clear();
    }

    [Test]
    public void ValidRelationship_BeginsWithoutPlayerOrCombatReferences()
    {
        Ship leader = CreateShip();
        Ship follower = CreateShip();

        Assert.That(follower.Follow.TryBeginFollowRelationship(leader.Follow), Is.True);

        Assert.That(follower.Follow.IsFollowing, Is.True);
        Assert.That(follower.Follow.HasFollowIntent, Is.True);
        Assert.That(follower.Follow.FollowTarget, Is.SameAs(leader.Follow));
        Assert.That(follower.Follow.TargetTrailRecorder, Is.SameAs(leader.Recorder));
        Assert.That(follower.Follow.FollowStartLeaderDistance, Is.Zero);
        Assert.That(leader.Recorder.SubscriberCount, Is.EqualTo(1));
    }

    [Test]
    public void SelfFollow_IsRejected()
    {
        Ship ship = CreateShip();
        Assert.That(ship.Follow.TryBeginFollowRelationship(ship.Follow), Is.False);
        Assert.That(ship.Follow.HasFollowIntent, Is.False);
        Assert.That(ship.Recorder.IsRecording, Is.False);
    }

    [TestCase(2)]
    [TestCase(3)]
    [TestCase(12)]
    public void CycleOfAnyLength_IsRejected(int length)
    {
        Ship[] ships = new Ship[length];
        for (int i = 0; i < length; i++) ships[i] = CreateShip();
        for (int i = 0; i < length - 1; i++)
        {
            Assert.That(ships[i].Follow.TryBeginFollowRelationship(ships[i + 1].Follow),
                Is.True);
        }

        Assert.That(ships[length - 1].Follow.TryBeginFollowRelationship(ships[0].Follow),
            Is.False);
        Assert.That(ships[length - 1].Follow.HasFollowIntent, Is.False);
        Assert.That(ships[0].Recorder.SubscriberCount, Is.Zero);
    }

    [Test]
    public void FourShipChain_AllowsEachShipToFollowAndRecordItsOwnMovement()
    {
        Ship a = CreateShip();
        Ship b = CreateShip();
        Ship c = CreateShip();
        Ship d = CreateShip();
        Assert.That(b.Follow.TryBeginFollowRelationship(a.Follow), Is.True);
        Assert.That(c.Follow.TryBeginFollowRelationship(b.Follow), Is.True);
        Assert.That(d.Follow.TryBeginFollowRelationship(c.Follow), Is.True);

        Capture(b, new Vector3(12f, 0f, 5f), 10f);

        Assert.That(a.Recorder.HeadDistance, Is.Zero);
        Assert.That(b.Recorder.HeadSample.WorldPosition, Is.EqualTo(b.Root.transform.position));
        Assert.That(c.Follow.TargetTrailRecorder, Is.SameAs(b.Recorder));
        Assert.That(d.Follow.TargetTrailRecorder, Is.SameAs(c.Recorder));
        Assert.That(a.Recorder.SubscriberCount, Is.EqualTo(1));
        Assert.That(b.Recorder.SubscriberCount, Is.EqualTo(1));
        Assert.That(c.Recorder.SubscriberCount, Is.EqualTo(1));
        Assert.That(d.Recorder.IsRecording, Is.False);
    }

    [Test]
    public void RejectedReplacement_PreservesRelationshipStartAndProgress()
    {
        Ship leader = CreateShip();
        Ship follower = CreateShip();
        Ship descendant = CreateShip();
        Begin(follower, leader);
        Capture(leader, new Vector3(0f, 0f, 12f), 0f);
        Assert.That(follower.Follow.ReportConsumedTrailDistance(8d), Is.True);
        Begin(descendant, follower);

        Assert.That(follower.Follow.TryBeginFollowRelationship(descendant.Follow), Is.False);

        Assert.That(follower.Follow.FollowTarget, Is.SameAs(leader.Follow));
        Assert.That(follower.Follow.TargetTrailRecorder, Is.SameAs(leader.Recorder));
        Assert.That(follower.Follow.FollowStartLeaderDistance, Is.Zero);
        Assert.That(follower.Follow.ConsumedTrailDistance, Is.EqualTo(8d));
        Assert.That(leader.Recorder.SubscriberCount, Is.EqualTo(1));
        Assert.That(leader.Recorder.SampleCount, Is.EqualTo(2));
        Assert.That(descendant.Recorder.IsRecording, Is.False);
    }

    [Test]
    public void SameTarget_IsNoOpAndDoesNotRecaptureOrResetProgress()
    {
        Ship leader = CreateShip();
        Ship follower = CreateShip();
        Begin(follower, leader);
        Capture(leader, new Vector3(0f, 0f, 10f), 0f);
        follower.Follow.ReportConsumedTrailDistance(6d);
        ShipFollowTrailSample previousHead = leader.Recorder.HeadSample;
        leader.Root.transform.position = new Vector3(0f, 0f, 30f);

        Assert.That(follower.Follow.TryBeginFollowRelationship(leader.Follow), Is.True);

        Assert.That(follower.Follow.FollowStartLeaderDistance, Is.Zero);
        Assert.That(follower.Follow.ConsumedTrailDistance, Is.EqualTo(6d));
        Assert.That(leader.Recorder.HeadSample, Is.EqualTo(previousHead));
        Assert.That(leader.Recorder.SampleCount, Is.EqualTo(2));
        Assert.That(leader.Recorder.SubscriberCount, Is.EqualTo(1));
        Assert.That(leader.Recorder.TryGetFollowerProgress(follower.Follow,
            out double start, out double consumed), Is.True);
        Assert.That(start, Is.Zero);
        Assert.That(consumed, Is.EqualTo(6d));
    }

    [TestCase("null")]
    [TestCase("inactive")]
    [TestCase("missing-recorder")]
    [TestCase("disabled-recorder")]
    [TestCase("disabled-movement")]
    public void InvalidTarget_RejectsWithoutReplacingOldRelationship(string failure)
    {
        Ship leader = CreateShip();
        Ship follower = CreateShip();
        Ship invalid = CreateShip();
        Begin(follower, leader);
        ShipFollowController target = invalid.Follow;
        switch (failure)
        {
            case "null": target = null; break;
            case "inactive": invalid.Root.SetActive(false); break;
            case "missing-recorder": Object.DestroyImmediate(invalid.Recorder); break;
            case "disabled-recorder": invalid.Recorder.enabled = false; break;
            case "disabled-movement": invalid.Speed.enabled = false; break;
        }

        Assert.That(follower.Follow.TryBeginFollowRelationship(target), Is.False);
        Assert.That(follower.Follow.FollowTarget, Is.SameAs(leader.Follow));
        Assert.That(leader.Recorder.SubscriberCount, Is.EqualTo(1));
    }

    [Test]
    public void InvalidFollowerMovement_RejectsWithoutStartingRecorder()
    {
        Ship leader = CreateShip();
        Ship follower = CreateShip();
        follower.Planner.enabled = false;
        Assert.That(follower.Follow.TryBeginFollowRelationship(leader.Follow), Is.False);
        Assert.That(leader.Recorder.IsRecording, Is.False);
    }

    [Test]
    public void SuccessfulReplacement_ReleasesOldRecorderAndSubscribesNewOne()
    {
        Ship a = CreateShip();
        Ship b = CreateShip();
        Ship follower = CreateShip();
        Begin(follower, a);
        Begin(follower, b);
        Assert.That(a.Recorder.IsRecording, Is.False);
        Assert.That(a.Recorder.SampleCount, Is.Zero);
        Assert.That(b.Recorder.SubscriberCount, Is.EqualTo(1));
        Assert.That(follower.Follow.FollowTarget, Is.SameAs(b.Follow));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LostTarget_PreservesIntentUntilExplicitCancel(bool destroy)
    {
        Ship leader = CreateShip();
        Ship follower = CreateShip();
        Begin(follower, leader);
        Capture(leader, new Vector3(0f, 0f, 10f), 0f);
        follower.Follow.ReportConsumedTrailDistance(6d);
        if (destroy) Object.DestroyImmediate(leader.Root);
        else leader.Destination.enabled = false;

        Assert.DoesNotThrow(follower.Follow.RefreshFollowRelationship);

        Assert.That(follower.Follow.State, Is.EqualTo(ShipFollowController.RelationshipState.TargetLost));
        Assert.That(follower.Follow.IsFollowing, Is.False);
        Assert.That(follower.Follow.HasFollowIntent, Is.True);
        Assert.That(follower.Follow.FollowTarget, Is.Null);
        Assert.That(follower.Follow.TargetTrailRecorder, Is.Null);
        Assert.That(follower.Follow.ConsumedTrailDistance, Is.EqualTo(6d));
        follower.Follow.CancelFollow();
        Assert.That(follower.Follow.State, Is.EqualTo(ShipFollowController.RelationshipState.None));
        Assert.That(follower.Follow.HasFollowIntent, Is.False);
        Assert.That(follower.Follow.ConsumedTrailDistance, Is.Zero);
    }

    [Test]
    public void DisabledLeaderRecorder_NotifiesAllFollowersAndCleansHistory()
    {
        Ship leader = CreateShip();
        Ship first = CreateShip();
        Ship second = CreateShip();
        Begin(first, leader);
        Begin(second, leader);
        leader.Recorder.enabled = false;
        // EditMode does not guarantee OnDisable; drive the same validation boundary.
        leader.Recorder.CaptureCurrentPose(leader.Recorder.HeadSample.Timestamp + 1d);
        first.Follow.RefreshFollowRelationship();
        second.Follow.RefreshFollowRelationship();
        Assert.That(first.Follow.State, Is.EqualTo(ShipFollowController.RelationshipState.TargetLost));
        Assert.That(second.Follow.State, Is.EqualTo(ShipFollowController.RelationshipState.TargetLost));
        Assert.That(leader.Recorder.SubscriberCount, Is.Zero);
        Assert.That(leader.Recorder.SampleCount, Is.Zero);
    }

    [Test]
    public void DestroyedFollower_IsRemovedSafelyOnNextCapture()
    {
        Ship leader = CreateShip();
        Ship follower = CreateShip();
        Begin(follower, leader);
        Object.DestroyImmediate(follower.Root);
        Assert.DoesNotThrow(() => leader.Recorder.CaptureCurrentPose(1d));
        Assert.That(leader.Recorder.IsRecording, Is.False);
        Assert.That(leader.Recorder.SampleCount, Is.Zero);
    }

    [Test]
    public void FirstFollower_ActivatesInitialActualWorldPoseAndCourseSpeedSample()
    {
        Ship leader = CreateShip();
        Ship follower = CreateShip();
        leader.Root.transform.SetPositionAndRotation(new Vector3(15f, 7f, -20f),
            Quaternion.Euler(0f, 100f, 0f));
        SetPrivate(leader.Speed, "currentSpeed", 2f);
        SetPrivate(leader.Speed, "courseSpeed", 2.4f);
        Assert.That(leader.Recorder.IsRecording, Is.False);
        Assert.That(leader.Recorder.SampleCount, Is.Zero);

        Begin(follower, leader);

        ShipFollowTrailSample sample = leader.Recorder.Samples[0];
        Assert.That(leader.Recorder.IsRecording, Is.True);
        Assert.That(leader.Recorder.SampleCount, Is.EqualTo(1));
        Assert.That(sample.WorldPosition, Is.EqualTo(new Vector3(15f, 7f, -20f)));
        AssertHeading(100f, sample.WorldHeading);
        Assert.That(sample.CumulativeDistance, Is.Zero);
        Assert.That(sample.CourseSpeed, Is.EqualTo(2.4f));
        Assert.That(double.IsNaN(sample.Timestamp), Is.False);
    }

    [Test]
    public void SecondFollower_ReusesHistoryAndStartsAtCurrentIncludingUnsampledHead()
    {
        Ship leader = CreateShip();
        Ship first = CreateShip();
        Ship later = CreateShip();
        Begin(first, leader);
        Capture(leader, new Vector3(0f, 0f, 10f), 0f);
        leader.Root.transform.position = new Vector3(0f, 0f, 12f);

        Begin(later, leader);

        Assert.That(first.Follow.TargetTrailRecorder, Is.SameAs(later.Follow.TargetTrailRecorder));
        Assert.That(leader.Root.GetComponents<ShipFollowTrailRecorder>().Length, Is.EqualTo(1));
        Assert.That(leader.Recorder.SubscriberCount, Is.EqualTo(2));
        Assert.That(leader.Recorder.SampleCount, Is.EqualTo(2));
        Assert.That(first.Follow.FollowStartLeaderDistance, Is.Zero);
        Assert.That(later.Follow.FollowStartLeaderDistance, Is.EqualTo(12d));
        Assert.That(later.Follow.ConsumedTrailDistance, Is.EqualTo(12d));
        Assert.That(leader.Recorder.TryGetSampleAtDistance(12d, out var start), Is.True);
        Assert.That(start.WorldPosition, Is.EqualTo(new Vector3(0f, 0f, 12f)));
    }

    [Test]
    public void DistanceThreshold_AppendsSampleAtFourMeters()
    {
        Ship leader = CreateRecordingLeader(out _);
        Capture(leader, new Vector3(0f, 0f, 4f), 0f);
        Assert.That(leader.Recorder.SampleCount, Is.EqualTo(2));
        Assert.That(leader.Recorder.HeadDistance, Is.EqualTo(4d));
    }

    [Test]
    public void HeadingThreshold_AppendsSampleWithLittleTranslation()
    {
        Ship leader = CreateRecordingLeader(out _);
        Capture(leader, new Vector3(0f, 0f, 0.1f), 3f);
        Assert.That(leader.Recorder.SampleCount, Is.EqualTo(2));
        AssertHeading(3f, leader.Recorder.Samples[1].WorldHeading);
    }

    [Test]
    public void SubThresholdMotion_UpdatesHeadButDoesNotAppendSample()
    {
        Ship leader = CreateRecordingLeader(out _);
        Capture(leader, new Vector3(0f, 0f, 1f), 1f);
        Capture(leader, new Vector3(0f, 0f, 2f), 2f);
        Assert.That(leader.Recorder.SampleCount, Is.EqualTo(1));
        Assert.That(leader.Recorder.HeadDistance, Is.EqualTo(2d));
    }

    [Test]
    public void ObservedHorizontalPath_AccumulatesAcrossSubThresholdSegmentsAndReversals()
    {
        Ship leader = CreateRecordingLeader(out _);
        Capture(leader, new Vector3(2f, 0f, 0f), 0f);
        Capture(leader, new Vector3(2f, 0f, 2f), 0f);
        Capture(leader, Vector3.zero, 0f);
        Assert.That(leader.Recorder.Samples[1].CumulativeDistance, Is.EqualTo(4d));
        Assert.That(leader.Recorder.HeadDistance, Is.EqualTo(4d + Math.Sqrt(8d)).Within(0.00001d));
        double previous = -1d;
        foreach (ShipFollowTrailSample sample in leader.Recorder.Samples)
        {
            Assert.That(sample.CumulativeDistance, Is.GreaterThanOrEqualTo(previous));
            previous = sample.CumulativeDistance;
        }
    }

    [Test]
    public void VerticalOnlyMotion_AddsNoHorizontalDistanceOrSample()
    {
        Ship leader = CreateRecordingLeader(out _);
        Capture(leader, new Vector3(0f, 30f, 0f), 0f);
        Assert.That(leader.Recorder.HeadDistance, Is.Zero);
        Assert.That(leader.Recorder.SampleCount, Is.EqualTo(1));
        Assert.That(leader.Recorder.HeadSample.WorldPosition.y, Is.EqualTo(30f));
    }

    [TestCase(350f, 355f, 0f, TurnDirection.Clockwise, 10f)]
    [TestCase(10f, 5f, 0f, TurnDirection.CounterClockwise, -10f)]
    public void ConsecutiveTurnSamples_ExtendOneSignedEventAcrossWrap(
        float start, float middle, float end, TurnDirection direction, float signedDelta)
    {
        Ship leader = CreateShip();
        leader.Root.transform.rotation = Quaternion.Euler(0f, start, 0f);
        Begin(CreateShip(), leader);
        Capture(leader, new Vector3(0f, 0f, 5f), middle);
        Capture(leader, new Vector3(0f, 0f, 10f), end);

        Assert.That(leader.Recorder.TurnEvents.Count, Is.EqualTo(1));
        ShipFollowTurnEvent turn = leader.Recorder.TurnEvents[0];
        Assert.That(turn.Direction, Is.EqualTo(direction));
        Assert.That(turn.AccumulatedHeadingDelta, Is.EqualTo(signedDelta).Within(0.001f));
        Assert.That(turn.StartDistance, Is.Zero);
        Assert.That(turn.EndDistance, Is.EqualTo(10d));
        AssertHeading(start, turn.StartingHeading);
        AssertHeading(end, turn.EndingHeading);
        Assert.That(turn.IsComplete, Is.False);
    }

    [Test]
    public void StableHeadingSample_ClosesTurnEvent()
    {
        Ship leader = CreateRecordingLeader(out _);
        Capture(leader, new Vector3(0f, 0f, 5f), 5f);
        Capture(leader, new Vector3(0f, 0f, 10f), 5f);
        Assert.That(leader.Recorder.TurnEvents.Count, Is.EqualTo(1));
        Assert.That(leader.Recorder.TurnEvents[0].IsComplete, Is.True);
        Assert.That(leader.Recorder.TurnEvents[0].EndDistance, Is.EqualTo(5d));
    }

    [Test]
    public void DirectionReversal_ClosesOldTurnAndStartsNewTurn()
    {
        Ship leader = CreateRecordingLeader(out _);
        Capture(leader, new Vector3(0f, 0f, 5f), 5f);
        Capture(leader, new Vector3(0f, 0f, 10f), 0f);
        Assert.That(leader.Recorder.TurnEvents.Count, Is.EqualTo(2));
        Assert.That(leader.Recorder.TurnEvents[0].IsComplete, Is.True);
        Assert.That(leader.Recorder.TurnEvents[1].IsComplete, Is.False);
        Assert.That(leader.Recorder.TurnEvents[1].Direction, Is.EqualTo(TurnDirection.CounterClockwise));
        Assert.That(leader.Recorder.TurnEvents[1].StartDistance, Is.EqualTo(5d));
    }

    [Test]
    public void SampleQuery_InterpolatesPositionSpeedTimeAndHeadingAcrossWrap()
    {
        Ship leader = CreateShip();
        leader.Root.transform.rotation = Quaternion.Euler(0f, 350f, 0f);
        SetPrivate(leader.Speed, "courseSpeed", 2f);
        Begin(CreateShip(), leader);
        double startTime = leader.Recorder.HeadSample.Timestamp;
        SetPrivate(leader.Speed, "courseSpeed", 4f);
        leader.Root.transform.SetPositionAndRotation(new Vector3(0f, 0f, 10f),
            Quaternion.Euler(0f, 10f, 0f));
        leader.Recorder.CaptureCurrentPose(startTime + 2d);

        Assert.That(leader.Recorder.TryGetSampleAtDistance(5d, out var sample), Is.True);
        Assert.That(sample.WorldPosition, Is.EqualTo(new Vector3(0f, 0f, 5f)));
        AssertHeading(0f, sample.WorldHeading);
        Assert.That(sample.CourseSpeed, Is.EqualTo(3f));
        Assert.That(sample.Timestamp, Is.EqualTo(startTime + 1d).Within(0.00001d));
        Assert.That(leader.Recorder.TryGetSampleAtDistance(-1d, out _), Is.False);
        Assert.That(leader.Recorder.TryGetSampleAtDistance(11d, out _), Is.False);
        Assert.That(leader.Recorder.TryGetSampleAtDistance(double.NaN, out _), Is.False);
    }

    [Test]
    public void SampleQuery_AtStationaryTurnUsesLatestHeadingAtSharedDistance()
    {
        Ship leader = CreateRecordingLeader(out _);
        Capture(leader, Vector3.zero, 5f);
        Capture(leader, Vector3.zero, 10f);
        Assert.That(leader.Recorder.TryGetSampleAtDistance(0d, out var sample), Is.True);
        AssertHeading(10f, sample.WorldHeading);
    }

    [Test]
    public void TrailCollections_CannotBeModifiedByConsumers()
    {
        Ship leader = CreateRecordingLeader(out _);
        Capture(leader, new Vector3(0f, 0f, 5f), 5f);
        Assert.That(leader.Recorder.Samples, Is.Not.InstanceOf<List<ShipFollowTrailSample>>());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<ShipFollowTrailSample>)leader.Recorder.Samples).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<ShipFollowTurnEvent>)leader.Recorder.TurnEvents).Clear());
        Assert.That(leader.Recorder.SampleCount, Is.EqualTo(2));
        Assert.That(leader.Recorder.TurnEvents.Count, Is.EqualTo(1));
    }

    [Test]
    public void CancellingOneSubscriber_KeepsSharedRecorderAlive()
    {
        Ship leader = CreateRecordingLeader(out Ship first);
        Ship second = CreateShip();
        Begin(second, leader);
        first.Follow.CancelFollow();
        Capture(leader, new Vector3(0f, 0f, 5f), 0f);
        Assert.That(leader.Recorder.IsRecording, Is.True);
        Assert.That(leader.Recorder.SubscriberCount, Is.EqualTo(1));
        Assert.That(leader.Recorder.SampleCount, Is.EqualTo(2));
        Assert.That(second.Follow.IsFollowing, Is.True);
    }

    [Test]
    public void FinalUnsubscribe_CleansHistoryAndNextSubscriptionStartsFresh()
    {
        Ship leader = CreateRecordingLeader(out Ship follower);
        Capture(leader, new Vector3(0f, 0f, 5f), 5f);
        follower.Follow.CancelFollow();
        Assert.That(leader.Recorder.IsRecording, Is.False);
        Assert.That(leader.Recorder.SampleCount, Is.Zero);
        Assert.That(leader.Recorder.TurnEvents, Is.Empty);
        Assert.That(leader.Recorder.HeadDistance, Is.Zero);
        Assert.That(leader.Recorder.CaptureCurrentPose(1d), Is.False);
        Assert.That(leader.Recorder.TryGetSampleAtDistance(0d, out _), Is.False);
        Begin(follower, leader);
        Assert.That(leader.Recorder.SampleCount, Is.EqualTo(1));
        Assert.That(leader.Recorder.HeadDistance, Is.Zero);
        Assert.That(leader.Recorder.Samples[0].WorldPosition, Is.EqualTo(new Vector3(0f, 0f, 5f)));
    }

    [Test]
    public void Trimming_UsesSlowestProgressMinusDefaultTwentyFiveMeterMargin()
    {
        Ship leader = CreateRecordingLeader(out Ship slow);
        Ship fast = CreateShip();
        Begin(fast, leader);
        RecordStraight(leader, 100);

        Assert.That(fast.Follow.ReportConsumedTrailDistance(100d), Is.True);
        Assert.That(leader.Recorder.Samples[0].CumulativeDistance, Is.Zero);
        Assert.That(slow.Follow.ReportConsumedTrailDistance(40d), Is.True);
        Assert.That(leader.Recorder.Samples[0].CumulativeDistance, Is.EqualTo(10d));
        Assert.That(leader.Recorder.TryGetSampleAtDistance(15d, out _), Is.True);
        Assert.That(slow.Follow.ReportConsumedTrailDistance(80d), Is.True);
        Assert.That(leader.Recorder.Samples[0].CumulativeDistance, Is.EqualTo(50d));
        Assert.That(leader.Recorder.HeadDistance, Is.EqualTo(100d));
    }

    [Test]
    public void LongTrail_TrimsCompletedDataWhileRetainingSlowestFollowersNeededGeometry()
    {
        Ship leader = CreateRecordingLeader(out Ship slow);
        Ship fast = CreateShip();
        Begin(fast, leader);
        var samplesView = leader.Recorder.Samples;
        var turnsView = leader.Recorder.TurnEvents;
        for (int step = 1; step <= 1000; step++)
        {
            float heading = step % 50 < 5 ? (step % 50) * 15f : 60f;
            Capture(leader, Vector3.forward * (step * 10f), heading);
            double fastProgress = System.Math.Max(0d, leader.Recorder.HeadDistance - 50d);
            double slowProgress = System.Math.Max(0d, leader.Recorder.HeadDistance - 150d);
            Assert.That(fast.Follow.ReportConsumedTrailDistance(fastProgress), Is.True);
            Assert.That(slow.Follow.ReportConsumedTrailDistance(slowProgress), Is.True);
            Assert.That(leader.Recorder.TryGetSampleAtDistance(slowProgress, out _), Is.True);
            if (step > 50)
            {
                Assert.That(leader.Recorder.SampleCount, Is.LessThan(30));
                Assert.That(leader.Recorder.TurnEvents.Count, Is.LessThanOrEqualTo(3));
            }
        }
        Assert.That(leader.Recorder.HeadDistance, Is.EqualTo(10000d));
        Assert.That(leader.Recorder.Samples[0].CumulativeDistance, Is.GreaterThan(9700d));
        Assert.That(leader.Recorder.Samples, Is.SameAs(samplesView));
        Assert.That(leader.Recorder.TurnEvents, Is.SameAs(turnsView));
        fast.Follow.CancelFollow();
        slow.Follow.CancelFollow();
        Assert.That(leader.Recorder.SubscriberCount, Is.Zero);
        Assert.That(samplesView, Is.Empty);
        Assert.That(turnsView, Is.Empty);
    }

    [Test]
    public void LaterFollower_DoesNotPinHistoryBeforeItsStart()
    {
        Ship leader = CreateRecordingLeader(out Ship first);
        RecordStraight(leader, 100);
        Ship later = CreateShip();
        Begin(later, leader);
        first.Follow.CancelFollow();
        Assert.That(later.Follow.FollowStartLeaderDistance, Is.EqualTo(100d));
        Assert.That(leader.Recorder.Samples[0].CumulativeDistance, Is.EqualTo(70d));
        Assert.That(leader.Recorder.TryGetSampleAtDistance(100d, out _), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Trimming_PreservesNeededTurnStartUntilWholeCompletedTurnIsBehindMargin(bool complete)
    {
        Ship leader = CreateRecordingLeader(out Ship follower);
        Capture(leader, new Vector3(0f, 0f, 10f), 0f);
        Capture(leader, new Vector3(0f, 0f, 20f), 0f);
        for (int distance = 30; distance <= 70; distance += 10)
        {
            Capture(leader, new Vector3(0f, 0f, distance), (distance - 20) * 0.3f);
        }
        if (complete)
        {
            for (int distance = 80; distance <= 140; distance += 10)
                Capture(leader, new Vector3(0f, 0f, distance), 15f);
            follower.Follow.ReportConsumedTrailDistance(90d);
        }
        else
        {
            follower.Follow.ReportConsumedTrailDistance(70d);
        }

        Assert.That(leader.Recorder.TurnEvents.Count, Is.EqualTo(1));
        Assert.That(leader.Recorder.TurnEvents[0].StartDistance, Is.EqualTo(20d));
        Assert.That(leader.Recorder.TryGetSampleAtDistance(20d, out _), Is.True);
        Assert.That(leader.Recorder.Samples[0].CumulativeDistance, Is.LessThanOrEqualTo(20d));
        if (complete)
        {
            follower.Follow.ReportConsumedTrailDistance(140d);
            Assert.That(leader.Recorder.TurnEvents, Is.Empty);
            Assert.That(leader.Recorder.Samples[0].CumulativeDistance, Is.EqualTo(110d));
        }
    }

    [Test]
    public void Progress_RejectsBackwardNonFiniteAndBeyondHeadWithoutMutation()
    {
        Ship leader = CreateRecordingLeader(out Ship follower);
        Capture(leader, new Vector3(0f, 0f, 10f), 0f);
        follower.Follow.ReportConsumedTrailDistance(5d);
        foreach (double invalid in new[] { -1d, 4d, 11d, double.NaN, double.PositiveInfinity })
        {
            Assert.That(follower.Follow.ReportConsumedTrailDistance(invalid), Is.False);
            Assert.That(follower.Follow.ConsumedTrailDistance, Is.EqualTo(5d));
        }
        Assert.That(leader.Recorder.TryGetFollowerProgress(follower.Follow, out _,
            out double progress), Is.True);
        Assert.That(progress, Is.EqualTo(5d));
    }

    [Test]
    public void FollowOperations_DoNotMutateMovementOwnersOrFormation()
    {
        Ship leader = CreateShip();
        Ship follower = CreateShip();
        FormationCommandController formation = CreateObject().AddComponent<FormationCommandController>();
        SetPrivate(formation, "isActive", true);
        SetPrivate(formation, "formationState", FormationCommandController.FormationState.Moving);
        SetPrivate(formation, "requestedManeuverStyle", FormationManeuverStyle.InSuccession);
        SetPrivate(formation, "effectiveManeuverStyle", FormationManeuverStyle.InSuccession);
        follower.Destination.SetDestination(new Vector3(50f, 0f, 50f),
            ShipDestinationController.TurnSelectionMode.Auto, WindNavigationAssistMode.Manual);
        follower.Speed.DecreasePlayerSpeedOrder();
        follower.Speed.SetFormationMaximumTargetSpeed(2f);
        follower.Speed.SetPlayerStopSpeedCap(0f);
        SetPrivate(follower.Speed, "currentSpeed", 1.7f);
        SetPrivate(follower.Tacking, "isActive", true);
        SetPrivate(follower.Wearing, "isActive", true);
        Component[] owners = { follower.Speed, follower.Turning, follower.Heading,
            follower.Planner, follower.Tacking, follower.Wearing, follower.Destination,
            leader.Speed, leader.Turning, leader.Heading, leader.Planner,
            leader.Tacking, leader.Wearing, leader.Destination, formation };
        string[] before = new string[owners.Length];
        for (int i = 0; i < owners.Length; i++) before[i] = JsonUtility.ToJson(owners[i]);
        Vector3 position = follower.Root.transform.position;
        Quaternion rotation = follower.Root.transform.rotation;
        Vector3 leaderPosition = leader.Root.transform.position;
        Quaternion leaderRotation = leader.Root.transform.rotation;
        int sequence = follower.Planner.CommandSequence;

        Begin(follower, leader);
        leader.Recorder.CaptureCurrentPose(leader.Recorder.HeadSample.Timestamp + 1d);
        follower.Follow.ReportConsumedTrailDistance(0d);
        follower.Follow.RefreshFollowRelationship();
        follower.Follow.CancelFollow();

        for (int i = 0; i < owners.Length; i++)
            Assert.That(JsonUtility.ToJson(owners[i]), Is.EqualTo(before[i]), owners[i].GetType().Name);
        Assert.That(follower.Root.transform.position, Is.EqualTo(position));
        Assert.That(follower.Root.transform.rotation, Is.EqualTo(rotation));
        Assert.That(leader.Root.transform.position, Is.EqualTo(leaderPosition));
        Assert.That(leader.Root.transform.rotation, Is.EqualTo(leaderRotation));
        Assert.That(follower.Speed.CurrentSpeed, Is.EqualTo(1.7f));
        Assert.That(follower.Planner.CommandSequence, Is.EqualTo(sequence));
        Assert.That(formation.IsActive, Is.True);
    }

    [Test]
    public void FollowFoundation_HasNoCombatInputNavigationOrPoseWriterDependency()
    {
        string sourceRoot = Path.Combine(Application.dataPath, "Assets/Game/Scripts");
        foreach (string name in new[] { "ShipFollowController", "ShipFollowTrailRecorder" })
        {
            string source = File.ReadAllText(Path.Combine(sourceRoot, "Sailing", name + ".cs"));
            foreach (string forbidden in new[] { "Combat", "TeamId", "ShipPlayerCommandInput",
                "ShipSelectionManager", "FormationCommandController", "SetDestination(",
                "ExecuteHeadingCommand(", "SetTargetHeading(", "SetRudderCommand(",
                "StartTack(", "StartWear(", "CurrentSpeed", "transform.Rotate(",
                "SetPositionAndRotation(" })
                Assert.That(source, Does.Not.Contain(forbidden), name + ": " + forbidden);
            Assert.That(Regex.IsMatch(source,
                @"transform\.(?:position|rotation|eulerAngles|localPosition|localRotation)\s*[+\-*/]?="),
                Is.False);
        }
        foreach (string path in Directory.GetFiles(Path.Combine(sourceRoot, "Combat"),
            "*AI*.cs", SearchOption.AllDirectories))
            Assert.That(File.ReadAllText(path), Does.Not.Contain("ShipFollow"), path);
    }

    private Ship CreateRecordingLeader(out Ship follower)
    {
        Ship leader = CreateShip();
        follower = CreateShip();
        Begin(follower, leader);
        return leader;
    }

    private static void Begin(Ship follower, Ship leader)
        => Assert.That(follower.Follow.TryBeginFollowRelationship(leader.Follow), Is.True);

    private static void Capture(Ship ship, Vector3 position, float heading)
    {
        ship.Root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, heading, 0f));
        Assert.That(ship.Recorder.CaptureCurrentPose(ship.Recorder.HeadSample.Timestamp + 1d), Is.True);
    }

    private static void RecordStraight(Ship ship, int maximum)
    {
        for (int distance = 10; distance <= maximum; distance += 10)
            Capture(ship, new Vector3(0f, 0f, distance), 0f);
    }

    private Ship CreateShip()
    {
        GameObject root = CreateObject();
        GlobalWind wind = root.AddComponent<GlobalWind>();
        wind.windFromDegrees = 90f;
        Ship ship = new Ship(root);
        SetPrivate(ship.Speed, "globalWind", wind);
        SetPrivate(ship.Turning, "shipSailingSpeed", ship.Speed);
        SetPrivate(ship.Heading, "shipTurning", ship.Turning);
        SetPrivate(ship.Tacking, "shipSailingSpeed", ship.Speed);
        SetPrivate(ship.Tacking, "shipTurning", ship.Turning);
        SetPrivate(ship.Tacking, "headingController", ship.Heading);
        SetPrivate(ship.Wearing, "shipSailingSpeed", ship.Speed);
        SetPrivate(ship.Wearing, "headingController", ship.Heading);
        SetPrivate(ship.Planner, "globalWind", wind);
        SetPrivate(ship.Planner, "headingController", ship.Heading);
        SetPrivate(ship.Planner, "shipTacking", ship.Tacking);
        SetPrivate(ship.Planner, "shipWearing", ship.Wearing);
        SetPrivate(ship.Destination, "globalWind", wind);
        SetPrivate(ship.Destination, "maneuverPlanner", ship.Planner);
        return ship;
    }

    private GameObject CreateObject()
    {
        GameObject root = new("Follow Test Root");
        created.Add(root);
        return root;
    }

    private static void SetPrivate(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        field.SetValue(target, value);
    }

    private static void AssertHeading(float expected, float actual)
        => Assert.That(Mathf.Abs(Mathf.DeltaAngle(expected, actual)), Is.LessThanOrEqualTo(0.001f));

    private sealed class Ship
    {
        public readonly GameObject Root;
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
