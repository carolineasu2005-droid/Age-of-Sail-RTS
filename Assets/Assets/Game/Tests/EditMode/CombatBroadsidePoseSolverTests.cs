using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CombatBroadsidePoseSolverTests
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
        Assert.That(profile.IsPoseValid, Is.True);
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

    [TestCase(70f, 0f, 180f, 0f)]
    [TestCase(-70f, 0f, 0f, 180f)]
    [TestCase(0f, 70f, 90f, 270f)]
    [TestCase(0f, -70f, 270f, 90f)]
    public void CardinalTargets_ProduceCorrectPortAndStarboardHeadings(
        float targetX,
        float targetZ,
        float expectedPort,
        float expectedStarboard
    )
    {
        GameObject shooter = CreateShooter(Vector3.zero);
        GameObject target = CreateTarget(new Vector3(targetX, 0f, targetZ));
        Assert.That(Solve(shooter, target, null, out var result), Is.True);
        Assert.That(result.PortCandidate.IsValid, Is.True);
        Assert.That(result.StarboardCandidate.IsValid, Is.True);
        Assert.That(result.PortCandidate.DesiredHeadingDegrees,
            Is.EqualTo(expectedPort).Within(0.001f));
        Assert.That(result.StarboardCandidate.DesiredHeadingDegrees,
            Is.EqualTo(expectedStarboard).Within(0.001f));

        Vector3 targetDirection = target.transform.position - shooter.transform.position;
        targetDirection.y = 0f;
        shooter.transform.rotation = Quaternion.Euler(0f, expectedPort, 0f);
        Assert.That(shooter.GetComponent<ShipFireEligibility>()
            .TryEvaluateBlindFireDirection(targetDirection,
                out BlindFireEligibilityResult portArc), Is.True);
        Assert.That(portArc.InBroadsideArc, Is.True);
        Assert.That(portArc.Side, Is.EqualTo(CombatSide.Port));
        Assert.That(portArc.AimLocalBearingDegrees, Is.EqualTo(-90f).Within(0.001f));

        shooter.transform.rotation = Quaternion.Euler(0f, expectedStarboard, 0f);
        Assert.That(shooter.GetComponent<ShipFireEligibility>()
            .TryEvaluateBlindFireDirection(targetDirection,
                out BlindFireEligibilityResult starboardArc), Is.True);
        Assert.That(starboardArc.InBroadsideArc, Is.True);
        Assert.That(starboardArc.Side, Is.EqualTo(CombatSide.Starboard));
        Assert.That(starboardArc.AimLocalBearingDegrees, Is.EqualTo(90f).Within(0.001f));
    }

    [Test]
    public void FoundationRatios_UseWeaponMaximumFromFireEligibility()
    {
        GameObject shooter = CreateShooter(Vector3.zero);
        GameObject target = CreateTarget(Vector3.right * 70f);
        ShipFireEligibility weapon = shooter.GetComponent<ShipFireEligibility>();

        Assert.That(profile.DesiredCombatRangeRatio, Is.EqualTo(0.70f));
        Assert.That(profile.PreferredRangeBandMinRatio, Is.EqualTo(0.55f));
        Assert.That(profile.PreferredRangeBandMaxRatio, Is.EqualTo(0.80f));
        Assert.That(profile.BroadsideSideSwitchAdvantageDegrees, Is.EqualTo(20f));
        Assert.That(profile.BroadsideApproachAngleDegrees, Is.EqualTo(35f));
        Assert.That(profile.BroadsideApproachLeadDistanceMeters, Is.EqualTo(120f));
        Assert.That(weapon.MaximumRangeMeters, Is.EqualTo(100f));
        Assert.That(Solve(shooter, target, null, out var result), Is.True);
        Assert.That(result.MaximumWeaponRangeMeters, Is.EqualTo(weapon.MaximumRangeMeters));
        Assert.That(result.DesiredRangeMeters, Is.EqualTo(70f).Within(0.001f));
        Assert.That(new SerializedObject(profile).FindProperty("maximumRangeMeters"), Is.Null);

        SerializedObject weaponData = new SerializedObject(weapon);
        weaponData.FindProperty("maximumRangeMeters").floatValue = 200f;
        weaponData.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(Solve(shooter, target, null, out var updated), Is.True);
        Assert.That(updated.MaximumWeaponRangeMeters, Is.EqualTo(200f));
        Assert.That(updated.DesiredRangeMeters, Is.EqualTo(140f).Within(0.001f));
    }

    [Test]
    public void InvalidPoseRatios_DoNotInvalidateAcquisitionConfiguration()
    {
        CombatAIProfile invalidPose = ScriptableObject.CreateInstance<CombatAIProfile>();
        try
        {
            SerializedObject data = new SerializedObject(invalidPose);
            data.FindProperty("preferredRangeBandMinRatio").floatValue = 0.9f;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(invalidPose.IsValid, Is.True);
            Assert.That(invalidPose.IsPoseValid, Is.False);

            GameObject shooter = CreateShooter(Vector3.zero);
            GameObject target = CreateTarget(Vector3.right * 70f);
            Assert.That(CombatBroadsidePoseSolver.TrySolve(
                shooter, target, null, invalidPose, out var result), Is.False);
            Assert.That(result.IsValid, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(invalidPose);
        }
    }

    [TestCase(54f, CombatRangeState.TooClose, CombatAIMovementIntent.OpenRange)]
    [TestCase(54.99f, CombatRangeState.TooClose, CombatAIMovementIntent.OpenRange)]
    [TestCase(55f, CombatRangeState.InBand, CombatAIMovementIntent.HoldCombatPose)]
    [TestCase(80f, CombatRangeState.InBand, CombatAIMovementIntent.HoldCombatPose)]
    [TestCase(80.01f, CombatRangeState.TooFar, CombatAIMovementIntent.CloseRange)]
    [TestCase(81f, CombatRangeState.TooFar, CombatAIMovementIntent.CloseRange)]
    public void RangeBand_ClassifiesDescriptiveIntent(
        float distance,
        CombatRangeState expectedRange,
        CombatAIMovementIntent expectedIntent
    )
    {
        GameObject shooter = CreateShooter(Vector3.zero);
        GameObject target = CreateTarget(Vector3.right * distance);
        Assert.That(Solve(shooter, target, CombatSide.Starboard, out var result), Is.True);
        Assert.That(result.RangeState, Is.EqualTo(expectedRange));
        Assert.That(result.MovementIntent, Is.EqualTo(expectedIntent));
    }

    [Test]
    public void InBandMisalignment_RequestsAlignBroadside()
    {
        GameObject shooter = CreateShooter(Vector3.zero);
        GameObject target = CreateTarget(Vector3.forward * 70f);
        Assert.That(Solve(shooter, target, null, out var result), Is.True);
        Assert.That(result.RangeState, Is.EqualTo(CombatRangeState.InBand));
        Assert.That(result.MovementIntent, Is.EqualTo(CombatAIMovementIntent.AlignBroadside));
    }

    [Test]
    public void DesiredPosition_UsesTargetRelativeHorizontalRadialLine()
    {
        GameObject shooter = CreateShooter(new Vector3(10f, 2f, 20f));
        GameObject target = CreateTarget(new Vector3(110f, 5f, 20f));
        Assert.That(Solve(shooter, target, null, out var result), Is.True);
        Assert.That(result.DesiredPositionWorld.x, Is.EqualTo(40f).Within(0.001f));
        Assert.That(result.DesiredPositionWorld.y, Is.EqualTo(5f).Within(0.001f));
        Assert.That(result.DesiredPositionWorld.z, Is.EqualTo(20f).Within(0.001f));
        Assert.That(result.PortCandidate.DesiredPositionWorld,
            Is.EqualTo(result.StarboardCandidate.DesiredPositionWorld));
    }

    [TestCase(150f, CombatSide.Port, 1f)]
    [TestCase(150f, CombatSide.Starboard, -1f)]
    [TestCase(-150f, CombatSide.Port, -1f)]
    [TestCase(-150f, CombatSide.Starboard, 1f)]
    public void TooFar_ApproachClosesAndOffsetsTowardPreferredBroadside(
        float targetZ, CombatSide preferred, float lateralSign)
    {
        GameObject shooter = CreateShooter(Vector3.zero);
        GameObject target = CreateTarget(Vector3.forward * targetZ);
        Assert.That(Solve(shooter, target, preferred, out var first), Is.True);
        Assert.That(Solve(shooter, target, preferred, out var repeated), Is.True);
        Assert.That(first.PreferredSide, Is.EqualTo(preferred));
        Assert.That(first.RangeState, Is.EqualTo(CombatRangeState.TooFar));
        Assert.That(first.MovementIntent, Is.EqualTo(CombatAIMovementIntent.CloseRange));
        Assert.That(first.ApproachActive, Is.True);
        Vector3 leg = first.ApproachDestinationWorld - shooter.transform.position;
        Vector3 toTarget = (target.transform.position - shooter.transform.position)
            .normalized;
        Assert.That(leg.y, Is.EqualTo(0f).Within(0.001f));
        Assert.That(leg.magnitude, Is.EqualTo(120f).Within(0.001f));
        Assert.That(Vector3.Dot(leg.normalized, toTarget),
            Is.EqualTo(Mathf.Cos(35f * Mathf.Deg2Rad)).Within(0.001f));
        Assert.That(leg.x * lateralSign, Is.GreaterThan(50f));
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(
            targetZ > 0f ? 0f : 180f, first.ApproachHeadingDegrees)),
            Is.EqualTo(35f).Within(0.001f));
        Assert.That(float.IsNaN(first.ApproachHeadingDegrees), Is.False);
        Assert.That(float.IsInfinity(first.ApproachHeadingDegrees), Is.False);
        Assert.That(repeated.ApproachHeadingDegrees,
            Is.EqualTo(first.ApproachHeadingDegrees));
        Assert.That(repeated.ApproachDestinationWorld,
            Is.EqualTo(first.ApproachDestinationWorld));
        Assert.That(first.DesiredPositionWorld.x, Is.EqualTo(0f).Within(0.001f));
        Assert.That(Mathf.Abs(first.ApproachDestinationWorld.x),
            Is.GreaterThan(50f),
            "A stern chase must not command only the longitudinal radial station.");
    }

    [Test]
    public void InBandAndTooClose_DoNotActivateApproach()
    {
        GameObject shooter = CreateShooter(Vector3.zero);
        GameObject target = CreateTarget(Vector3.forward * 70f);
        Assert.That(Solve(shooter, target, null, out var inBand), Is.True);
        Assert.That(inBand.ApproachActive, Is.False);
        target.transform.position = Vector3.forward * 30f;
        Assert.That(Solve(shooter, target, null, out var tooClose), Is.True);
        Assert.That(tooClose.ApproachActive, Is.False);
        Assert.That(tooClose.MovementIntent,
            Is.EqualTo(CombatAIMovementIntent.OpenRange));
    }

    [Test]
    public void RepeatedInput_IsDeterministicAndTargetPositionChangesPose()
    {
        GameObject shooter = CreateShooter(Vector3.zero);
        GameObject target = CreateTarget(Vector3.right * 70f);
        Assert.That(Solve(shooter, target, null, out var first), Is.True);
        Assert.That(Solve(shooter, target, null, out var repeat), Is.True);
        Assert.That(repeat.DesiredPositionWorld, Is.EqualTo(first.DesiredPositionWorld));
        Assert.That(repeat.DesiredHeadingDegrees, Is.EqualTo(first.DesiredHeadingDegrees));
        Assert.That(repeat.PreferredSide, Is.EqualTo(first.PreferredSide));
        Assert.That(first.IsValid, Is.True);
        Assert.That(first.DesiredPositionWorld.sqrMagnitude,
            Is.LessThan(0.000001f));

        target.transform.position = Vector3.forward * 75f;
        Assert.That(Solve(shooter, target, null, out var movedTarget), Is.True);
        Assert.That(movedTarget.IsValid, Is.True);
        Assert.That(movedTarget.DesiredPositionWorld.z,
            Is.EqualTo(5f).Within(0.001f));
        Assert.That(movedTarget.DesiredPositionWorld,
            Is.Not.EqualTo(first.DesiredPositionWorld));
        Assert.That(movedTarget.PortCandidate.DesiredHeadingDegrees,
            Is.Not.EqualTo(first.PortCandidate.DesiredHeadingDegrees));
    }

    [TestCase(85f, CombatSide.Port, CombatSide.Port)]
    [TestCase(70f, CombatSide.Port, CombatSide.Starboard)]
    [TestCase(95f, CombatSide.Starboard, CombatSide.Starboard)]
    [TestCase(110f, CombatSide.Starboard, CombatSide.Port)]
    public void PreferredSide_SwitchesOnlyForConfiguredHeadingAdvantage(
        float currentHeading,
        CombatSide preferred,
        CombatSide expected
    )
    {
        GameObject shooter = CreateShooter(Vector3.zero);
        shooter.transform.rotation = Quaternion.Euler(0f, currentHeading, 0f);
        GameObject target = CreateTarget(Vector3.right * 70f);
        Assert.That(Solve(shooter, target, preferred, out var result), Is.True);
        Assert.That(result.PreferredSide, Is.EqualTo(expected));
    }

    [Test]
    public void NoPreferredSide_ChoosesCheapestAndPortWinsExactTie()
    {
        GameObject shooter = CreateShooter(Vector3.zero);
        GameObject target = CreateTarget(Vector3.right * 70f);
        shooter.transform.rotation = Quaternion.Euler(0f, 10f, 0f);
        Assert.That(Solve(shooter, target, null, out var lowerCost), Is.True);
        Assert.That(lowerCost.PreferredSide, Is.EqualTo(CombatSide.Starboard));
        shooter.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        Assert.That(Solve(shooter, target, null, out var tie), Is.True);
        Assert.That(tie.PreferredSide, Is.EqualTo(CombatSide.Port));
    }

    [Test]
    public void Solver_DoesNotMutateRootsMovementOrCombat()
    {
        GameObject shooter = CreateShooter(Vector3.zero);
        GameObject target = CreateTarget(Vector3.right * 70f);
        ShipHeadingController heading = shooter.AddComponent<ShipHeadingController>();
        ShipManeuverPlanner planner = shooter.AddComponent<ShipManeuverPlanner>();
        ShipDestinationController destination = shooter.AddComponent<ShipDestinationController>();
        ShipCombatState combat = shooter.GetComponent<ShipCombatState>();
        Vector3 shooterPosition = shooter.transform.position;
        Quaternion shooterRotation = shooter.transform.rotation;
        Vector3 targetPosition = target.transform.position;
        Quaternion targetRotation = target.transform.rotation;
        int projectiles = Object.FindObjectsByType<CombatProjectile>(
            FindObjectsInactive.Include).Length;
        int sequence = planner.CommandSequence;
        bool headingActive = heading.IsActive;
        bool hasDestination = destination.HasDestination;
        BroadsideReloadState portReload = combat.PortBroadsideState;
        BroadsideReloadState starboardReload = combat.StarboardBroadsideState;
        float portRemaining = combat.PortReloadRemainingSeconds;
        float starboardRemaining = combat.StarboardReloadRemainingSeconds;
        bool autoFireEnabled = combat.AutoFireEnabled;
        GameObject manualTarget = combat.ManualTarget;

        Assert.That(Solve(shooter, target, null, out var result), Is.True);
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.PortCandidate.NavigationFeasible, Is.Null);
        Assert.That(result.StarboardCandidate.NavigationFeasible, Is.Null);
        Assert.That(shooter.transform.position, Is.EqualTo(shooterPosition));
        Assert.That(shooter.transform.rotation, Is.EqualTo(shooterRotation));
        Assert.That(target.transform.position, Is.EqualTo(targetPosition));
        Assert.That(target.transform.rotation, Is.EqualTo(targetRotation));
        Assert.That(heading.IsActive, Is.EqualTo(headingActive));
        Assert.That(planner.CommandSequence, Is.EqualTo(sequence));
        Assert.That(destination.HasDestination, Is.EqualTo(hasDestination));
        Assert.That(combat.PortBroadsideState, Is.EqualTo(portReload));
        Assert.That(combat.StarboardBroadsideState, Is.EqualTo(starboardReload));
        Assert.That(combat.PortReloadRemainingSeconds, Is.EqualTo(portRemaining));
        Assert.That(combat.StarboardReloadRemainingSeconds,
            Is.EqualTo(starboardRemaining));
        Assert.That(combat.AutoFireEnabled, Is.EqualTo(autoFireEnabled));
        Assert.That(combat.ManualTarget, Is.SameAs(manualTarget));
        Assert.That(Object.FindObjectsByType<CombatProjectile>(
            FindObjectsInactive.Include).Length, Is.EqualTo(projectiles));
    }

    [Test]
    public void CoincidentHorizontalRoots_HaveNoValidSolution()
    {
        GameObject shooter = CreateShooter(Vector3.zero);
        GameObject target = CreateTarget(Vector3.up * 5f);
        Assert.That(Solve(shooter, target, null, out var result), Is.False);
        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void CombatDisabledShooter_HasNoValidPose()
    {
        GameObject shooter = CreateShooter(Vector3.zero);
        GameObject target = CreateTarget(Vector3.right * 70f);
        ShipIntegrity integrity = shooter.GetComponent<ShipIntegrity>();
        Assert.That(integrity.TryApplyIntegrityLoss(
            integrity.MaximumIntegrity * 0.8f, out _), Is.True);
        Assert.That(integrity.IsCombatDisabled, Is.True);
        Assert.That(Solve(shooter, target, null, out var result), Is.False);
        Assert.That(result.IsValid, Is.False);
    }

    private bool Solve(GameObject shooter, GameObject target,
        CombatSide? preferred, out CombatBroadsidePoseResult result)
    {
        return CombatBroadsidePoseSolver.TrySolve(
            shooter, target, preferred, profile, out result);
    }

    private GameObject CreateShooter(Vector3 position)
    {
        GameObject root = CreateRoot(0, position);
        root.AddComponent<ShipCombatState>();
        ShipFireEligibility weapon = root.AddComponent<ShipFireEligibility>();
        SerializedObject serialized = new SerializedObject(weapon);
        serialized.FindProperty("effectiveRangeMeters").floatValue = 60f;
        serialized.FindProperty("maximumRangeMeters").floatValue = 100f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    private GameObject CreateTarget(Vector3 position)
    {
        GameObject root = CreateRoot(1, position);
        ShipCombatGeometry geometry = root.GetComponent<ShipCombatGeometry>();
        GameObject child = new GameObject("Midship");
        child.transform.SetParent(root.transform, false);
        child.layer = 8;
        BoxCollider collider = child.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        CombatHitRegion region = child.AddComponent<CombatHitRegion>();
        SerializedObject regionData = new SerializedObject(region);
        regionData.FindProperty("region").enumValueIndex = (int)CombatHullRegion.Midship;
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
        GameObject root = new GameObject("Pose Test Ship");
        created.Add(root);
        root.transform.position = position;
        root.AddComponent<ShipCombatGeometry>();
        ShipCombatAffiliation affiliation = root.AddComponent<ShipCombatAffiliation>();
        SerializedObject teamData = new SerializedObject(affiliation);
        teamData.FindProperty("teamId").intValue = team;
        teamData.ApplyModifiedPropertiesWithoutUndo();
        ShipIntegrity integrity = root.AddComponent<ShipIntegrity>();
        SerializedObject integrityData = new SerializedObject(integrity);
        integrityData.FindProperty("integrityProfile").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<ShipIntegrityProfile>(IntegrityProfilePath);
        integrityData.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(integrity.TryInitialize(), Is.True);
        return root;
    }
}
