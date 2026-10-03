using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CombatAIIntegrationTests
{
    private const string BasePath =
        "Assets/Assets/Game/Ship/Proxy/PF_Ship_Gelderland_Combat_v01.prefab";
    private const string AIPath =
        "Assets/Assets/Game/Ship/Proxy/PF_Ship_Gelderland_CombatAI_v01.prefab";
    private const string ProfilePath =
        "Assets/Assets/Game/Data/SO_CombatAI_Foundation.asset";
    private const string ScenePath =
        "Assets/Scenes/Prototype/SailingPrototype_01_Speed.unity";

    [Test]
    public void AIPrefab_IsOneControllerVariantOfNeutralCombatPrefab()
    {
        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePath);
        GameObject aiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AIPath);
        CombatAIProfile profile = AssetDatabase.LoadAssetAtPath<CombatAIProfile>(
            ProfilePath);
        Assert.That(basePrefab, Is.Not.Null);
        Assert.That(aiPrefab, Is.Not.Null);
        Assert.That(profile, Is.Not.Null);
        Assert.That(PrefabUtility.GetPrefabAssetType(aiPrefab),
            Is.EqualTo(PrefabAssetType.Variant));
        Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(aiPrefab),
            Is.EqualTo(basePrefab));
        Assert.That(basePrefab.GetComponentsInChildren<ShipCombatAIController>(true),
            Is.Empty);
        Assert.That(aiPrefab.GetComponentsInChildren<ShipCombatAIController>(true),
            Has.Length.EqualTo(1));
        Assert.That(aiPrefab.GetComponent<ShipCombatAIController>().Profile,
            Is.SameAs(profile));
        Assert.That(aiPrefab.GetComponent<ShipCombatAffiliation>().TeamId,
            Is.EqualTo(-1));
        Assert.That(basePrefab.GetComponent<ShipCombatAffiliation>().TeamId,
            Is.EqualTo(-1));

        Assert.That(aiPrefab.GetComponent<ShipBroadsideFireExecutor>().ProjectilePrefab,
            Is.SameAs(basePrefab.GetComponent<ShipBroadsideFireExecutor>().ProjectilePrefab));
        Assert.That(aiPrefab.GetComponent<ShipBroadsideFireExecutor>().CombatDamageProfile,
            Is.SameAs(basePrefab.GetComponent<ShipBroadsideFireExecutor>().CombatDamageProfile));
        Assert.That(aiPrefab.GetComponent<ShipBroadsideFireExecutor>().CombatRakingProfile,
            Is.SameAs(basePrefab.GetComponent<ShipBroadsideFireExecutor>().CombatRakingProfile));
        Assert.That(aiPrefab.GetComponent<ShipFireEligibility>().CombatObstructionProfile,
            Is.SameAs(basePrefab.GetComponent<ShipFireEligibility>().CombatObstructionProfile));
        Assert.That(aiPrefab.GetComponent<CombatVFXPlaceholderReceiver>()
            .CannonLingeringSmokePrefab,
            Is.SameAs(basePrefab.GetComponent<CombatVFXPlaceholderReceiver>()
                .CannonLingeringSmokePrefab));
    }

    [Test]
    public void AIAsset_AddsOnlyControllerAndExplicitProfile()
    {
        string yaml = File.ReadAllText(AIPath);
        Assert.That(yaml, Does.Contain(
            "m_SourcePrefab: {fileID: 100100000, guid: 45f76b674dece124096d84bb473f8322, type: 3}"));
        Assert.That(Count(yaml, "addedObject: {fileID:"), Is.EqualTo(1));
        Assert.That(Count(yaml,
            "m_EditorClassIdentifier: AgeOfSailRTS.Runtime::ShipCombatAIController"),
            Is.EqualTo(1));
        Assert.That(yaml, Does.Contain(
            "profile: {fileID: 11400000, guid: a6111000000000000000000000000006, type: 2}"));
        Assert.That(yaml, Does.Not.Contain("teamId: 2"));
    }

    [Test]
    public void GenericProxies_HaveNoCombatOrAIComponents()
    {
        foreach (string weight in new[] { "Light", "Medium", "Heavy" })
        {
            string path = "Assets/Assets/Game/Ship/Proxy/PF_Proxy_"
                + weight + "_v01.prefab";
            GameObject proxy = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(proxy, Is.Not.Null);
            Assert.That(proxy.GetComponentsInChildren<ShipCombatAIController>(true),
                Is.Empty, weight);
            Assert.That(proxy.GetComponentsInChildren<ShipCombatState>(true),
                Is.Empty, weight);
            Assert.That(proxy.GetComponentsInChildren<ShipFireEligibility>(true),
                Is.Empty, weight);
            Assert.That(proxy.GetComponentsInChildren<ShipBroadsideFireExecutor>(true),
                Is.Empty, weight);
            string yaml = File.ReadAllText(path);
            Assert.That(yaml, Does.Not.Contain(
                "a6111000000000000000000000000004"), weight);
            Assert.That(yaml, Does.Not.Contain(
                "a6111000000000000000000000000006"), weight);
        }
    }

    [Test]
    public void Prototype_PreservesTeamOneCombatShipAndHasTeamTwoAIVariant()
    {
        string scene = File.ReadAllText(ScenePath).Replace("\r\n", "\n");
        MatchCollection instances = Regex.Matches(scene,
            @"^--- !u!1001 &(?<id>[0-9]+)\n.*?(?=^--- !u!|\z)",
            RegexOptions.Multiline | RegexOptions.Singleline);
        Match sceneRoots = Regex.Match(scene,
            @"^--- !u!1660057539 &[0-9]+\nSceneRoots:\n.*?(?=^--- !u!|\z)",
            RegexOptions.Multiline | RegexOptions.Singleline);
        Assert.That(sceneRoots.Success, Is.True);
        int teamOneCombatShips = 0;
        int teamTwoAIVariants = 0;
        foreach (Match instance in instances)
        {
            string rootEntry = @"^  - \{fileID: "
                + instance.Groups["id"].Value + @"\}$";
            if (!Regex.IsMatch(sceneRoots.Value, rootEntry,
                RegexOptions.Multiline))
            {
                continue;
            }

            if (instance.Value.IndexOf(
                "m_SourcePrefab: {fileID: 100100000, guid: 45f76b674dece124096d84bb473f8322, type: 3}",
                StringComparison.Ordinal) >= 0
                && instance.Value.IndexOf("propertyPath: teamId\n      value: 1",
                    StringComparison.Ordinal) >= 0)
            {
                teamOneCombatShips++;
            }

            if (instance.Value.IndexOf(
                "m_SourcePrefab: {fileID: 100100000, guid: a6111000000000000000000000000012, type: 3}",
                StringComparison.Ordinal) >= 0
                && instance.Value.IndexOf("propertyPath: teamId\n      value: 2",
                    StringComparison.Ordinal) >= 0)
            {
                teamTwoAIVariants++;
            }
        }

        Assert.That(teamOneCombatShips, Is.GreaterThanOrEqualTo(1));
        Assert.That(teamTwoAIVariants, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void Panel_FormatsResolvedAIStateWithoutTacticalQueries()
    {
        GameObject ship = new GameObject("AI Root");
        GameObject target = new GameObject("Authoritative Target Root");
        try
        {
            GameObject child = new GameObject("Child Collider");
            child.transform.SetParent(target.transform, false);
            ShipCombatAIController ai = ship.AddComponent<ShipCombatAIController>();
            SetField(ai, "currentTargetShipRoot", target);
            SetField(ai, "targetRelationship", CombatRelationship.Hostile);
            SetField(ai, "targetLifecycle", ShipCombatLifecycleState.Operational);
            SetField(ai, "lastTargetChangeReason",
                CombatAITargetChangeReason.Acquired);
            MethodInfo formatter = typeof(ShipTestPanel).GetMethod(
                "BuildCombatAISummary", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(formatter, Is.Not.Null);
            Assert.That((string)formatter.Invoke(null, new object[] { null }),
                Does.Contain("AI Enabled: NO"));
            string output = (string)formatter.Invoke(null, new object[] { ai });
            Assert.That(output, Does.Contain("AI Enabled: YES"));
            Assert.That(output, Does.Contain("Current Target: Authoritative Target Root"));
            Assert.That(output, Does.Not.Contain("Current Target: Child Collider"));
            Assert.That(output, Does.Contain("Target Relationship: Hostile"));
            Assert.That(output, Does.Contain("Last Target Change Reason: Acquired"));
            Assert.That(output, Does.Contain("Port Fire Eligibility: N/A"));
            Assert.That(output, Does.Contain("Starboard Fire Eligibility: N/A"));
            Assert.That(output, Does.Contain("Active Destination: N/A"));
            Assert.That(output, Does.Contain("Planner Command Sequence: N/A"));
            Assert.That(output, Does.Contain("Player Stopped: N/A"));

            string source = File.ReadAllText(
                "Assets/Assets/Game/Editor/Debug/ShipTestPanel.cs");
            string aiSection = ExtractBlock(source,
                "private static void DrawCombatAISection(",
                "private static void DrawPhaseSevenDiagnostics(");
            Assert.That(aiSection, Does.Contain("ai.LastPose"));
            Assert.That(aiSection, Does.Contain("ai.LastFireEligibility"));
            Assert.That(aiSection, Does.Not.Contain("TrySolve("));
            Assert.That(aiSection, Does.Not.Contain("TryEvaluate("));
            Assert.That(aiSection, Does.Not.Contain("Physics."));
            Assert.That(aiSection, Does.Not.Contain("CombatAITargetAcquisition"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(ship);
        }
    }

    [Test]
    public void Panel_ShowsResolvedRangeAndFireRejectionFromAIThink()
    {
        GameObject aiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AIPath);
        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePath);
        GameObject aiShip = UnityEngine.Object.Instantiate(aiPrefab);
        GameObject target = UnityEngine.Object.Instantiate(basePrefab);
        try
        {
            aiShip.transform.position = Vector3.zero;
            target.transform.position = Vector3.right * 600f;
            target.name = "Observed Target Root";
            aiShip.GetComponent<CombatVFXPlaceholderReceiver>()
                .VisualSpawningEnabled = false;
            CombatLifecycleTestUtility.EnsureOperational(aiShip);
            CombatLifecycleTestUtility.EnsureOperational(target);
            SetTeam(aiShip, 0);
            SetTeam(target, 1);
            Physics.SyncTransforms();
            ShipCombatAIController ai = aiShip.GetComponent<ShipCombatAIController>();
            Assert.That(ai.Think(0f), Is.True);
            Assert.That(ai.LastPose.IsValid, Is.True);
            Assert.That(ai.HasLastFireEligibility, Is.True);
            Assert.That(ai.LastFireEligibility.FailureReasons.HasFlag(
                FireEligibilityFailure.BeyondMaximumRange), Is.True);
            MethodInfo formatter = typeof(ShipTestPanel).GetMethod(
                "BuildCombatAISummary", BindingFlags.NonPublic | BindingFlags.Static);
            string output = (string)formatter.Invoke(null, new object[] { ai });
            Assert.That(output, Does.Contain("Current Target: Observed Target Root"));
            Assert.That(output, Does.Contain("Range State: TooFar"));
            Assert.That(output, Does.Contain("Approach Basis: To Target"));
            Assert.That(output, Does.Contain("Desired Combat Range: 350"));
            Assert.That(output, Does.Contain("BEYOND_MAXIMUM_RANGE"));
            Assert.That(output, Does.Contain("Last Movement Command: "
                + ai.LastMovementCommandStatus));

            target.transform.position = Vector3.right * 100f;
            Physics.SyncTransforms();
            Assert.That(ai.Think(0.25f), Is.True);
            output = (string)formatter.Invoke(null, new object[] { ai });
            Assert.That(output, Does.Contain("Last Fire Side: Starboard"));
            Assert.That(output, Does.Contain("Last Fire Result: Accepted"));

            target.transform.position = Vector3.back * 600f;
            Physics.SyncTransforms();
            Assert.That(ai.Think(0.5f), Is.True);
            output = (string)formatter.Invoke(null, new object[] { ai });
            Assert.That(output, Does.Contain("Approach Basis: Current Heading"));
        }
        finally
        {
            foreach (CombatProjectile projectile in
                UnityEngine.Object.FindObjectsByType<CombatProjectile>(
                    FindObjectsInactive.Include))
            {
                UnityEngine.Object.DestroyImmediate(projectile.gameObject);
            }
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(aiShip);
        }
    }

    private static void SetTeam(GameObject root, int team)
    {
        SerializedObject affiliation = new SerializedObject(
            root.GetComponent<ShipCombatAffiliation>());
        affiliation.FindProperty("teamId").intValue = team;
        affiliation.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetField(object owner, string name, object value)
    {
        FieldInfo field = owner.GetType().GetField(name,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, name);
        field.SetValue(owner, value);
    }

    private static int Count(string text, string token)
    {
        int count = 0;
        for (int index = 0; (index = text.IndexOf(token, index,
            StringComparison.Ordinal)) >= 0; index += token.Length)
        {
            count++;
        }
        return count;
    }

    private static string ExtractBlock(string text, string start, string end)
    {
        string normalized = text.Replace("\r\n", "\n");
        int first = normalized.IndexOf(start, StringComparison.Ordinal);
        Assert.That(first, Is.GreaterThanOrEqualTo(0));
        int last = normalized.IndexOf(end, first + start.Length,
            StringComparison.Ordinal);
        Assert.That(last, Is.GreaterThan(first));
        return normalized.Substring(first, last - first);
    }
}
