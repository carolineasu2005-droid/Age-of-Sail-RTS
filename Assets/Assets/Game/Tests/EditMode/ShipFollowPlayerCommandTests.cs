using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class ShipFollowPlayerCommandTests
{
    private readonly List<GameObject> created = new();
    private ShipSelectionManager selection;
    private ShipCommandDispatcher dispatcher;
    private FormationCommandController formation;
    private ShipPlayerCommandInput input;
    private Ship follower;
    private Ship leader;
    private Ship hostile;

    [SetUp]
    public void SetUp()
    {
        GameObject host = CreateObject("Player Commands");
        GlobalWind wind = host.AddComponent<GlobalWind>();
        wind.windFromDegrees = 90f;
        selection = host.AddComponent<ShipSelectionManager>();
        dispatcher = host.AddComponent<ShipCommandDispatcher>();
        formation = host.AddComponent<FormationCommandController>();
        input = host.AddComponent<ShipPlayerCommandInput>();
        SetPrivate(dispatcher, "selectionManager", selection);
        SetPrivate(dispatcher, "formationCommandController", formation);
        SetPrivate(formation, "selectionManager", selection);
        SetPrivate(formation, "commandDispatcher", dispatcher);
        SetPrivate(formation, "globalWind", wind);
        SetPrivate(input, "selectionManager", selection);
        SetPrivate(input, "commandDispatcher", dispatcher);
        SetPrivate(input, "formationCommandController", formation);
        SetPrivate(input, "commandCamera", null);
        InvokePrivate(formation, "OnDisable");
        InvokePrivate(formation, "OnEnable");
        InvokePrivate(input, "OnDisable");
        InvokePrivate(input, "OnEnable");
        follower = CreateShip(0, Vector3.zero);
        leader = CreateShip(0, Vector3.forward * 150f);
        hostile = CreateShip(1, Vector3.right * 150f);
        selection.SelectSingle(follower.Destination);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ShipContext_RoutesFriendlyToFollowAndHostileToManualTarget(bool isHostile)
    {
        follower.State.SetAutoFireEnabled(true);
        ClickShip(isHostile ? hostile.Root : leader.Root);
        Assert.That(follower.State.ManualTarget, isHostile ? Is.SameAs(hostile.Root) : Is.Null);
        Assert.That(follower.Follow.IsFollowing, Is.EqualTo(!isHostile));
        Assert.That(follower.State.AutoFireEnabled, Is.EqualTo(!isHostile));
        Assert.That(dispatcher.DispatchSequence, Is.EqualTo(isHostile ? 0 : 1));
        Assert.That(follower.Destination.HasDestination, Is.False);
    }

    [Test]
    public void SelfShipContext_IsConsumedWithoutAnyCommand()
    {
        ClickShip(follower.Root);
        AssertNoContextCommand();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void UnknownRelationship_IsConsumedWithoutWorldFallthrough(bool missingAffiliation)
    {
        if (missingAffiliation) Object.DestroyImmediate(leader.Affiliation);
        else SetPrivate(leader.Affiliation, "teamId", -1);
        ClickShip(leader.Root);
        AssertNoContextCommand();
    }

    [Test]
    public void FriendlyMultiSelection_IsConsumedWithoutDestinationOrFormation()
    {
        selection.AddSelection(CreateShip(0, Vector3.right * 20f).Destination);
        ClickShip(leader.Root);
        AssertNoContextCommand();
        Assert.That(dispatcher.TryStartFollow(leader.Root, out var result), Is.False);
        Assert.That(result.Failure, Is.EqualTo(ShipCommandDispatcher.FollowFailure.MultipleSelection));
    }

    [Test]
    public void FriendlyCycle_IsConsumedWithoutWorldFallthrough()
    {
        Assert.That(leader.Follow.TryBeginFollowRelationship(follower.Follow), Is.True);
        ClickShip(leader.Root);
        AssertNoContextCommand();
        Assert.That(leader.Follow.FollowTarget, Is.SameAs(follower.Follow));
    }

    [Test]
    public void FriendlyMissingFollowComponents_IsConsumedWithoutWorldFallthrough()
    {
        Object.DestroyImmediate(leader.Recorder);
        ClickShip(leader.Root);
        AssertNoContextCommand();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RejectedHostileManualTarget_HasNoWorldFallthrough(bool multipleSelection)
    {
        if (multipleSelection) selection.AddSelection(leader.Destination);
        else hostile.State.enabled = false;
        ClickShip(hostile.Root);
        AssertNoContextCommand();
    }

    [Test]
    public void CapturedShipDestroyedBeforeRelease_RemainsConsumed()
    {
        CaptureClick(leader.Root);
        Object.DestroyImmediate(leader.Root);
        InvokePrivate(input, "CompleteDestinationGesture");
        AssertNoContextCommand();
    }

    [Test]
    public void ChildHit_ResolvesAuthoritativeComponentRoot()
    {
        GameObject child = CreateObject("Arbitrary Child");
        child.transform.SetParent(leader.Root.transform, false);
        Assert.That(input.TryHandleShipContext(child), Is.True);
        Assert.That(follower.Follow.FollowTarget, Is.SameAs(leader.Follow));
    }

    [Test]
    public void HostileManualTarget_DisablesAutoFireAndPreservesFollow()
    {
        StartFollowing();
        follower.State.SetAutoFireEnabled(true);
        int sequence = dispatcher.DispatchSequence;
        ClickShip(hostile.Root);
        Assert.That(follower.State.ManualTarget, Is.SameAs(hostile.Root));
        Assert.That(follower.State.AutoFireEnabled, Is.False);
        Assert.That(follower.Follow.FollowTarget, Is.SameAs(leader.Follow));
        Assert.That(follower.Speed.FollowSpeedCapActive, Is.True);
        Assert.That(dispatcher.DispatchSequence, Is.EqualTo(sequence));
    }

    [Test]
    public void FollowStart_PreservesManualTarget()
    {
        Assert.That(input.TryAssignManualTarget(hostile.Root), Is.True);
        StartFollowing();
        Assert.That(follower.State.ManualTarget, Is.SameAs(hostile.Root));
        Assert.That(follower.State.AutoFireEnabled, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FollowStart_PreservesAutoFireState(bool enabled)
    {
        follower.State.SetAutoFireEnabled(enabled);
        StartFollowing();
        Assert.That(follower.State.AutoFireEnabled, Is.EqualTo(enabled));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void DirectManualTarget_RejectsFriendlyAndUnknownWithoutChangingCombat(int targetTeam)
    {
        Assert.That(input.TryAssignManualTarget(hostile.Root), Is.True);
        SetPrivate(leader.Affiliation, "teamId", targetTeam);
        Assert.That(input.TryAssignManualTarget(leader.Root), Is.False);
        Assert.That(follower.State.ManualTarget, Is.SameAs(hostile.Root));
        Assert.That(follower.State.AutoFireEnabled, Is.False);
    }

    [Test]
    public void AcceptedFollow_ReplacesDestinationPlannerAndStopButRetainsSpeedOrder()
    {
        follower.Destination.SetDestination(new Vector3(50f, 0f, 50f),
            ShipDestinationController.TurnSelectionMode.Auto, WindNavigationAssistMode.Manual);
        Assert.That(follower.Planner.IsActive, Is.True);
        follower.Speed.SetPlayerStopSpeedCap(0f);
        follower.Speed.DecreasePlayerSpeedOrder();
        StartFollowing();
        Assert.That(follower.Destination.HasDestination, Is.False);
        Assert.That(follower.Planner.IsActive, Is.False);
        Assert.That(follower.Heading.IsActive, Is.False);
        Assert.That(follower.Speed.IsPlayerStopped, Is.False);
        Assert.That(follower.Speed.PlayerSpeedOrderNormalized, Is.EqualTo(0.75f));
        Assert.That(follower.Speed.FollowSpeedCapActive, Is.True);
        Assert.That(dispatcher.DispatchSequence, Is.EqualTo(1));
        Assert.That(dispatcher.LastDispatchResult, Is.EqualTo(ShipCommandDispatcher.DispatchResult.Follow));
    }

    [Test]
    public void AcceptedFollow_ClearsPendingGroupOrder()
    {
        selection.AddSelection(CreateShip(0, Vector3.right * 20f).Destination);
        dispatcher.DispatchDestination(Vector3.forward * 300f,
            ShipDestinationController.TurnSelectionMode.Auto, WindNavigationAssistMode.Manual);
        Assert.That(dispatcher.GroupCommandPending, Is.True);
        selection.SelectSingle(follower.Destination);
        int before = dispatcher.DispatchSequence;
        StartFollowing();
        Assert.That(dispatcher.GroupCommandPending, Is.False);
        Assert.That(dispatcher.DispatchSequence, Is.EqualTo(before + 1));
    }

    [TestCase(ShipCommandDispatcher.FollowFailure.NoSelection)]
    [TestCase(ShipCommandDispatcher.FollowFailure.MultipleSelection)]
    [TestCase(ShipCommandDispatcher.FollowFailure.InvalidFollower)]
    [TestCase(ShipCommandDispatcher.FollowFailure.InvalidTarget)]
    [TestCase(ShipCommandDispatcher.FollowFailure.Self)]
    [TestCase(ShipCommandDispatcher.FollowFailure.NotFriendly)]
    [TestCase(ShipCommandDispatcher.FollowFailure.UnknownRelationship)]
    [TestCase(ShipCommandDispatcher.FollowFailure.Cycle)]
    [TestCase(ShipCommandDispatcher.FollowFailure.MissingFollowComponents)]
    [TestCase(ShipCommandDispatcher.FollowFailure.TargetInvalidLifecycle)]
    [TestCase(ShipCommandDispatcher.FollowFailure.DispatcherUnavailable)]
    public void FollowPreflightFailure_PreservesExistingMovement(ShipCommandDispatcher.FollowFailure expected)
    {
        follower.Destination.SetDestination(new Vector3(50f, 0f, 50f),
            ShipDestinationController.TurnSelectionMode.Auto, WindNavigationAssistMode.Manual);
        follower.Speed.SetPlayerStopSpeedCap(0f);
        GameObject target = leader.Root;
        switch (expected)
        {
            case ShipCommandDispatcher.FollowFailure.NoSelection: selection.ClearSelection(); break;
            case ShipCommandDispatcher.FollowFailure.MultipleSelection: selection.AddSelection(hostile.Destination); break;
            case ShipCommandDispatcher.FollowFailure.InvalidFollower: follower.Destination.enabled = false; break;
            case ShipCommandDispatcher.FollowFailure.InvalidTarget: target = null; break;
            case ShipCommandDispatcher.FollowFailure.Self: target = follower.Root; break;
            case ShipCommandDispatcher.FollowFailure.NotFriendly: target = hostile.Root; break;
            case ShipCommandDispatcher.FollowFailure.UnknownRelationship: SetPrivate(leader.Affiliation, "teamId", -1); break;
            case ShipCommandDispatcher.FollowFailure.Cycle: leader.Follow.TryBeginFollowRelationship(follower.Follow); break;
            case ShipCommandDispatcher.FollowFailure.MissingFollowComponents: follower.Navigation.enabled = false; break;
            case ShipCommandDispatcher.FollowFailure.TargetInvalidLifecycle:
                CombatLifecycleTestUtility.SetLifecycleThroughIntegrityLoss(leader.Root, ShipCombatLifecycleState.Sinking); break;
            case ShipCommandDispatcher.FollowFailure.DispatcherUnavailable: dispatcher.enabled = false; break;
        }
        int plannerSequence = follower.Planner.CommandSequence;
        Assert.That(dispatcher.TryStartFollow(target, out var result), Is.False);
        Assert.That(result.Accepted, Is.False);
        Assert.That(result.Failure, Is.EqualTo(expected));
        Assert.That(follower.Destination.HasDestination, Is.True);
        Assert.That(follower.Planner.IsActive, Is.True);
        Assert.That(follower.Planner.CommandSequence, Is.EqualTo(plannerSequence));
        Assert.That(follower.Speed.IsPlayerStopped, Is.True);
        Assert.That(dispatcher.DispatchSequence, Is.Zero);
    }

    [Test]
    public void NonFiniteTargetPose_PreflightPreservesExistingMovement()
    {
        follower.Destination.SetDestination(new Vector3(50f, 0f, 50f),
            ShipDestinationController.TurnSelectionMode.Auto, WindNavigationAssistMode.Manual);
        SetPrivate(leader.Speed, "courseSpeed", float.NaN);
        int plannerSequence = follower.Planner.CommandSequence;
        Assert.That(dispatcher.TryStartFollow(leader.Root, out var result), Is.False);
        Assert.That(result.Failure, Is.EqualTo(ShipCommandDispatcher.FollowFailure.InvalidTarget));
        Assert.That(follower.Destination.HasDestination, Is.True);
        Assert.That(follower.Planner.CommandSequence, Is.EqualTo(plannerSequence));
        Assert.That(follower.Planner.IsActive, Is.True);
        Assert.That(leader.Recorder.SubscriberCount, Is.Zero);
        Assert.That(dispatcher.DispatchSequence, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FollowPreflight_CancelsFormationOnlyAfterAcceptance(bool valid)
    {
        Ship peer = CreateShip(0, Vector3.right * 20f);
        Assert.That(formation.CaptureCurrentFormation(new[] { follower.Destination, peer.Destination }), Is.True);
        follower.Destination.SetDestination(new Vector3(50f, 0f, 50f),
            ShipDestinationController.TurnSelectionMode.Auto, WindNavigationAssistMode.Manual);
        Assert.That(dispatcher.TryStartFollow(valid ? leader.Root : hostile.Root, out _), Is.EqualTo(valid));
        Assert.That(formation.IsActive, Is.EqualTo(!valid));
        Assert.That(follower.Destination.HasDestination, Is.EqualTo(!valid));
        Assert.That(follower.Planner.IsActive, Is.EqualTo(!valid));
        Assert.That(follower.Follow.IsFollowing, Is.EqualTo(valid));
    }

    [Test]
    public void FollowOnUnrelatedShip_PreservesFormationAcrossEventAndUpdate()
    {
        Ship first = CreateShip(0, Vector3.right * 100f);
        Ship second = CreateShip(0, Vector3.right * 140f);
        Assert.That(formation.CaptureCurrentFormation(new[] { first.Destination, second.Destination }), Is.True);
        SetPrivate(formation, "formationState", FormationCommandController.FormationState.Moving);
        StartFollowing();
        InvokePrivate(formation, "Update");
        Assert.That(formation.ContainsActiveMember(first.Destination), Is.True);
        Assert.That(formation.ContainsActiveMember(second.Destination), Is.True);
        Assert.That(follower.Follow.IsFollowing, Is.True);
        // Subsequent existing non-Follow sequence replacement still has its original effect.
        dispatcher.DispatchDestination(Vector3.forward * 300f,
            ShipDestinationController.TurnSelectionMode.Auto, WindNavigationAssistMode.Manual);
        Assert.That(formation.IsActive, Is.False);
    }

    [Test]
    public void ValidReplacement_ResubscribesAndInvalidReplacementPreservesCurrentTarget()
    {
        StartFollowing();
        Ship replacement = CreateShip(0, Vector3.forward * 300f);
        Assert.That(dispatcher.TryStartFollow(replacement.Root, out _), Is.True);
        Assert.That(leader.Recorder.SubscriberCount, Is.Zero);
        Assert.That(replacement.Recorder.SubscriberCount, Is.EqualTo(1));
        Assert.That(follower.Follow.FollowTarget, Is.SameAs(replacement.Follow));
        int sequence = dispatcher.DispatchSequence;
        Assert.That(dispatcher.TryStartFollow(hostile.Root, out _), Is.False);
        Assert.That(follower.Follow.FollowTarget, Is.SameAs(replacement.Follow));
        Assert.That(dispatcher.DispatchSequence, Is.EqualTo(sequence));
    }

    [Test]
    public void SameTarget_IsAcceptedNoOpWithoutResettingReplayOrSequence()
    {
        StartFollowing();
        leader.Root.transform.position += Vector3.forward * 200f;
        leader.Recorder.CaptureCurrentPose(1d);
        follower.Root.transform.position = follower.Follow.FollowStartSample.WorldPosition;
        follower.Navigation.Tick(0d);
        follower.Root.transform.position += Vector3.forward * 20f;
        follower.Navigation.Tick(1d);
        double progress = follower.Navigation.TrailCursor;
        Assert.That(progress, Is.GreaterThan(0d));
        int version = follower.Follow.RelationshipVersion;
        int sequence = dispatcher.DispatchSequence;
        Assert.That(dispatcher.TryStartFollow(leader.Root, out var result), Is.True);
        Assert.That(result.IsNoOp, Is.True);
        Assert.That(follower.Navigation.TrailCursor, Is.EqualTo(progress));
        Assert.That(follower.Follow.RelationshipVersion, Is.EqualTo(version));
        Assert.That(dispatcher.DispatchSequence, Is.EqualTo(sequence));
    }

    [Test]
    public void DeselectAndReselect_PreservesFollow()
    {
        StartFollowing();
        int version = follower.Follow.RelationshipVersion;
        selection.ClearSelection();
        selection.SelectSingle(hostile.Destination);
        selection.SelectSingle(follower.Destination);
        Assert.That(follower.Follow.FollowTarget, Is.SameAs(leader.Follow));
        Assert.That(follower.Follow.RelationshipVersion, Is.EqualTo(version));
    }

    [Test]
    public void WorldClick_ReplacesFollowWithDestination()
    {
        StartFollowing();
        SetPrivate(input, "rightMouseDownWorldPosition", Vector3.forward * 500f);
        InvokePrivate(input, "CommitNormalDestinationClick");
        AssertFollowReleased();
        Assert.That(follower.Destination.HasDestination, Is.True);
        Assert.That(dispatcher.DispatchSequence, Is.EqualTo(2));
    }

    [Test]
    public void InvalidDestination_PreservesFollowAndDoesNotPublishSequence()
    {
        StartFollowing();
        dispatcher.DispatchDestination(new Vector3(float.NaN, 0f, 10f),
            ShipDestinationController.TurnSelectionMode.Auto, WindNavigationAssistMode.Manual);
        Assert.That(follower.Follow.IsFollowing, Is.True);
        Assert.That(dispatcher.DispatchSequence, Is.EqualTo(1));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void DirectedHeading_CancelsFollowOnlyAfterItsPreflight(bool valid)
    {
        StartFollowing();
        Assert.That(dispatcher.TryDispatchDirectedHeading(valid ? 45f : 350f,
            TurnDirection.Clockwise, out _), Is.EqualTo(valid));
        Assert.That(follower.Follow.IsFollowing, Is.EqualTo(!valid));
        Assert.That(follower.Speed.FollowSpeedCapActive, Is.EqualTo(!valid));
        Assert.That(dispatcher.DispatchSequence, Is.EqualTo(valid ? 2 : 1));
        if (valid) Assert.That(follower.Planner.CurrentManeuver, Is.EqualTo(ShipManeuverPlanner.ManeuverType.NormalTurn));
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void AcceptedSpeedStep_IncludingBounds_ReleasesFollow(bool increase, bool atBound)
    {
        int before = atBound ? (increase ? 4 : 1) : (increase ? 2 : 3);
        SetPrivate(follower.Speed, "playerSpeedOrderStep", before);
        StartFollowing();
        bool accepted = increase ? dispatcher.TryIncreaseSelectedSpeedOrder() : dispatcher.TryDecreaseSelectedSpeedOrder();
        Assert.That(accepted, Is.True);
        AssertFollowReleased();
        Assert.That(follower.Speed.PlayerSpeedOrderNormalized,
            Is.EqualTo(Mathf.Clamp(before + (increase ? 1 : -1), 1, 4) * 0.25f));
        Assert.That(follower.Speed.IsPlayerStopped, Is.False);
        Assert.That(dispatcher.DispatchSequence, Is.EqualTo(1)); // Existing speed-command sequence semantics.
    }

    [Test]
    public void RejectedSpeedStep_PreservesFollow()
    {
        StartFollowing();
        selection.AddSelection(leader.Destination);
        Assert.That(dispatcher.TryIncreaseSelectedSpeedOrder(), Is.False);
        Assert.That(follower.Follow.IsFollowing, Is.True);
        Assert.That(follower.Speed.FollowSpeedCapActive, Is.True);
    }

    [Test]
    public void StopSelectedShips_ReleasesAllSelectedFollowAuthorityThenAppliesStop()
    {
        StartFollowing();
        Ship second = CreateShip(0, Vector3.right * 20f);
        selection.SelectSingle(second.Destination);
        Assert.That(dispatcher.TryStartFollow(leader.Root, out _), Is.True);
        selection.AddSelection(follower.Destination);
        dispatcher.DispatchStopSelectedShips();
        AssertFollowReleased();
        Assert.That(second.Follow.HasFollowIntent, Is.False);
        Assert.That(second.Speed.FollowSpeedCapActive, Is.False);
        Assert.That(follower.Speed.IsPlayerStopped, Is.True);
        Assert.That(second.Speed.IsPlayerStopped, Is.True);
        Assert.That(follower.Planner.IsActive, Is.False);
        Assert.That(dispatcher.GroupCommandPending, Is.False);
    }

    [TestCase("Placement")]
    [TestCase("SnapshotDestination")]
    [TestCase("GroupDestination")]
    [TestCase("Capture")]
    public void AcceptedFormation_ReplacesFollowOnCommandedMembers(string command)
    {
        StartFollowing();
        Ship second = CreateShip(0, Vector3.right * 20f);
        selection.SelectSingle(second.Destination);
        Assert.That(dispatcher.TryStartFollow(leader.Root, out _), Is.True);
        selection.AddSelection(follower.Destination);
        Assert.That(FormationGeometrySnapshot.TryCapture(selection.SelectedShips,
            selection.PrimarySelectedShip, out var snapshot), Is.True);
        switch (command)
        {
            case "Placement": dispatcher.DispatchFormationPlacement(Vector3.forward * 300f, 0f,
                snapshot, ShipDestinationController.TurnSelectionMode.Auto, WindNavigationAssistMode.Manual); break;
            case "SnapshotDestination": dispatcher.DispatchFormationDestination(Vector3.forward * 300f,
                snapshot, ShipDestinationController.TurnSelectionMode.Auto, WindNavigationAssistMode.Manual); break;
            case "GroupDestination": dispatcher.DispatchDestination(Vector3.forward * 300f,
                ShipDestinationController.TurnSelectionMode.Auto, WindNavigationAssistMode.Manual); break;
            case "Capture": Assert.That(formation.CaptureCurrentFormation(selection.SelectedShips), Is.True); break;
        }
        AssertFollowReleased();
        Assert.That(second.Follow.HasFollowIntent, Is.False);
        Assert.That(second.Speed.FollowSpeedCapActive, Is.False);
        if (command != "Capture") InvokePrivate(formation, "TryConsumePendingGroupCommand");
        Assert.That(formation.ContainsActiveMember(follower.Destination), Is.True);
        Assert.That(formation.ContainsActiveMember(second.Destination), Is.True);
    }

    [TestCase("MissingSnapshot")]
    [TestCase("InvalidHeading")]
    [TestCase("InactiveMember")]
    public void RejectedFormationPreflight_PreservesFollow(string failure)
    {
        StartFollowing();
        Ship second = CreateShip(0, Vector3.right * 20f);
        FormationGeometrySnapshot.TryCapture(new[] { follower.Destination, second.Destination },
            follower.Destination, out var snapshot);
        if (failure == "InactiveMember") second.Destination.enabled = false;
        dispatcher.DispatchFormationPlacement(Vector3.forward * 300f,
            failure == "InvalidHeading" ? float.NaN : 0f,
            failure == "MissingSnapshot" ? null : snapshot,
            ShipDestinationController.TurnSelectionMode.Auto, WindNavigationAssistMode.Manual);
        Assert.That(follower.Follow.IsFollowing, Is.True);
        Assert.That(follower.Speed.FollowSpeedCapActive, Is.True);
        Assert.That(dispatcher.GroupCommandPending, Is.False);
        Assert.That(dispatcher.DispatchSequence, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CapturedCombatMode_RejectionDoesNotBecomeFriendlyFollow(bool blindFire)
    {
        PointCameraAt(leader.Root);
        CaptureClick(leader.Root);
        if (blindFire)
        {
            SetPrivate(input, "blindFireArmed", true);
            SetPrivate(input, "blindFireArmedShipRoot", follower.Root);
            SetPrivate(input, "blindFireClickPending", true);
        }
        else
        {
            Assert.That(input.TryArmManualTarget(), Is.False); // Already captured gesture cannot arm.
            SetPrivate(input, "manualTargetArmed", true);
            SetPrivate(input, "manualTargetArmedShipRoot", follower.Root);
            SetPrivate(input, "manualTargetClickPending", true);
        }
        InvokePrivate(input, "CompleteDestinationGesture");
        AssertNoContextCommand();
        Assert.That(input.ManualTargetArmed, Is.False);
        Assert.That(input.BlindFireArmed, Is.False);
    }

    [Test]
    public void CapturedFormationPlacement_PrecedesShipContext()
    {
        StartFollowing();
        Ship second = CreateShip(0, Vector3.right * 20f);
        selection.AddSelection(second.Destination);
        FormationGeometrySnapshot.TryCapture(selection.SelectedShips, follower.Destination, out var snapshot);
        PointCameraAt(leader.Root);
        CaptureClick(leader.Root);
        SetPrivate(input, "rightMouseDownGeometrySnapshot", snapshot);
        SetPrivate(input, "formationPlacementPreviewActive", true);
        SetPrivate(input, "hasLastValidPreviewHeading", true);
        SetPrivate(input, "lastValidPreviewHeading", 45f);
        InvokePrivate(input, "CompleteDestinationGesture");
        AssertFollowReleased();
        Assert.That(dispatcher.GroupCommandPending, Is.True);
        Assert.That(dispatcher.PendingGroupHasExplicitFormationHeading, Is.True);
        Assert.That(dispatcher.DispatchSequence, Is.EqualTo(2));
        Assert.That(follower.State.ManualTarget, Is.Null);
    }

    [Test]
    public void Commands_AddNoPhysicalWritersAndNoAIConsumer()
    {
        Vector3 position = follower.Root.transform.position;
        Quaternion rotation = follower.Root.transform.rotation;
        float speed = follower.Speed.CurrentSpeed;
        StartFollowing();
        Assert.That(follower.Root.transform.position, Is.EqualTo(position));
        Assert.That(follower.Root.transform.rotation, Is.EqualTo(rotation));
        Assert.That(follower.Speed.CurrentSpeed, Is.EqualTo(speed));
        string scripts = Path.Combine(Application.dataPath, "Assets/Game/Scripts");
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(file);
            if (!name.StartsWith("CombatAI", StringComparison.Ordinal)
                && !name.StartsWith("ShipCombatAI", StringComparison.Ordinal)) continue;
            string source = File.ReadAllText(file);
            Assert.That(source, Does.Not.Contain("ShipFollow"), file);
            Assert.That(source, Does.Not.Contain("TryStartFollow"), file);
            Assert.That(source, Does.Not.Contain(".CancelFollow("), file);
        }
        foreach (string file in new[] { "Command/ShipCommandDispatcher.cs", "Sailing/ShipPlayerCommandInput.cs" })
            Assert.That(Regex.IsMatch(File.ReadAllText(Path.Combine(scripts, file)),
                @"(?:transform\.(?:position|rotation|eulerAngles)|CurrentSpeed)\s*[+\-*/]?="), Is.False, file);
    }

    [TestCase(3)]
    [TestCase(4)]
    public void PlayerChain_EachReplayConsumesImmediateLeadersObservedTrail(int count)
    {
        Ship[] chain = new Ship[count];
        for (int i = 0; i < count; i++) chain[i] = CreateShip(0, Vector3.zero);
        for (int i = 1; i < count; i++)
        {
            selection.SelectSingle(chain[i].Destination);
            Assert.That(dispatcher.TryStartFollow(chain[i - 1].Root, out _), Is.True);
        }
        for (int leaderIndex = 0; leaderIndex < count - 1; leaderIndex++)
        {
            Ship immediateLeader = chain[leaderIndex];
            float lateral = leaderIndex == 0 ? 0f : leaderIndex == 1 ? 10f : -10f;
            for (int step = 1; step <= 12; step++)
            {
                immediateLeader.Root.transform.position = new Vector3(step * lateral, 0f, step * 20f);
                Assert.That(immediateLeader.Recorder.CaptureCurrentPose(step), Is.True);
            }
            Ship replay = chain[leaderIndex + 1];
            replay.Navigation.Tick(0d);
            Assert.That(replay.Follow.FollowTarget, Is.SameAs(immediateLeader.Follow));
            Assert.That(replay.Follow.TargetTrailRecorder, Is.SameAs(immediateLeader.Recorder));
            Assert.That(replay.Navigation.ReplayTargetDistance, Is.LessThanOrEqualTo(25d));
            Assert.That(replay.Navigation.HasReplayTarget, Is.True);
            if (leaderIndex > 0)
            {
                Assert.That(replay.Follow.TargetTrailRecorder, Is.Not.SameAs(chain[0].Recorder));
                Assert.That(Mathf.Sign(replay.Navigation.ReplayTarget.x), Is.EqualTo(Mathf.Sign(lateral)));
            }
            replay.Root.transform.position = replay.Navigation.ReplayTarget;
            replay.Navigation.Tick(1d);
            Assert.That(replay.Navigation.TrailCursor, Is.GreaterThan(0d));
            Assert.That(replay.Follow.ConsumedTrailDistance, Is.EqualTo(replay.Navigation.TrailCursor));
            Assert.That(replay.Navigation.TrailCursor, Is.LessThan(immediateLeader.Recorder.HeadDistance));
        }
        Ship shared = CreateShip(0, Vector3.zero);
        selection.SelectSingle(shared.Destination);
        Assert.That(dispatcher.TryStartFollow(chain[1].Root, out _), Is.True);
        Assert.That(shared.Follow.TargetTrailRecorder, Is.SameAs(chain[2].Follow.TargetTrailRecorder));
        Assert.That(chain[1].Recorder.SubscriberCount, Is.EqualTo(2));
        selection.SelectSingle(chain[0].Destination);
        Assert.That(dispatcher.TryStartFollow(chain[count - 1].Root, out var result), Is.False);
        Assert.That(result.Failure, Is.EqualTo(ShipCommandDispatcher.FollowFailure.Cycle));
        Assert.That(chain[0].Follow.HasFollowIntent, Is.False);
    }

    private void StartFollowing()
        => Assert.That(dispatcher.TryStartFollow(leader.Root, out _), Is.True);

    private void AssertFollowReleased()
    {
        Assert.That(follower.Follow.HasFollowIntent, Is.False);
        Assert.That(follower.Speed.FollowSpeedCapActive, Is.False);
    }

    private void AssertNoContextCommand()
    {
        Assert.That(follower.Follow.HasFollowIntent, Is.False);
        Assert.That(follower.State.ManualTarget, Is.Null);
        Assert.That(follower.Destination.HasDestination, Is.False);
        Assert.That(dispatcher.GroupCommandPending, Is.False);
        Assert.That(dispatcher.DispatchSequence, Is.Zero);
    }

    private void ClickShip(GameObject clicked)
    {
        CaptureClick(clicked);
        InvokePrivate(input, "CompleteDestinationGesture");
    }

    private void CaptureClick(GameObject clicked)
    {
        SetPrivate(input, "rightPlacementGestureActive", true);
        SetPrivate(input, "shipContextClickPending", true);
        SetPrivate(input, "shipContextClickedObject", clicked);
        SetPrivate(input, "rightMouseDownWorldPosition", new Vector3(300f, 0f, 300f));
    }

    private void PointCameraAt(GameObject target)
    {
        BoxCollider collider = target.AddComponent<BoxCollider>();
        collider.size = Vector3.one * 5f;
        Camera camera = CreateObject("Context Camera").AddComponent<Camera>();
        camera.transform.position = target.transform.position + new Vector3(0f, 20f, -30f);
        camera.transform.LookAt(target.transform.position);
        Vector3 screen = camera.WorldToScreenPoint(target.transform.position);
        SetPrivate(input, "commandCamera", camera);
        SetPrivate(input, "rightMouseDownScreenPosition", new Vector2(screen.x, screen.y));
        SetPrivate(input, "rightMouseCurrentScreenPosition", new Vector2(screen.x, screen.y));
        Physics.SyncTransforms();
    }

    private Ship CreateShip(int team, Vector3 position)
    {
        GameObject root = CreateObject("Movement Root");
        root.transform.position = position;
        Ship ship = new(root);
        ship.Wind.windFromDegrees = 90f;
        SetPrivate(ship.Affiliation, "teamId", team);
        SetPrivate(ship.Speed, "globalWind", ship.Wind);
        SetPrivate(ship.Speed, "polarTargetSpeed", 4f);
        SetPrivate(ship.Speed, "courseSpeed", 3f);
        SetPrivate(ship.Speed, "relativeWindAngleSigned", 90f);
        SetPrivate(ship.Planner, "globalWind", ship.Wind);
        SetPrivate(ship.Planner, "headingController", ship.Heading);
        SetPrivate(ship.Planner, "shipTacking", root.GetComponent<ShipTacking>());
        SetPrivate(ship.Planner, "shipWearing", root.GetComponent<ShipWearing>());
        SetPrivate(ship.Destination, "globalWind", ship.Wind);
        SetPrivate(ship.Destination, "maneuverPlanner", ship.Planner);
        InvokePrivate(ship.Navigation, "Awake");
        return ship;
    }

    private GameObject CreateObject(string name)
    {
        GameObject root = new(name);
        created.Add(root);
        return root;
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

    private sealed class Ship
    {
        public readonly GameObject Root;
        public readonly GlobalWind Wind;
        public readonly ShipDestinationController Destination;
        public readonly ShipSailingSpeed Speed;
        public readonly ShipHeadingController Heading;
        public readonly ShipManeuverPlanner Planner;
        public readonly ShipFollowController Follow;
        public readonly ShipFollowTrailRecorder Recorder;
        public readonly ShipFollowNavigationController Navigation;
        public readonly ShipCombatAffiliation Affiliation;
        public readonly ShipCombatState State;
        public Ship(GameObject root)
        {
            Root = root;
            Wind = root.AddComponent<GlobalWind>();
            Speed = root.AddComponent<ShipSailingSpeed>();
            root.AddComponent<ShipTurning>();
            Heading = root.AddComponent<ShipHeadingController>();
            root.AddComponent<ShipTacking>();
            root.AddComponent<ShipWearing>();
            Planner = root.AddComponent<ShipManeuverPlanner>();
            Destination = root.AddComponent<ShipDestinationController>();
            Recorder = root.AddComponent<ShipFollowTrailRecorder>();
            Follow = root.AddComponent<ShipFollowController>();
            Navigation = root.AddComponent<ShipFollowNavigationController>();
            Affiliation = root.AddComponent<ShipCombatAffiliation>();
            State = root.AddComponent<ShipCombatState>();
        }
    }
}
