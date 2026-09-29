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
    public IEnumerator WrongArc_FormalAIPrefab_TurnsAfterAlignmentCommand()
    {
        CreateFixture(new Vector3(0f, 0f, 350f));
        Assert.That(ai.Think(Time.time), Is.True);

        AssertCommonMovementChain(CombatAIMovementIntent.AlignBroadside);
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

    private void CreateFixture(Vector3 targetOffset)
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
        targetRoot.GetComponent<ShipSailingSpeed>().enabled = false;
        targetRoot.GetComponent<ShipTurning>().enabled = false;

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
