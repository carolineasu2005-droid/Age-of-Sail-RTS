using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CombatAIMovementAdapterTests
{
    private const string AIProfilePath =
        "Assets/Assets/Game/Data/SO_CombatAI_Foundation.asset";
    private const string IntegrityProfilePath =
        "Assets/Assets/Game/Data/SO_ShipIntegrity_Foundation.asset";
    private readonly List<GameObject> created = new List<GameObject>();
    private CombatAIProfile profile;

    [SetUp]
    public void SetUp()
    {
        profile = AssetDatabase.LoadAssetAtPath<CombatAIProfile>(AIProfilePath);
        Assert.That(profile, Is.Not.Null);
        Assert.That(profile.IsMovementValid, Is.True);
        Assert.That(profile.BroadsideAlignmentLeadDistanceMeters, Is.EqualTo(40f));
        Assert.That(profile.DestinationUpdateThresholdMeters, Is.EqualTo(5f));
        Assert.That(profile.DesiredHeadingUpdateThresholdDegrees, Is.EqualTo(5f));
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        }
        created.Clear();
    }

    [Test]
    public void TooFar_UsesExistingDestinationCommand()
    {
        GameObject shooter = CreateShooter();
        GameObject target = CreateTarget(Vector3.right * 150f);
        CombatBroadsidePoseResult pose = Solve(shooter, target);
        ShipDestinationController destination = shooter.GetComponent<ShipDestinationController>();
        ShipManeuverPlanner planner = shooter.GetComponent<ShipManeuverPlanner>();
        CombatAIMovementAdapter adapter = new CombatAIMovementAdapter(destination);
        Vector3 originalPosition = shooter.transform.position;
        Quaternion originalRotation = shooter.transform.rotation;

        Assert.That(pose.MovementIntent, Is.EqualTo(CombatAIMovementIntent.CloseRange));
        Assert.That(adapter.TryApply(pose, target, profile), Is.True);
        Assert.That(destination.HasDestination, Is.True);
        Assert.That(GetDestination(destination).x, Is.EqualTo(80f).Within(0.001f));
        Assert.That(planner.CommandSequence, Is.EqualTo(1));
        Assert.That(shooter.transform.position, Is.EqualTo(originalPosition));
        Assert.That(shooter.transform.rotation, Is.EqualTo(originalRotation));
    }

    [Test]
    public void TooClose_CommandsRadialOpenRangeDestination()
    {
        GameObject shooter = CreateShooter();
        GameObject target = CreateTarget(Vector3.right * 30f);
        CombatBroadsidePoseResult pose = Solve(shooter, target);
        ShipDestinationController destination = shooter.GetComponent<ShipDestinationController>();

        Assert.That(pose.MovementIntent, Is.EqualTo(CombatAIMovementIntent.OpenRange));
        Assert.That(new CombatAIMovementAdapter(destination)
            .TryApply(pose, target, profile), Is.True);
        Assert.That(GetDestination(destination).x, Is.EqualTo(-40f).Within(0.001f));
        Assert.That(destination.HasDestination, Is.True);
    }

    [Test]
    public void InBandMisalignment_CommandsFortyMeterForwardLead()
    {
        GameObject shooter = CreateShooter();
        GameObject target = CreateTarget(Vector3.forward * 70f);
        CombatBroadsidePoseResult pose = Solve(shooter, target);
        ShipDestinationController destination = shooter.GetComponent<ShipDestinationController>();

        Assert.That(pose.MovementIntent,
            Is.EqualTo(CombatAIMovementIntent.AlignBroadside));
        Assert.That(new CombatAIMovementAdapter(destination)
            .TryApply(pose, target, profile), Is.True);
        Vector3 commanded = GetDestination(destination);
        Vector3 expectedForward = Quaternion.Euler(
            0f, pose.DesiredHeadingDegrees, 0f) * Vector3.forward;
        Vector3 lead = commanded - shooter.transform.position;
        Assert.That(lead.magnitude, Is.EqualTo(40f).Within(0.001f));
        Assert.That(lead.y, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(Vector3.Distance(lead, expectedForward * 40f),
            Is.LessThan(0.0001f));
    }

    [Test]
    public void RepeatedPoseWithinThresholds_DoesNotSpamMovement()
    {
        GameObject shooter = CreateShooter();
        GameObject target = CreateTarget(Vector3.right * 150f);
        ShipDestinationController destination = shooter.GetComponent<ShipDestinationController>();
        ShipManeuverPlanner planner = shooter.GetComponent<ShipManeuverPlanner>();
        CombatAIMovementAdapter adapter = new CombatAIMovementAdapter(destination);
        Assert.That(adapter.TryApply(Solve(shooter, target), target, profile), Is.True);
        int commands = planner.CommandSequence;
        Assert.That(adapter.TryApply(Solve(shooter, target), target, profile), Is.False);
        Assert.That(planner.CommandSequence, Is.EqualTo(commands));

        target.transform.position = Vector3.right * 154f;
        Assert.That(adapter.TryApply(Solve(shooter, target), target, profile), Is.False);
        Assert.That(planner.CommandSequence, Is.EqualTo(commands));

        target.transform.position = Vector3.right * 156f;
        Assert.That(adapter.TryApply(Solve(shooter, target), target, profile), Is.True);
        Assert.That(planner.CommandSequence, Is.EqualTo(commands + 1));
    }

    [Test]
    public void HeadingChangeBeyondFiveDegrees_UpdatesEvenWhenDestinationMovesLess()
    {
        GameObject shooter = CreateShooter();
        GameObject target = CreateTarget(Vector3.right * 30f);
        ShipDestinationController destination = shooter.GetComponent<ShipDestinationController>();
        ShipManeuverPlanner planner = shooter.GetComponent<ShipManeuverPlanner>();
        CombatAIMovementAdapter adapter = new CombatAIMovementAdapter(destination);
        Assert.That(adapter.TryApply(Solve(shooter, target), target, profile), Is.True);
        Vector3 firstDestination = GetDestination(destination);
        int commands = planner.CommandSequence;

        target.transform.position = Quaternion.Euler(0f, 7f, 0f)
            * (Vector3.right * 30f);
        CombatBroadsidePoseResult changed = Solve(shooter, target);
        Vector3 offset = changed.DesiredPositionWorld - firstDestination;
        offset.y = 0f;
        Assert.That(offset.magnitude, Is.LessThan(5f));
        Assert.That(adapter.TryApply(changed, target, profile), Is.True);
        Assert.That(planner.CommandSequence, Is.EqualTo(commands + 1));
    }

    [Test]
    public void TargetChange_AndCompletedCommand_AllowNewSubmission()
    {
        GameObject shooter = CreateShooter();
        GameObject first = CreateTarget(Vector3.right * 150f);
        GameObject second = CreateTarget(Vector3.right * 150f);
        ShipDestinationController destination = shooter.GetComponent<ShipDestinationController>();
        ShipManeuverPlanner planner = shooter.GetComponent<ShipManeuverPlanner>();
        CombatAIMovementAdapter adapter = new CombatAIMovementAdapter(destination);

        Assert.That(adapter.TryApply(Solve(shooter, first), first, profile), Is.True);
        Assert.That(adapter.TryApply(Solve(shooter, second), second, profile), Is.True);
        Assert.That(planner.CommandSequence, Is.EqualTo(2));
        destination.ClearDestination();
        Assert.That(adapter.TryApply(Solve(shooter, second), second, profile), Is.True);
        Assert.That(planner.CommandSequence, Is.EqualTo(3));
    }

    [Test]
    public void FirstBlockedTransition_AllowsOneReplacementWithoutSpam()
    {
        GameObject shooter = CreateShooter();
        GameObject target = CreateTarget(Vector3.right * 150f);
        ShipDestinationController destination = shooter.GetComponent<ShipDestinationController>();
        ShipManeuverPlanner planner = shooter.GetComponent<ShipManeuverPlanner>();
        CombatAIMovementAdapter adapter = new CombatAIMovementAdapter(destination);
        CombatBroadsidePoseResult pose = Solve(shooter, target);
        Assert.That(adapter.TryApply(pose, target, profile), Is.True);
        int commands = planner.CommandSequence;

        SerializedObject data = new SerializedObject(destination);
        data.FindProperty("navigationMode").enumValueIndex =
            (int)ShipDestinationController.NavigationMode.Blocked;
        data.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(adapter.TryApply(pose, target, profile), Is.True);
        Assert.That(planner.CommandSequence, Is.EqualTo(commands + 1));
        Assert.That(adapter.TryApply(pose, target, profile), Is.False);
    }

    [Test]
    public void InBandAlignedPose_DoesNotIssueNewDestination()
    {
        GameObject shooter = CreateShooter();
        GameObject target = CreateTarget(Vector3.right * 70f);
        ShipDestinationController destination = shooter.GetComponent<ShipDestinationController>();
        ShipManeuverPlanner planner = shooter.GetComponent<ShipManeuverPlanner>();
        CombatBroadsidePoseResult pose = Solve(shooter, target);
        Assert.That(pose.MovementIntent,
            Is.EqualTo(CombatAIMovementIntent.HoldCombatPose));
        Assert.That(new CombatAIMovementAdapter(destination)
            .TryApply(pose, target, profile), Is.False);
        Assert.That(destination.HasDestination, Is.False);
        Assert.That(planner.CommandSequence, Is.Zero);
    }

    [Test]
    public void InvalidMovementConfiguration_DoesNotInvalidateEarlierAIStages()
    {
        CombatAIProfile invalidMovement =
            ScriptableObject.CreateInstance<CombatAIProfile>();
        try
        {
            SerializedObject data = new SerializedObject(invalidMovement);
            data.FindProperty("broadsideAlignmentLeadDistanceMeters")
                .floatValue = 0f;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(invalidMovement.IsValid, Is.True);
            Assert.That(invalidMovement.IsPoseValid, Is.True);
            Assert.That(invalidMovement.IsMovementValid, Is.False);

            GameObject shooter = CreateShooter();
            GameObject target = CreateTarget(Vector3.forward * 70f);
            CombatAIMovementAdapter adapter = new CombatAIMovementAdapter(
                shooter.GetComponent<ShipDestinationController>());
            Assert.That(adapter.TryApply(Solve(shooter, target), target,
                invalidMovement), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(invalidMovement);
        }
    }

    [Test]
    public void ExternalLatestDestination_RemainsUntilTacticalRequestChanges()
    {
        GameObject shooter = CreateShooter();
        GameObject target = CreateTarget(Vector3.right * 150f);
        ShipDestinationController destination = shooter.GetComponent<ShipDestinationController>();
        CombatAIMovementAdapter adapter = new CombatAIMovementAdapter(destination);
        Assert.That(adapter.TryApply(Solve(shooter, target), target, profile), Is.True);

        Vector3 external = Vector3.forward * 200f;
        destination.SetDestination(external,
            ShipDestinationController.TurnSelectionMode.Auto,
            WindNavigationAssistMode.Assisted);
        Assert.That(adapter.TryApply(Solve(shooter, target), target, profile), Is.False);
        Assert.That(GetDestination(destination), Is.EqualTo(external));

        target.transform.position = Vector3.right * 160f;
        Assert.That(adapter.TryApply(Solve(shooter, target), target, profile), Is.True);
        Assert.That(GetDestination(destination), Is.Not.EqualTo(external));
    }

    [Test]
    public void PlayerStopCap_IsNeverClearedByAIAdapter()
    {
        GameObject shooter = CreateShooter();
        GameObject target = CreateTarget(Vector3.right * 150f);
        ShipSailingSpeed speed = shooter.GetComponent<ShipSailingSpeed>();
        ShipDestinationController destination = shooter.GetComponent<ShipDestinationController>();
        CombatAIMovementAdapter adapter = new CombatAIMovementAdapter(destination);
        speed.SetPlayerStopSpeedCap(0f);
        Assert.That(adapter.TryApply(Solve(shooter, target), target, profile), Is.False);
        Assert.That(destination.HasDestination, Is.False);
        Assert.That(speed.IsPlayerStopped, Is.True);

        speed.ClearPlayerStopSpeedCap();
        Assert.That(adapter.TryApply(Solve(shooter, target), target, profile), Is.True);
        Assert.That(destination.HasDestination, Is.True);
    }

    [Test]
    public void AssistedDestination_DelegatesUpwindRouteToNavigation()
    {
        GameObject windObject = new GameObject("Wind");
        created.Add(windObject);
        GlobalWind wind = windObject.AddComponent<GlobalWind>();
        wind.windFromDegrees = 0f;
        GameObject shooter = CreateShooter();
        ShipDestinationController destination = shooter.GetComponent<ShipDestinationController>();
        SetReference(destination, "globalWind", wind);
        SetReference(shooter.GetComponent<ShipManeuverPlanner>(), "globalWind", wind);
        GameObject target = CreateTarget(Vector3.forward * 150f);

        Assert.That(new CombatAIMovementAdapter(destination)
            .TryApply(Solve(shooter, target), target, profile), Is.True);
        Assert.That(destination.CurrentNavigationMode,
            Is.EqualTo(ShipDestinationController.NavigationMode.BeatingUpwind));
    }

    [Test]
    public void Controller_NoTargetAndCombatIncapable_LeaveNavigationUntouched()
    {
        GameObject shooter = CreateShooter();
        GameObject target = CreateTarget(Vector3.right * 150f);
        ShipDestinationController destination = shooter.GetComponent<ShipDestinationController>();
        ShipManeuverPlanner planner = shooter.GetComponent<ShipManeuverPlanner>();
        ShipCombatAIController controller = shooter.AddComponent<ShipCombatAIController>();
        SetReference(controller, "profile", profile);
        Physics.SyncTransforms();
        Assert.That(controller.Think(0f), Is.True);
        Assert.That(destination.HasDestination, Is.True);
        int commands = planner.CommandSequence;
        Vector3 issued = GetDestination(destination);

        SetTeam(target, 0);
        Assert.That(controller.Think(0.25f), Is.True);
        Assert.That(controller.State, Is.EqualTo(CombatAIState.NoTarget));
        Assert.That(planner.CommandSequence, Is.EqualTo(commands));
        Assert.That(GetDestination(destination), Is.EqualTo(issued));

        ShipIntegrity integrity = shooter.GetComponent<ShipIntegrity>();
        Assert.That(integrity.TryApplyIntegrityLoss(
            integrity.MaximumIntegrity * 0.8f, out _), Is.True);
        Assert.That(controller.Think(0.5f), Is.True);
        Assert.That(controller.State, Is.EqualTo(CombatAIState.CombatIncapable));
        Assert.That(planner.CommandSequence, Is.EqualTo(commands));
        Assert.That(GetDestination(destination), Is.EqualTo(issued));
    }

    [Test]
    public void Controller_SinkingTargetResetsPortPreferenceBeforeSolvingNewTarget()
    {
        GameObject shooter = CreateShooter();
        shooter.transform.rotation = Quaternion.Euler(0f, 85f, 0f);
        GameObject first = CreateTarget(Vector3.left * 150f);
        ShipDestinationController destination =
            shooter.GetComponent<ShipDestinationController>();
        ShipManeuverPlanner planner = shooter.GetComponent<ShipManeuverPlanner>();
        ShipCombatAIController controller =
            shooter.AddComponent<ShipCombatAIController>();
        SetReference(controller, "profile", profile);
        Physics.SyncTransforms();

        Assert.That(controller.Think(0f), Is.True);
        Assert.That(controller.CurrentTargetShipRoot, Is.SameAs(first));
        Assert.That(controller.LastPose.PreferredSide, Is.EqualTo(CombatSide.Port));
        Assert.That(GetDestination(destination).x,
            Is.EqualTo(-80f).Within(0.001f));
        int firstCommandSequence = planner.CommandSequence;

        GameObject second = CreateTarget(Vector3.right * 150f);
        Assert.That(CombatBroadsidePoseSolver.TrySolve(
            shooter, second, CombatSide.Port, profile,
            out CombatBroadsidePoseResult withOldPreference), Is.True);
        Assert.That(withOldPreference.PreferredSide, Is.EqualTo(CombatSide.Port));
        Assert.That(CombatBroadsidePoseSolver.TrySolve(
            shooter, second, null, profile,
            out CombatBroadsidePoseResult fresh), Is.True);
        Assert.That(fresh.PreferredSide, Is.EqualTo(CombatSide.Starboard));

        ShipIntegrity firstIntegrity = first.GetComponent<ShipIntegrity>();
        Assert.That(firstIntegrity.TryApplyIntegrityLoss(
            firstIntegrity.MaximumIntegrity, out _), Is.True);
        Assert.That(firstIntegrity.IsSinking, Is.True);
        Physics.SyncTransforms();

        Assert.That(controller.Think(0.25f), Is.True);
        Assert.That(controller.CurrentTargetShipRoot, Is.SameAs(second));
        Assert.That(controller.LastTargetChangeReason,
            Is.EqualTo(CombatAITargetChangeReason.Sinking));
        Assert.That(controller.LastPose.PreferredSide,
            Is.EqualTo(CombatSide.Starboard));
        Assert.That(planner.CommandSequence,
            Is.EqualTo(firstCommandSequence + 1));
        Assert.That(GetDestination(destination).x,
            Is.EqualTo(80f).Within(0.001f));
    }

    [Test]
    public void AdapterSource_ContainsNoDirectMovementOrFireWriters()
    {
        string source = File.ReadAllText(Path.Combine(Application.dataPath,
            "Assets/Game/Scripts/Combat/CombatAIMovementAdapter.cs"));
        Assert.That(source, Does.Not.Contain("transform.position ="));
        Assert.That(source, Does.Not.Contain("transform.rotation ="));
        Assert.That(source, Does.Not.Contain("SetRudderCommand"));
        Assert.That(source, Does.Not.Contain("SetPlayerStopSpeedCap"));
        Assert.That(source, Does.Not.Contain("StartTack"));
        Assert.That(source, Does.Not.Contain("StartWear"));
        Assert.That(source, Does.Not.Contain("TryCommitBroadsideFire"));
        Assert.That(source, Does.Not.Contain("TryExecute"));
        Assert.That(source, Does.Contain("destinationController.SetDestination("));
    }

    private CombatBroadsidePoseResult Solve(GameObject shooter, GameObject target)
    {
        Assert.That(CombatBroadsidePoseSolver.TrySolve(
            shooter, target, null, profile, out CombatBroadsidePoseResult result),
            Is.True);
        return result;
    }

    private GameObject CreateShooter()
    {
        GameObject root = CreateRoot(0, Vector3.zero);
        root.AddComponent<ShipCombatState>();
        ShipFireEligibility weapon = root.AddComponent<ShipFireEligibility>();
        SerializedObject weaponData = new SerializedObject(weapon);
        weaponData.FindProperty("effectiveRangeMeters").floatValue = 60f;
        weaponData.FindProperty("maximumRangeMeters").floatValue = 100f;
        weaponData.ApplyModifiedPropertiesWithoutUndo();
        root.AddComponent<ShipSailingSpeed>();
        ShipHeadingController heading = root.AddComponent<ShipHeadingController>();
        ShipManeuverPlanner planner = root.AddComponent<ShipManeuverPlanner>();
        ShipDestinationController destination =
            root.AddComponent<ShipDestinationController>();
        // The EditMode fixture cannot rely on runtime Awake reference wiring.
        // Wire the same Movement chain that the production prefab uses.
        SetReference(heading, "shipTurning", root.GetComponent<ShipTurning>());
        SetReference(planner, "headingController", heading);
        SetReference(destination, "maneuverPlanner", planner);
        return root;
    }

    private GameObject CreateTarget(Vector3 position)
    {
        GameObject root = CreateRoot(1, position);
        ShipCombatGeometry geometry = root.GetComponent<ShipCombatGeometry>();
        GameObject regionObject = new GameObject("Midship");
        regionObject.transform.SetParent(root.transform, false);
        regionObject.layer = 8;
        BoxCollider collider = regionObject.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        CombatHitRegion region = regionObject.AddComponent<CombatHitRegion>();
        SerializedObject regionData = new SerializedObject(region);
        regionData.FindProperty("region").enumValueIndex =
            (int)CombatHullRegion.Midship;
        regionData.FindProperty("owner").objectReferenceValue = geometry;
        regionData.FindProperty("queryCollider").objectReferenceValue = collider;
        regionData.ApplyModifiedPropertiesWithoutUndo();
        SerializedObject geometryData = new SerializedObject(geometry);
        geometryData.FindProperty("midshipRegion").objectReferenceValue = region;
        geometryData.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    private GameObject CreateRoot(int team, Vector3 position)
    {
        GameObject root = new GameObject("Combat AI Movement Test Ship");
        created.Add(root);
        root.transform.position = position;
        root.AddComponent<ShipCombatGeometry>();
        root.AddComponent<ShipCombatAffiliation>();
        SetTeam(root, team);
        ShipIntegrity integrity = root.AddComponent<ShipIntegrity>();
        SetReference(integrity, "integrityProfile",
            AssetDatabase.LoadAssetAtPath<ShipIntegrityProfile>(IntegrityProfilePath));
        Assert.That(integrity.TryInitialize(), Is.True);
        return root;
    }

    private static void SetTeam(GameObject root, int team)
    {
        SerializedObject data = new SerializedObject(
            root.GetComponent<ShipCombatAffiliation>());
        data.FindProperty("teamId").intValue = team;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetReference(Object owner, string field, Object value)
    {
        Assert.That(value, Is.Not.Null);
        SerializedObject data = new SerializedObject(owner);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Vector3 GetDestination(ShipDestinationController owner)
    {
        FieldInfo field = typeof(ShipDestinationController).GetField(
            "destination", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (Vector3)field.GetValue(owner);
    }
}
