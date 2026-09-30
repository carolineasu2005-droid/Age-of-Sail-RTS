using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class FormalCombatAIMovementPlayModeTests
{
    private const string AIPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/PF_Ship_Gelderland_CombatAI_v01.prefab";
    private const string TargetPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/PF_Ship_Gelderland_Combat_v01.prefab";

    private readonly List<GameObject> createdObjects = new List<GameObject>();
    private readonly List<GlobalWind> existingWinds = new List<GlobalWind>();
    private readonly List<float> originalWindDirections = new List<float>();
    private readonly List<float> originalWindStrengths = new List<float>();

    private GameObject aiRoot;
    private GameObject targetRoot;
    private ShipCombatAIController ai;
    private ShipDestinationController destination;
    private ShipManeuverPlanner planner;
    private ShipSailingSpeed speed;

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        for (int i = 0; i < existingWinds.Count; i++)
        {
            if (existingWinds[i] != null)
            {
                existingWinds[i].windFromDegrees = originalWindDirections[i];
                existingWinds[i].windStrength = originalWindStrengths[i];
            }
        }

        foreach (GameObject createdObject in createdObjects)
        {
            if (createdObject != null)
            {
                Object.Destroy(createdObject);
            }
        }

        yield return null;
        createdObjects.Clear();
        existingWinds.Clear();
        originalWindDirections.Clear();
        originalWindStrengths.Clear();
    }

    [UnityTest]
    public IEnumerator OutOfRange_FormalAIPrefab_SailsAfterCloseRangeCommand()
    {
        CreateFixture(new Vector3(600f, 0f, 0f));
        Assert.That(ai.Think(Time.time), Is.True);

        AssertCommonMovementChain(CombatAIMovementIntent.CloseRange);
        Assert.That(ai.HasLastFireEligibility, Is.True);
        Assert.That(ai.LastFireEligibility.FailureReasons
            & FireEligibilityFailure.BeyondMaximumRange,
            Is.Not.EqualTo(FireEligibilityFailure.None));
        Assert.That(ai.HasLastFireResult, Is.False);

        Vector3 startPosition = aiRoot.transform.position;
        yield return new WaitForSeconds(2f);

        Assert.That(speed.CurrentSpeed, Is.GreaterThan(0f));
        Assert.That(Vector3.Distance(aiRoot.transform.position, startPosition),
            Is.GreaterThan(0.25f));
    }

    [UnityTest]
    public IEnumerator SternChase_FormalAIUsesLateralAttackLegThroughMovement()
    {
        CreateFixture(new Vector3(0f, 0f, 600f), true);
        Assert.That(ai.Think(Time.time), Is.True);
        AssertCommonMovementChain(CombatAIMovementIntent.CloseRange);
        Assert.That(ai.LastPose.RangeState, Is.EqualTo(CombatRangeState.TooFar));
        Assert.That(ai.LastPose.ApproachActive, Is.True);
        Assert.That(ai.LastPose.ApproachUsesCurrentHeading, Is.False);
        Assert.That(ai.LastPose.PreferredSide, Is.EqualTo(CombatSide.Port));
        Vector3 initialAIPosition = aiRoot.transform.position;
        Vector3 initialTargetPosition = targetRoot.transform.position;
        Vector3 commanded = ReadPrivateField<Vector3>(destination, "destination");
        Assert.That(Vector3.Distance(commanded,
            ai.LastPose.ApproachDestinationWorld), Is.LessThan(0.1f));
        Assert.That(commanded.x - initialAIPosition.x, Is.GreaterThan(50f));
        Assert.That(commanded.z - initialAIPosition.z, Is.GreaterThan(50f));
        Assert.That(ai.LastPose.DesiredPositionWorld.x - initialAIPosition.x,
            Is.EqualTo(0f).Within(0.1f));

        for (int second = 0; second < 8; second++)
        {
            yield return new WaitForSeconds(1f);
            Assert.That(ai.CurrentTargetShipRoot, Is.SameAs(targetRoot));
            Assert.That(ai.LastPose.PreferredSide, Is.EqualTo(CombatSide.Port),
                "The approach should retain the chosen broadside across Thinks.");
        }

        Assert.That(targetRoot.transform.position.z - initialTargetPosition.z,
            Is.GreaterThan(0.25f),
            "The stern-chase target must sail through Movement.");
        Assert.That(speed.CurrentSpeed, Is.GreaterThan(0f));
        Assert.That(aiRoot.transform.position.z - initialAIPosition.z,
            Is.GreaterThan(0.25f));
        Assert.That(aiRoot.transform.position.x - initialAIPosition.x,
            Is.GreaterThan(1f),
            "The AI should leave the original pursuit centerline.");
        Assert.That(destination.HasDestination, Is.True);
    }

    [UnityTest]
    public IEnumerator AftPursuer_FormalAITurnsOutThroughMovement()
    {
        CreateFixture(new Vector3(0f, 0f, -600f), true);
        Vector3 initialAIPosition = aiRoot.transform.position;
        Vector3 initialTargetPosition = targetRoot.transform.position;
        Vector3 toTarget = initialTargetPosition - initialAIPosition;
        toTarget.y = 0f;
        Assert.That(Vector3.Dot(aiRoot.transform.forward, toTarget.normalized),
            Is.LessThan(-0.9f));

        Assert.That(ai.Think(Time.time), Is.True);
        AssertCommonMovementChain(CombatAIMovementIntent.CloseRange);
        CombatBroadsidePoseResult initialPose = ai.LastPose;
        Assert.That(initialPose.RangeState, Is.EqualTo(CombatRangeState.TooFar));
        Assert.That(initialPose.ApproachActive, Is.True);
        Assert.That(initialPose.ApproachUsesCurrentHeading, Is.True);
        Assert.That(initialPose.PreferredSide, Is.EqualTo(CombatSide.Port));
        Assert.That(initialPose.DesiredPositionWorld.z - initialAIPosition.z,
            Is.LessThan(-100f));
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(
            aiRoot.transform.eulerAngles.y, initialPose.ApproachHeadingDegrees)),
            Is.EqualTo(ai.Profile.BroadsideApproachAngleDegrees).Within(0.1f));

        Vector3 commanded = ReadPrivateField<Vector3>(destination, "destination");
        Assert.That(Vector3.Distance(commanded,
            initialPose.ApproachDestinationWorld), Is.LessThan(0.1f));
        Vector3 leg = commanded - initialAIPosition;
        Assert.That(leg.magnitude,
            Is.EqualTo(ai.Profile.BroadsideApproachLeadDistanceMeters)
                .Within(0.1f));
        Assert.That(leg.x, Is.LessThan(-50f));
        Assert.That(leg.z, Is.GreaterThan(50f));
        Assert.That(Vector3.Dot(leg.normalized, toTarget.normalized),
            Is.LessThan(0f),
            "The tactical leg must turn out rather than reverse at the pursuer.");

        for (int second = 0; second < 8; second++)
        {
            yield return new WaitForSeconds(1f);
            Assert.That(ai.CurrentTargetShipRoot, Is.SameAs(targetRoot));
            Assert.That(ai.LastPose.RangeState,
                Is.EqualTo(CombatRangeState.TooFar));
            Assert.That(ai.LastPose.MovementIntent,
                Is.EqualTo(CombatAIMovementIntent.CloseRange));
            Assert.That(ai.LastPose.ApproachUsesCurrentHeading, Is.True);
            Assert.That(ai.LastPose.PreferredSide, Is.EqualTo(CombatSide.Port),
                "The existing side hysteresis should keep the turn-out stable.");
        }

        Assert.That(targetRoot.transform.position.z - initialTargetPosition.z,
            Is.GreaterThan(0.25f),
            "The pursuer must advance through its Movement destination.");
        Assert.That(speed.CurrentSpeed, Is.GreaterThan(0f));
        Assert.That(aiRoot.transform.position.z - initialAIPosition.z,
            Is.GreaterThan(0.25f));
        Assert.That(initialAIPosition.x - aiRoot.transform.position.x,
            Is.GreaterThan(1f),
            "The pursued AI must leave its original centerline.");
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f,
            aiRoot.transform.eulerAngles.y)), Is.GreaterThan(1f));
        Assert.That(destination.HasDestination, Is.True);
    }

    [UnityTest]
    public IEnumerator WrongArc_FormalAIPrefab_TurnsAfterAlignmentCommand()
    {
        CreateFixture(new Vector3(0f, 0f, 350f));
        Assert.That(ai.Think(Time.time), Is.True);

        AssertCommonMovementChain(CombatAIMovementIntent.AlignBroadside);
        Assert.That(ai.LastPose.ApproachActive, Is.False);
        Assert.That(ai.LastPose.ApproachUsesCurrentHeading, Is.False);
        Assert.That(ai.HasLastFireEligibility, Is.True);
        Assert.That(ai.LastFireEligibility.FailureReasons
            & FireEligibilityFailure.NoBroadsideArc,
            Is.Not.EqualTo(FireEligibilityFailure.None));
        Assert.That(ai.HasLastFireResult, Is.False);

        Vector3 commandedDestination = ReadPrivateField<Vector3>(
            destination, "destination");
        Vector3 expectedLead = aiRoot.transform.position
            + Quaternion.Euler(0f, ai.LastPose.DesiredHeadingDegrees, 0f)
                * Vector3.forward
                * ai.Profile.BroadsideAlignmentLeadDistanceMeters;
        Assert.That(Vector3.Distance(commandedDestination, expectedLead),
            Is.LessThan(0.1f));

        Vector3 startPosition = aiRoot.transform.position;
        float startHeading = aiRoot.transform.eulerAngles.y;
        yield return new WaitForSeconds(2f);

        Assert.That(Vector3.Distance(aiRoot.transform.position, startPosition),
            Is.GreaterThan(0.25f));
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(
                startHeading, aiRoot.transform.eulerAngles.y)),
            Is.GreaterThan(0.25f));
    }

    private void CreateFixture(Vector3 targetOffset, bool targetSails = false)
    {
#if UNITY_EDITOR
        foreach (GlobalWind existingWind in Object.FindObjectsByType<GlobalWind>(
            FindObjectsSortMode.None))
        {
            existingWinds.Add(existingWind);
            originalWindDirections.Add(existingWind.windFromDegrees);
            originalWindStrengths.Add(existingWind.windStrength);
            existingWind.windFromDegrees = 180f;
            existingWind.windStrength = 1f;
        }

        GameObject windObject = new GameObject("Formal AI movement test wind");
        createdObjects.Add(windObject);
        GlobalWind wind = windObject.AddComponent<GlobalWind>();
        wind.windFromDegrees = 180f;
        wind.windStrength = 1f;

        GameObject aiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            AIPrefabPath);
        GameObject targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            TargetPrefabPath);
        Assert.That(aiPrefab, Is.Not.Null);
        Assert.That(targetPrefab, Is.Not.Null);

        Vector3 origin = new Vector3(10000f, 0f, 10000f);
        aiRoot = Object.Instantiate(aiPrefab, origin, Quaternion.identity);
        createdObjects.Add(aiRoot);
        targetRoot = Object.Instantiate(targetPrefab,
            origin + targetOffset, Quaternion.identity);
        createdObjects.Add(targetRoot);
        SetTeamId(aiRoot, 2);
        SetTeamId(targetRoot, 1);
        if (targetSails)
        {
            targetRoot.GetComponent<ShipDestinationController>().SetDestination(
                targetRoot.transform.position + Vector3.forward * 300f,
                ShipDestinationController.TurnSelectionMode.Auto,
                WindNavigationAssistMode.Assisted);
        }
        else
        {
            targetRoot.GetComponent<ShipSailingSpeed>().enabled = false;
            targetRoot.GetComponent<ShipTurning>().enabled = false;
        }

        ai = aiRoot.GetComponent<ShipCombatAIController>();
        destination = aiRoot.GetComponent<ShipDestinationController>();
        planner = aiRoot.GetComponent<ShipManeuverPlanner>();
        speed = aiRoot.GetComponent<ShipSailingSpeed>();
        Assert.That(ai, Is.Not.Null);
        Assert.That(destination, Is.Not.Null);
        Assert.That(planner, Is.Not.Null);
        Assert.That(speed, Is.Not.Null);
        Physics.SyncTransforms();
#else
        Assert.Ignore("Formal prefab loading requires the Unity Editor.");
#endif
    }

    private void AssertCommonMovementChain(CombatAIMovementIntent expectedIntent)
    {
        Assert.That(ai.CurrentTargetShipRoot, Is.SameAs(targetRoot));
        Assert.That(ai.LastPose.IsValid, Is.True);
        Assert.That(ai.LastPose.MovementIntent, Is.EqualTo(expectedIntent));
        Assert.That(ai.LastMovementCommandStatus,
            Is.EqualTo(CombatAIMovementCommandStatus.Submitted));
        Assert.That(destination.HasDestination, Is.True);
        Assert.That(destination.CurrentNavigationMode,
            Is.EqualTo(ShipDestinationController.NavigationMode.Direct));
        Assert.That(ReadPrivateField<ShipManeuverPlanner>(
                destination, "maneuverPlanner"), Is.SameAs(planner));
        Assert.That(planner.CommandSequence, Is.GreaterThan(0));
        Assert.That(planner.CurrentManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.NormalTurn));
        ShipHeadingController heading =
            aiRoot.GetComponent<ShipHeadingController>();
        ShipTurning turning = aiRoot.GetComponent<ShipTurning>();
        Assert.That(heading, Is.Not.Null);
        Assert.That(turning, Is.Not.Null);
        Assert.That(heading.IsActive, Is.True);
        Assert.That(ReadPrivateField<ShipHeadingController>(
                planner, "headingController"), Is.SameAs(heading));
        Assert.That(ReadPrivateField<ShipTurning>(
                heading, "shipTurning"), Is.SameAs(turning));
        Assert.That(ReadPrivateField<ShipSailingSpeed>(
                turning, "shipSailingSpeed"), Is.SameAs(speed));
        Assert.That(speed.IsPlayerStopped, Is.False);
        Assert.That(speed.GetWindLimitedTargetSpeed(), Is.GreaterThan(0f));
        Assert.That(ReadPrivateField<GlobalWind>(speed, "globalWind"),
            Is.Not.Null);
        Assert.That(ReadPrivateField<GlobalWind>(planner, "globalWind"),
            Is.Not.Null);
    }

    private static void SetTeamId(GameObject shipRoot, int teamId)
    {
        ShipCombatAffiliation affiliation =
            shipRoot.GetComponent<ShipCombatAffiliation>();
        Assert.That(affiliation, Is.Not.Null);
        FieldInfo field = typeof(ShipCombatAffiliation).GetField(
            "teamId", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(affiliation, teamId);
    }

    private static T ReadPrivateField<T>(object owner, string name)
    {
        FieldInfo field = owner.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        return (T)field.GetValue(owner);
    }
}
