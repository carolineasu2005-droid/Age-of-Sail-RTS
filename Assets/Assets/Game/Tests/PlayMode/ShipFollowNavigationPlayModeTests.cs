using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class ShipFollowNavigationPlayModeTests
{
    private const double DistanceTolerance = 0.00001d;
    private readonly List<Object> created = new();
    private float originalTimeScale;
    private bool acceleratedSimulation;

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (acceleratedSimulation)
        {
            Time.timeScale = originalTimeScale;
            acceleratedSimulation = false;
        }
        foreach (Object item in created)
            if (item != null) Object.Destroy(item);
        created.Clear();
        yield return null;
    }

    [UnityTest]
    public IEnumerator EntryNavigation_UsesNaturalAccelerationAndRealRootTranslation()
    {
        Ship leader = CreateShip(Vector3.zero, 2f);
        Ship follower = CreateShip(new Vector3(0f, 0f, -120f), 0f);
        ShipFollowNavigationController navigation = follower.Root.AddComponent<ShipFollowNavigationController>();
        follower.Speed.DecreasePlayerSpeedOrder();
        follower.Speed.DecreasePlayerSpeedOrder();
        follower.Speed.DecreasePlayerSpeedOrder();
        Vector3 before = follower.Root.transform.position;
        Assert.That(follower.Follow.TryBeginFollowRelationship(leader.Follow), Is.True);
        navigation.Tick(Time.timeAsDouble);
        Assert.That(follower.Root.transform.position, Is.EqualTo(before), "Entry command must not snap Root.");
        Assert.That(follower.Speed.CurrentSpeed, Is.Zero);
        Assert.That(navigation.State, Is.EqualTo(ShipFollowNavigationController.ReplayState.ApproachingTrailStart));
        Assert.That(navigation.HasReplayTarget, Is.True);
        const float minimumDisplacement = 0.25f;
        double deadline = Time.timeAsDouble + 6d;
        double realDeadline = Time.realtimeSinceStartupAsDouble + 10d;
        while (Vector3.Distance(follower.Root.transform.position, before) < minimumDisplacement
            && Time.timeAsDouble < deadline && Time.realtimeSinceStartupAsDouble < realDeadline)
            yield return null;
        Assert.That(Vector3.Distance(follower.Root.transform.position, before),
            Is.GreaterThanOrEqualTo(minimumDisplacement), DescribeReplay(leader, follower, navigation));
        Assert.That(follower.Speed.CurrentSpeed, Is.GreaterThan(0f));
        Assert.That(follower.Speed.CurrentSpeed, Is.LessThan(follower.Speed.AvailableTargetSpeed),
            "Speed must still be accelerating naturally from rest.");
        Assert.That(follower.Speed.PlayerSpeedOrderNormalized, Is.EqualTo(0.25f));
        Assert.That(leader.Recorder.HeadDistance, Is.GreaterThan(0d));
        Assert.That(follower.Speed.IsPlayerStopped, Is.False);
    }

    [UnityTest]
    public IEnumerator StationaryShortTrail_HoldsWithoutDrivingIntoLeader()
    {
        Ship leader = CreateShip(Vector3.zero, 0f);
        leader.Speed.SetPlayerStopSpeedCap(0f);
        Ship follower = CreateShip(new Vector3(0f, 0f, -30f), 0f);
        ShipFollowNavigationController navigation = follower.Root.AddComponent<ShipFollowNavigationController>();
        follower.Follow.TryBeginFollowRelationship(leader.Follow);
        Vector3 before = follower.Root.transform.position;
        for (int frame = 0; frame < 10; frame++) yield return null;
        Assert.That(navigation.State, Is.EqualTo(ShipFollowNavigationController.ReplayState.TooCloseHold));
        Assert.That(follower.Root.transform.position, Is.EqualTo(before));
        Assert.That(follower.Speed.FollowSpeedCap, Is.Zero);
        Assert.That(follower.Speed.IsPlayerStopped, Is.False);
        Assert.That(follower.Follow.IsFollowing, Is.True);
    }

    [UnityTest]
    public IEnumerator LeaderResume_ReopensHistoryAndPropulsionWithoutNewRelationship()
    {
        Ship leader = CreateShip(Vector3.zero, 0f);
        leader.Speed.SetPlayerStopSpeedCap(0f);
        Ship follower = CreateShip(new Vector3(0f, 0f, -60f), 0f);
        follower.Root.AddComponent<ShipFollowNavigationController>();
        follower.Follow.TryBeginFollowRelationship(leader.Follow);
        for (int frame = 0; frame < 4; frame++) yield return null;
        Assert.That(follower.Speed.FollowSpeedCap, Is.Zero);
        int relationshipVersion = follower.Follow.RelationshipVersion;
        leader.Speed.ClearPlayerStopSpeedCap();
        double deadline = Time.timeAsDouble + 6d;
        int frames = 0;
        while (follower.Speed.CurrentSpeed <= 0.05f && Time.timeAsDouble < deadline && frames++ < 2000)
            yield return null;
        Assert.That(follower.Speed.CurrentSpeed, Is.GreaterThan(0.05f));
        Assert.That(leader.Recorder.HeadDistance, Is.GreaterThan(0d));
        Assert.That(follower.Follow.RelationshipVersion, Is.EqualTo(relationshipVersion));
        Assert.That(follower.Follow.IsFollowing, Is.True);
    }

    [UnityTest]
    public IEnumerator LostTarget_HoldsThroughNaturalDragAndCancelRestoresRetainedOrder()
    {
        Ship leader = CreateShip(Vector3.zero, 2f);
        Ship follower = CreateShip(new Vector3(0f, 0f, -120f), 3f);
        ShipFollowNavigationController navigation = follower.Root.AddComponent<ShipFollowNavigationController>();
        follower.Speed.DecreasePlayerSpeedOrder();
        follower.Follow.TryBeginFollowRelationship(leader.Follow);
        navigation.Tick(Time.timeAsDouble);
        Object.Destroy(leader.Root);
        yield return null;
        float before = follower.Speed.CurrentSpeed;
        yield return null;
        Assert.That(navigation.State, Is.EqualTo(ShipFollowNavigationController.ReplayState.LostTargetHold));
        Assert.That(follower.Speed.FollowSpeedCap, Is.Zero);
        Assert.That(follower.Speed.EffectiveTargetSpeed, Is.Zero);
        Assert.That(follower.Speed.CurrentSpeed, Is.GreaterThan(0f));
        Assert.That(follower.Speed.CurrentSpeed, Is.LessThan(before));
        Assert.That(follower.Speed.IsPlayerStopped, Is.False);
        Assert.That(follower.Destination.HasDestination, Is.False);
        Assert.That(follower.Follow.HasFollowIntent, Is.True);
        follower.Follow.CancelFollow();
        Assert.That(follower.Speed.FollowSpeedCapActive, Is.False);
        yield return null;
        Assert.That(follower.Speed.PlayerSpeedOrderNormalized, Is.EqualTo(0.75f));
        Assert.That(follower.Speed.EffectiveTargetSpeed,
            Is.EqualTo(follower.Speed.AvailableTargetSpeed * 0.75f).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator RecordedTurn_UsesRealHeadingTurningAndThenResumesTrailProgress()
    {
        Ship leader = CreateShip(Vector3.zero, 0f);
        leader.Speed.SetPlayerStopSpeedCap(0f);
        Ship follower = CreateShip(Vector3.zero, 3f);
        SetPrivate(follower.Turning, "maxTurnRate", 20f);
        SetPrivate(follower.Turning, "rudderResponse", 360f);
        SetPrivate(follower.Heading, "rudderEaseAngle", 10f);
        ShipFollowNavigationController navigation = follower.Root.AddComponent<ShipFollowNavigationController>();
        follower.Follow.TryBeginFollowRelationship(leader.Follow);
        Record(leader, new Vector3(0f, 0f, 4f), 15f);
        Record(leader, new Vector3(0f, 0f, 8f), 30f);
        Record(leader, new Vector3(0f, 0f, 200f), 30f);
        ShipFollowTurnEvent recordedTurn = leader.Recorder.TurnEvents[0];
        Assert.That(recordedTurn.IsComplete, Is.True);
        navigation.Tick(Time.timeAsDouble);
        Assert.That(navigation.State, Is.EqualTo(ShipFollowNavigationController.ReplayState.ReplayingTurn));
        Assert.That(navigation.EntryReached, Is.True);
        Assert.That(follower.Planner.PlannedTurnDirection, Is.EqualTo(TurnDirection.Clockwise));
        Assert.That(follower.Planner.gameObject, Is.SameAs(follower.Root));
        double deadline = Time.timeAsDouble + 10d;
        double realDeadline = Time.realtimeSinceStartupAsDouble + 15d;
        bool physicallyTurned = false;
        // Progress is held while the real planner/heading owner is executing the recorded turn.
        while ((navigation.ActiveTurn.HasValue || follower.Planner.IsActive || follower.Heading.IsActive)
            && Time.timeAsDouble < deadline && Time.realtimeSinceStartupAsDouble < realDeadline)
        {
            AssertPendingTurnBoundary(leader, follower, navigation);
            yield return null;
            physicallyTurned |= Mathf.Abs(Mathf.DeltaAngle(0f, follower.Root.transform.eulerAngles.y)) > 3f;
        }
        Assert.That(physicallyTurned, Is.True, DescribeReplay(leader, follower, navigation));
        Assert.That(follower.Planner.IsActive, Is.False, DescribeReplay(leader, follower, navigation));
        Assert.That(follower.Heading.IsActive, Is.False, DescribeReplay(leader, follower, navigation));
        Assert.That(navigation.ActiveTurn, Is.Null, DescribeReplay(leader, follower, navigation));
        Assert.That(navigation.TrailCursor, Is.GreaterThanOrEqualTo(recordedTurn.EndDistance),
            DescribeReplay(leader, follower, navigation));
        // The event's semantic consumption is followed by actual post-event geometric projection.
        while (navigation.TrailCursor <= recordedTurn.EndDistance + DistanceTolerance
            && Time.timeAsDouble < deadline && Time.realtimeSinceStartupAsDouble < realDeadline)
            yield return null;
        Assert.That(follower.Root.transform.position.sqrMagnitude, Is.GreaterThan(1f));
        Assert.That(navigation.TrailCursor,
            Is.GreaterThan(recordedTurn.EndDistance + DistanceTolerance),
            DescribeReplay(leader, follower, navigation));
        Assert.That(follower.Follow.ConsumedTrailDistance, Is.EqualTo(navigation.TrailCursor));
    }

    [UnityTest]
    public IEnumerator TwoShips_RecordedTwoHundredMeterLegThenClockwiseSixtyDegreeTurn()
    {
        yield return ReplayLongLegAndTurn(TurnDirection.Clockwise);
    }

    [UnityTest]
    public IEnumerator TwoShips_RecordedTwoHundredMeterLegThenCounterClockwiseSixtyDegreeTurn()
    {
        yield return ReplayLongLegAndTurn(TurnDirection.CounterClockwise);
    }

    // The leader fixture supplies observed geometry; the follower uses real Movement Update.
    private IEnumerator ReplayLongLegAndTurn(TurnDirection direction)
    {
        originalTimeScale = Time.timeScale;
        acceleratedSimulation = true;
        Time.timeScale = 8f;
        Ship leader = CreateShip(Vector3.zero, 0f);
        leader.Speed.SetPlayerStopSpeedCap(0f);
        Ship follower = CreateShip(Vector3.zero, 4f);
        SetPrivate(follower.Speed, "baseMaxSpeed", 6f);
        SetPrivate(follower.Speed, "polarTargetSpeed", 6f);
        SetPrivate(follower.Speed, "accelerationTimeConstant", 0.5f);
        SetPrivate(follower.Speed, "naturalDragTimeConstant", 0.5f);
        SetPrivate(follower.Turning, "maxTurnRate", 8f);
        SetPrivate(follower.Turning, "rudderResponse", 360f);
        SetPrivate(follower.Heading, "rudderEaseAngle", 10f);
        var navigation = follower.Root.AddComponent<ShipFollowNavigationController>();
        Assert.That(follower.Follow.TryBeginFollowRelationship(leader.Follow), Is.True);
        for (int z = 10; z <= 200; z += 10) Record(leader, Vector3.forward * z, 0f);
        Vector3 straightEnd = leader.Root.transform.position;
        float sign = direction == TurnDirection.Clockwise ? 1f : -1f;
        Vector3 turnEnd = Vector3.zero;
        for (int degrees = 15; degrees <= 60; degrees += 15)
        {
            float radians = degrees * Mathf.Deg2Rad;
            turnEnd = new Vector3(sign * 45f * (1f - Mathf.Cos(radians)), 0f,
                straightEnd.z + 45f * Mathf.Sin(radians));
            Record(leader, turnEnd, sign * degrees);
        }
        Vector3 forward = Quaternion.Euler(0f, sign * 60f, 0f) * Vector3.forward;
        for (int meters = 30; meters <= 180; meters += 30)
            Record(leader, turnEnd + forward * meters, sign * 60f);
        ShipFollowTurnEvent recordedTurn = leader.Recorder.TurnEvents[0];
        Assert.That(recordedTurn.IsComplete, Is.True);
        Assert.That(recordedTurn.Direction, Is.EqualTo(direction));
        Assert.That(leader.Recorder.TryGetSampleAtDistance(recordedTurn.StartDistance,
            out ShipFollowTrailSample turnStart), Is.True);
        Assert.That(Vector3.Distance(turnStart.WorldPosition, straightEnd), Is.LessThan(0.1f));
        Assert.That(recordedTurn.StartDistance, Is.EqualTo(200d).Within(DistanceTolerance));
        float recordingInterval = GetPrivateFloat(leader.Recorder, "sampleDistanceThreshold");
        float arrivalTolerance = GetPrivateFloat(navigation, "trailArrivalTolerance");
        float projectionLookAhead = GetPrivateFloat(navigation, "straightLookAhead");
        Assert.That(recordingInterval, Is.GreaterThan(0f));
        double deadline = Time.timeAsDouble + 180d;
        double realDeadline = Time.realtimeSinceStartupAsDouble + 45d;
        double previousProgress = navigation.TrailCursor;
        Vector3 previousPosition = follower.Root.transform.position;
        double previousTime = Time.timeAsDouble;
        float previousFrameDelta = Time.deltaTime;
        float previousSpeed = follower.Speed.CourseSpeed;
        bool reachedTurnRegion = false;
        bool issuedSameDirection = false;
        bool turnCompleted = false;
        bool consumedRecordedTurn = false;

        void AssertPhysicalProgress()
        {
            string details = DescribeReplay(leader, follower, navigation);
            Assert.That(navigation.TrailCursor, Is.GreaterThanOrEqualTo(previousProgress), details);
            bool semanticHandoff = issuedSameDirection && !consumedRecordedTurn
                && !navigation.ActiveTurn.HasValue;
            if (semanticHandoff)
            {
                Assert.That(follower.Planner.IsActive, Is.False, details);
                Assert.That(follower.Heading.IsActive, Is.False, details);
                Assert.That(previousProgress, Is.LessThanOrEqualTo(recordedTurn.StartDistance + DistanceTolerance), details);
                Assert.That(navigation.TrailCursor, Is.EqualTo(recordedTurn.EndDistance).Within(DistanceTolerance),
                    "Successful recorded turn completion consumes exactly its semantic interval. " + details);
                consumedRecordedTurn = true;
            }
            else
                Assert.That(navigation.TrailCursor - previousProgress,
                    Is.LessThanOrEqualTo(projectionLookAhead + DistanceTolerance),
                    "A geometric projection must stay within its forward look-ahead. " + details);
            // Account for either frame's delta when coroutine/Update ordering crosses a frame boundary.
            float frameTime = (float)System.Math.Max(Time.timeAsDouble - previousTime,
                Mathf.Max(previousFrameDelta, Time.deltaTime));
            float possibleMotion = Mathf.Max(previousSpeed, follower.Speed.CourseSpeed) * frameTime;
            Assert.That(Vector3.Distance(previousPosition, follower.Root.transform.position),
                Is.LessThanOrEqualTo(possibleMotion + 0.01f),
                "Root movement must come from physical propulsion, without a snap. " + details);
            if (!semanticHandoff && navigation.TrailCursor > previousProgress + DistanceTolerance)
            {
                Assert.That(leader.Recorder.TryGetSampleAtDistance(navigation.TrailCursor,
                    out ShipFollowTrailSample consumed), Is.True, details);
                Assert.That(ShipFollowReplayMath.HorizontalDistance(follower.Root.transform.position,
                    consumed.WorldPosition), Is.LessThanOrEqualTo(arrivalTolerance + possibleMotion + 0.01f),
                    "Geometric cursor advancement must match nearby recorded geometry. " + details);
            }
            Assert.That(follower.Follow.ConsumedTrailDistance, Is.EqualTo(navigation.TrailCursor), details);
            previousProgress = navigation.TrailCursor;
            previousPosition = follower.Root.transform.position;
            previousTime = Time.timeAsDouble;
            previousFrameDelta = Time.deltaTime;
            previousSpeed = follower.Speed.CourseSpeed;
            AssertPendingTurnBoundary(leader, follower, navigation);
        }

        // First prove the recorded event was handled by this ship's real maneuver owner.
        while (!turnCompleted && Time.timeAsDouble < deadline
            && Time.realtimeSinceStartupAsDouble < realDeadline)
        {
            AssertPhysicalProgress();
            if (navigation.State == ShipFollowNavigationController.ReplayState.ReplayingTurn)
            {
                reachedTurnRegion |= Vector3.Distance(follower.Root.transform.position,
                    turnStart.WorldPosition) < 20f;
                issuedSameDirection |= follower.Planner.PlannedTurnDirection == direction;
                Assert.That(follower.Planner.gameObject, Is.SameAs(follower.Root));
            }
            if (issuedSameDirection && !navigation.ActiveTurn.HasValue
                && !follower.Planner.IsActive && !follower.Heading.IsActive)
                turnCompleted = true;
            if (!turnCompleted) yield return null;
        }
        AssertPhysicalProgress();
        Assert.That(reachedTurnRegion, Is.True,
            "Follower must enter the recorded corner before replaying it. " + DescribeReplay(leader, follower, navigation));
        Assert.That(issuedSameDirection, Is.True, DescribeReplay(leader, follower, navigation));
        Assert.That(turnCompleted, Is.True,
            "Recorded maneuver must complete and release its event before geometric replay. "
            + DescribeReplay(leader, follower, navigation));
        Assert.That(consumedRecordedTurn, Is.True, DescribeReplay(leader, follower, navigation));
        Assert.That(navigation.TrailCursor, Is.GreaterThanOrEqualTo(recordedTurn.EndDistance),
            DescribeReplay(leader, follower, navigation));
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(sign * 60f, follower.Root.transform.eulerAngles.y)),
            Is.LessThan(20f), DescribeReplay(leader, follower, navigation));

        double postTurnBaseline = navigation.TrailCursor;
        double requiredProgress = System.Math.Max(recordedTurn.EndDistance, postTurnBaseline)
            + recordingInterval;
        Vector3 handoffPosition = follower.Root.transform.position;
        double? firstResumedTarget = null;
        bool targetedLaterGeometry = false;
        // One recording interval beyond the event proves replay resumed; no catch-up distance is required.
        // Each phase retains the same watchdog, so approach/turn time does not consume the replay budget.
        deadline = Time.timeAsDouble + 180d;
        realDeadline = Time.realtimeSinceStartupAsDouble + 45d;
        while ((navigation.TrailCursor <= requiredProgress + DistanceTolerance || !targetedLaterGeometry)
            && Time.timeAsDouble < deadline && Time.realtimeSinceStartupAsDouble < realDeadline)
        {
            yield return null;
            AssertPhysicalProgress();
            if (!navigation.HasReplayTarget) continue;
            Assert.That(navigation.ReplayTargetDistance, Is.GreaterThan(recordedTurn.EndDistance),
                "A completed turn interval must not be targeted again. " + DescribeReplay(leader, follower, navigation));
            Assert.That(leader.Recorder.TryGetSampleAtDistance(navigation.ReplayTargetDistance,
                out ShipFollowTrailSample target), Is.True, DescribeReplay(leader, follower, navigation));
            Assert.That(Vector3.Distance(navigation.ReplayTarget, target.WorldPosition), Is.LessThan(0.01f),
                "Resumed destinations must use recorded trail geometry.");
            if (!firstResumedTarget.HasValue) firstResumedTarget = navigation.ReplayTargetDistance;
            targetedLaterGeometry |= navigation.ReplayTargetDistance
                > System.Math.Max(recordedTurn.EndDistance, firstResumedTarget.Value)
                    + recordingInterval + DistanceTolerance;
        }
        string resumeDetails = $"Turn={recordedTurn.StartDistance:R}-{recordedTurn.EndDistance:R}, "
            + $"HandoffCursor={postTurnBaseline:R}, RequiredProgress>{requiredProgress:R}, "
            + $"FirstResumedTarget={firstResumedTarget}; " + DescribeReplay(leader, follower, navigation);
        Assert.That(navigation.TrailCursor, Is.GreaterThan(requiredProgress + DistanceTolerance),
            "Follower must consume a recording interval beyond the completed event and handoff baseline. " + resumeDetails);
        Assert.That(targetedLaterGeometry, Is.True,
            "Follower must advance its replay destination beyond the completed event and first resumed target. " + resumeDetails);
        Assert.That(Vector3.Distance(handoffPosition, follower.Root.transform.position),
            Is.GreaterThan(recordingInterval), "Post-turn replay must include real Root translation. " + resumeDetails);
        Assert.That(follower.Follow.ConsumedTrailDistance, Is.EqualTo(navigation.TrailCursor));
        Assert.That(follower.Speed.CurrentSpeed, Is.LessThanOrEqualTo(follower.Speed.AvailableTargetSpeed + 0.1f));
    }

    private static void AssertPendingTurnBoundary(Ship leader, Ship follower,
        ShipFollowNavigationController navigation)
    {
        if (!navigation.ActiveTurn.HasValue) return;
        double boundary = System.Math.Max(follower.Follow.FollowStartLeaderDistance,
            navigation.ActiveTurn.Value.StartDistance);
        if (navigation.TrailCursor > boundary + DistanceTolerance
            || (navigation.HasReplayTarget && navigation.ReplayTargetDistance > boundary + DistanceTolerance))
            Assert.Fail("Replay must not pass a pending recorded turn boundary. "
                + DescribeReplay(leader, follower, navigation));
    }

    private static string DescribeReplay(Ship leader, Ship follower,
        ShipFollowNavigationController navigation)
    {
        ShipFollowTurnEvent? active = navigation.ActiveTurn;
        string turn = active.HasValue
            ? $"#{active.Value.Sequence} {active.Value.Direction} {active.Value.StartDistance:R}-{active.Value.EndDistance:R}, complete={active.Value.IsComplete}"
            : "none";
        return $"State={navigation.State}, EntryReached={navigation.EntryReached}, Turn={turn}; "
            + $"PlannerActive={follower.Planner.IsActive}, Maneuver={follower.Planner.CurrentManeuver}, HeadingActive={follower.Heading.IsActive}; "
            + $"Cursor={navigation.TrailCursor:R}, Consumed={follower.Follow.ConsumedTrailDistance:R}, Start={follower.Follow.FollowStartLeaderDistance:R}, Head={leader.Recorder.HeadDistance:R}; "
            + $"Root={follower.Root.transform.position.ToString("F4")}, Yaw={follower.Root.transform.eulerAngles.y}, FrozenEntry={follower.Follow.FollowStartSample.WorldPosition.ToString("F4")}; "
            + $"HasReplayTarget={navigation.HasReplayTarget}, ReplayTargetDistance={navigation.ReplayTargetDistance:R}, Speed={follower.Speed.CurrentSpeed}, Cap={follower.Speed.FollowSpeedCap}";
    }

    private Ship CreateShip(Vector3 position, float currentSpeed)
    {
        GameObject root = new("Follow PlayMode Root");
        created.Add(root);
        root.transform.position = position;
        Ship ship = new(root);
        ship.Wind.windFromDegrees = 90f;
        SailPolarProfile polar = ScriptableObject.CreateInstance<SailPolarProfile>();
        created.Add(polar);
        polar.polarCurve = AnimationCurve.Constant(0f, 180f, 1f);
        SetPrivate(ship.Speed, "globalWind", ship.Wind);
        SetPrivate(ship.Speed, "sailPolarProfile", polar);
        SetPrivate(ship.Speed, "currentSpeed", currentSpeed);
        SetPrivate(ship.Speed, "polarTargetSpeed", 4f);
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

    private static void Record(Ship ship, Vector3 position, float heading)
    {
        ship.Root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, heading, 0f));
        Assert.That(ship.Recorder.CaptureCurrentPose(ship.Recorder.HeadSample.Timestamp + 1d), Is.True);
    }

    private static void SetPrivate(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        field.SetValue(target, value);
    }

    private static float GetPrivateFloat(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        return (float)field.GetValue(target);
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
