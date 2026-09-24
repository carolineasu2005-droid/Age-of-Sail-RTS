using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ShipTestPanelTests
{
    private const BindingFlags PrivateStatic =
        BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly List<GameObject> createdRoots =
        new List<GameObject>();

    private GameObject shipRoot;
    private GameObject nonCombatRoot;
    private ShipCombatState combatState;
    private ShipTestPanel panel;


    [SetUp]
    public void SetUp()
    {
        shipRoot = new GameObject("Combat Ship Root");
        nonCombatRoot = new GameObject("Movement-Only Ship Root");
        createdRoots.Add(shipRoot);
        createdRoots.Add(nonCombatRoot);
        combatState = shipRoot.AddComponent<ShipCombatState>();
        panel = ScriptableObject.CreateInstance<ShipTestPanel>();
    }


    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(panel);

        for (int index = createdRoots.Count - 1; index >= 0; index--)
        {
            UnityEngine.Object.DestroyImmediate(createdRoots[index]);
        }

        createdRoots.Clear();
    }


    [Test]
    public void ResolveCombatState_NoTargetFailsSafely()
    {
        object[] arguments = { null, null, null };

        bool resolved = InvokePrivateStatic<bool>(
            "TryGetCombatState",
            arguments
        );

        Assert.That(resolved, Is.False);
        Assert.That(arguments[1], Is.Null);
        Assert.That((string)arguments[2], Does.Contain("Assign"));
    }


    [Test]
    public void ResolveCombatState_TargetWithoutOwnerFailsSafely()
    {
        object[] arguments = { nonCombatRoot, null, null };

        bool resolved = InvokePrivateStatic<bool>(
            "TryGetCombatState",
            arguments
        );

        Assert.That(resolved, Is.False);
        Assert.That(arguments[1], Is.Null);
        Assert.That(
            (string)arguments[2],
            Does.Contain("does not have a ShipCombatState")
        );
    }


    [Test]
    public void ResolveCombatState_UsesOwnerOnExplicitTargetRoot()
    {
        object[] arguments = { shipRoot, null, null };

        bool resolved = InvokePrivateStatic<bool>(
            "TryGetCombatState",
            arguments
        );

        Assert.That(resolved, Is.True);
        Assert.That(arguments[1], Is.SameAs(combatState));
        Assert.That(arguments[2], Is.EqualTo(string.Empty));
    }


    [Test]
    public void DebugPortAction_DelegatesToCombatStateWithoutRestartingTimer()
    {
        bool firstCommit = InvokePrivateStatic<bool>(
            "TryDebugFire",
            combatState,
            CombatSide.Port
        );
        float remainingAfterFirstCommit =
            combatState.PortReloadRemainingSeconds;

        bool secondCommit = InvokePrivateStatic<bool>(
            "TryDebugFire",
            combatState,
            CombatSide.Port
        );

        Assert.That(firstCommit, Is.True);
        Assert.That(secondCommit, Is.False);
        Assert.That(
            combatState.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Reloading)
        );
        Assert.That(
            combatState.PortReloadRemainingSeconds,
            Is.EqualTo(remainingAfterFirstCommit)
        );
        Assert.That(
            combatState.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
    }


    [Test]
    public void DebugStarboardAction_DelegatesToCombatStateIndependently()
    {
        bool committed = InvokePrivateStatic<bool>(
            "TryDebugFire",
            combatState,
            CombatSide.Starboard
        );

        Assert.That(committed, Is.True);
        Assert.That(
            combatState.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Reloading)
        );
        Assert.That(
            combatState.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(combatState.PortReloadRemainingSeconds, Is.Zero);
    }


    [Test]
    public void AutoFireAction_DelegatesToCombatCommand()
    {
        InvokePrivateStatic<object>("ToggleAutoFire", combatState);

        Assert.That(combatState.AutoFireEnabled, Is.True);

        InvokePrivateStatic<object>("ToggleAutoFire", combatState);

        Assert.That(combatState.AutoFireEnabled, Is.False);
    }


    [Test]
    public void PanelSource_HasNoObsoleteBlindFireModeToggle()
    {
        string source = ReadPanelSource();

        Assert.That(source, Does.Not.Contain("Toggle Blind Fire"));
        Assert.That(source, Does.Not.Contain("ToggleBlindFire"));
        Assert.That(source, Does.Not.Contain("BlindFireEnabled"));
        Assert.That(source, Does.Not.Contain("SetBlindFireEnabled"));
    }


    [Test]
    public void DebugActions_DoNotWriteShipRootTransform()
    {
        shipRoot.transform.SetPositionAndRotation(
            new Vector3(14f, 2f, -9f),
            Quaternion.Euler(0f, 71f, 0f)
        );
        shipRoot.transform.localScale = new Vector3(1.1f, 0.9f, 1.2f);
        Vector3 position = shipRoot.transform.position;
        Quaternion rotation = shipRoot.transform.rotation;
        Vector3 scale = shipRoot.transform.localScale;

        InvokePrivateStatic<object>("ToggleAutoFire", combatState);
        InvokePrivateStatic<bool>(
            "TryDebugFire",
            combatState,
            CombatSide.Port
        );
        InvokePrivateStatic<bool>(
            "TryDebugFire",
            combatState,
            CombatSide.Starboard
        );

        Assert.That(shipRoot.transform.position, Is.EqualTo(position));
        Assert.That(shipRoot.transform.rotation, Is.EqualTo(rotation));
        Assert.That(shipRoot.transform.localScale, Is.EqualTo(scale));
    }


    [Test]
    public void DynamicCandidateList_AcceptsMoreThanTwoAndPreservesReorder()
    {
        GameObject third = CreateRoot("Third Candidate");
        GameObject fourth = CreateRoot("Fourth Candidate");
        IList debugList = CreateDebugCandidateList(
            new[] { shipRoot, nonCombatRoot, third, fourth },
            new[] { true, false, true, false }
        );
        SetPrivateField(panel, "autoTargetCandidates", debugList);

        bool moved = InvokePrivateStatic<bool>(
            "TryMoveAutoTargetCandidate",
            debugList,
            3,
            1
        );
        IReadOnlyList<AutoTargetCandidate> candidates =
            InvokePrivateInstance<IReadOnlyList<AutoTargetCandidate>>(
                panel,
                "BuildAutoTargetCandidates"
            );

        Assert.That(moved, Is.True);
        Assert.That(candidates.Count, Is.EqualTo(4));
        Assert.That(candidates[0].TargetShipRoot, Is.SameAs(shipRoot));
        Assert.That(candidates[1].TargetShipRoot, Is.SameAs(fourth));
        Assert.That(candidates[2].TargetShipRoot, Is.SameAs(nonCombatRoot));
        Assert.That(candidates[3].TargetShipRoot, Is.SameAs(third));
        Assert.That(candidates[0].RelationshipAllowsFire, Is.True);
        Assert.That(candidates[1].RelationshipAllowsFire, Is.False);
        Assert.That(candidates[2].RelationshipAllowsFire, Is.False);
        Assert.That(candidates[3].RelationshipAllowsFire, Is.True);
        Assert.That(combatState.AutoFireEnabled, Is.False);
        Assert.That(combatState.ManualTarget, Is.Null);
        Assert.That(
            combatState.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(
            combatState.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
    }


    [Test]
    public void DebugSelector_ForwardsCompleteCollectionAndReturnsBothSides()
    {
        ConfigureCombatShip(shipRoot, true);
        SetPrivateField(
            shipRoot.GetComponent<ShipFireEligibility>(),
            "effectiveRangeMeters",
            100f
        );
        SetPrivateField(
            shipRoot.GetComponent<ShipFireEligibility>(),
            "maximumRangeMeters",
            200f
        );
        GameObject weakStarboard = CreateCombatShip(
            "Weak Starboard",
            Vector3.right * 50f,
            Quaternion.Euler(0f, 90f, 0f)
        );
        GameObject weakPort = CreateCombatShip(
            "Weak Port",
            Vector3.left * 50f,
            Quaternion.Euler(0f, 90f, 0f)
        );
        GameObject bestStarboard = CreateCombatShip(
            "Best Starboard",
            Vector3.right * 75f,
            Quaternion.identity
        );
        GameObject bestPort = CreateCombatShip(
            "Best Port",
            Vector3.left * 75f,
            Quaternion.identity
        );
        combatState.SetAutoFireEnabled(true);
        IReadOnlyList<AutoTargetCandidate> candidates = new[]
        {
            new AutoTargetCandidate(weakStarboard, true),
            new AutoTargetCandidate(weakPort, true),
            new AutoTargetCandidate(bestStarboard, true),
            new AutoTargetCandidate(bestPort, true)
        };
        object[] arguments =
        {
            shipRoot,
            candidates,
            default(AutoTargetSelectionResult),
            null
        };

        Physics.SyncTransforms();
        bool selected = InvokePrivateStatic<bool>(
            "TrySelectAutoTargetForDebug",
            arguments
        );
        AutoTargetSelectionResult result =
            (AutoTargetSelectionResult)arguments[2];

        Assert.That(selected, Is.True);
        Assert.That(result.PortSelection.HasTarget, Is.True);
        Assert.That(
            result.PortSelection.TargetShipRoot,
            Is.SameAs(bestPort)
        );
        Assert.That(result.PortSelection.CandidateIndex, Is.EqualTo(3));
        Assert.That(result.StarboardSelection.HasTarget, Is.True);
        Assert.That(
            result.StarboardSelection.TargetShipRoot,
            Is.SameAs(bestStarboard)
        );
        Assert.That(result.StarboardSelection.CandidateIndex, Is.EqualTo(2));
        Assert.That(arguments[3], Is.EqualTo(string.Empty));
    }


    [Test]
    public void ManualMode_DebugSelectorReportsBothAutoSidesInactive()
    {
        GameObject manualTarget = CreateRoot("Manual Combat Target");
        manualTarget.AddComponent<ShipCombatState>();
        combatState.SetAutoFireEnabled(true);
        Assert.That(combatState.AssignManualTarget(manualTarget), Is.True);
        object[] arguments =
        {
            shipRoot,
            new[] { new AutoTargetCandidate(manualTarget, true) },
            default(AutoTargetSelectionResult),
            null
        };

        bool selected = InvokePrivateStatic<bool>(
            "TrySelectAutoTargetForDebug",
            arguments
        );
        AutoTargetSelectionResult result =
            (AutoTargetSelectionResult)arguments[2];

        Assert.That(selected, Is.False);
        Assert.That(result.HasAnyTarget, Is.False);
        Assert.That(result.PortSelection.HasTarget, Is.False);
        Assert.That(result.StarboardSelection.HasTarget, Is.False);
        Assert.That(combatState.AutoFireEnabled, Is.False);
        Assert.That(combatState.ManualTarget, Is.SameAs(manualTarget));
    }


    [Test]
    public void PanelSource_ConsumesPerSideResultWithoutDuplicatingRules()
    {
        string source = ReadPanelSource();
        string selectionMethod = ExtractMethodBlock(
            source,
            "private static bool TrySelectAutoTargetForDebug",
            "private static string BuildEligibilitySummary"
        );

        Assert.That(source, Does.Contain("selection.PortSelection"));
        Assert.That(source, Does.Contain("selection.StarboardSelection"));
        Assert.That(source, Does.Contain("AUTO TARGET — {sideName}"));
        Assert.That(source, Does.Not.Contain("Current Auto Target"));
        Assert.That(source, Does.Not.Contain("ShipAutoTargetScorer"));
        Assert.That(selectionMethod, Does.Contain("ShipAutoTargetSelector.TrySelect"));
        Assert.That(selectionMethod, Does.Not.Contain("TryEvaluate("));
        Assert.That(selectionMethod, Does.Not.Contain("Mathf."));
        Assert.That(source, Does.Not.Contain("FindObjectsByType"));
        Assert.That(source, Does.Not.Contain("FindObjectsOfType"));
        Assert.That(source, Does.Not.Contain("OverlapSphere"));
        Assert.That(source, Does.Not.Contain("Physics.Raycast"));
        Assert.That(source, Does.Not.Contain("Physics.Linecast"));
    }


    [Test]
    public void PanelSource_HasNoStateBypassOrProjectileDependency()
    {
        string source = ReadPanelSource();

        Assert.That(source, Does.Not.Contain("SerializedObject"));
        Assert.That(source, Does.Not.Contain("System.Reflection"));
        Assert.That(source, Does.Not.Contain("Projectile"));
        Assert.That(source, Does.Not.Contain("ShotSample"));
        Assert.That(source, Does.Not.Contain("portReloadRemainingSeconds"));
        Assert.That(
            source,
            Does.Not.Contain("starboardReloadRemainingSeconds")
        );
        Assert.That(source, Does.Not.Contain(".transform.position ="));
        Assert.That(source, Does.Not.Contain(".transform.rotation ="));
        Assert.That(source, Does.Not.Contain("SetPositionAndRotation"));
        Assert.That(source, Does.Not.Contain("Physics.Raycast"));
        Assert.That(source, Does.Not.Contain("Physics.Linecast"));
        Assert.That(
            typeof(ShipTestPanel).Assembly.GetName().Name,
            Is.EqualTo("AgeOfSailRTS.Editor")
        );
    }


    private GameObject CreateRoot(string name)
    {
        GameObject root = new GameObject(name);
        createdRoots.Add(root);
        return root;
    }


    private GameObject CreateCombatShip(
        string name,
        Vector3 position,
        Quaternion rotation
    )
    {
        GameObject root = CreateRoot(name);
        root.transform.SetPositionAndRotation(position, rotation);
        ConfigureCombatShip(root, false);
        return root;
    }


    private static void ConfigureCombatShip(
        GameObject root,
        bool includeEligibility
    )
    {
        if (root.GetComponent<ShipCombatState>() == null)
        {
            root.AddComponent<ShipCombatState>();
        }

        ShipArtDefinition artDefinition =
            root.GetComponent<ShipArtDefinition>();

        if (artDefinition == null)
        {
            artDefinition = root.AddComponent<ShipArtDefinition>();
            SetArtReferences(root, artDefinition);
        }

        if (root.GetComponent<ShipCombatGeometry>() == null)
        {
            root.AddComponent<ShipCombatGeometry>();
        }

        ShipExposureReference exposure =
            root.GetComponent<ShipExposureReference>();

        if (exposure == null)
        {
            exposure = root.AddComponent<ShipExposureReference>();
            SetPrivateField(
                exposure,
                "shipArtDefinition",
                artDefinition
            );
        }

        if (includeEligibility
            && root.GetComponent<ShipFireEligibility>() == null)
        {
            root.AddComponent<ShipFireEligibility>();
        }
    }


    private static void SetArtReferences(
        GameObject root,
        ShipArtDefinition artDefinition
    )
    {
        SetArtReference(root, artDefinition, "centerReference", Vector3.zero);
        SetArtReference(root, artDefinition, "waterlineReference", Vector3.zero);
        SetArtReference(root, artDefinition, "deckReference", Vector3.up * 4f);
        SetArtReference(root, artDefinition, "bowReference", Vector3.forward * 15f);
        SetArtReference(root, artDefinition, "sternReference", Vector3.back * 15f);
        SetArtReference(root, artDefinition, "portReference", Vector3.left * 5f);
        SetArtReference(root, artDefinition, "starboardReference", Vector3.right * 5f);
    }


    private static void SetArtReference(
        GameObject root,
        ShipArtDefinition artDefinition,
        string fieldName,
        Vector3 localPosition
    )
    {
        GameObject referenceObject = new GameObject(fieldName);
        referenceObject.transform.SetParent(root.transform, false);
        referenceObject.transform.localPosition = localPosition;
        SetPrivateField(
            artDefinition,
            fieldName,
            referenceObject.transform
        );
    }


    private static IList CreateDebugCandidateList(
        IReadOnlyList<GameObject> roots,
        IReadOnlyList<bool> relationshipFlags
    )
    {
        Type candidateType = typeof(ShipTestPanel).GetNestedType(
            "AutoTargetDebugCandidate",
            BindingFlags.NonPublic
        );
        Assert.That(candidateType, Is.Not.Null);
        Type listType = typeof(List<>).MakeGenericType(candidateType);
        IList list = (IList)Activator.CreateInstance(listType);
        FieldInfo targetField = candidateType.GetField(
            "targetShipRoot",
            BindingFlags.Instance | BindingFlags.Public
        );
        FieldInfo relationshipField = candidateType.GetField(
            "relationshipAllowsFire",
            BindingFlags.Instance | BindingFlags.Public
        );
        Assert.That(targetField, Is.Not.Null);
        Assert.That(relationshipField, Is.Not.Null);
        Assert.That(roots.Count, Is.EqualTo(relationshipFlags.Count));

        for (int index = 0; index < roots.Count; index++)
        {
            object entry = Activator.CreateInstance(candidateType, true);
            targetField.SetValue(entry, roots[index]);
            relationshipField.SetValue(entry, relationshipFlags[index]);
            list.Add(entry);
        }

        return list;
    }


    private static string ReadPanelSource()
    {
        string sourcePath = Path.Combine(
            Application.dataPath,
            "Assets/Game/Editor/Debug/ShipTestPanel.cs"
        );
        return File.ReadAllText(sourcePath);
    }


    private static string ExtractMethodBlock(
        string source,
        string startMarker,
        string endMarker
    )
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(
            endMarker,
            start,
            StringComparison.Ordinal
        );
        Assert.That(start, Is.GreaterThanOrEqualTo(0));
        Assert.That(end, Is.GreaterThan(start));
        return source.Substring(start, end - start);
    }


    private static T InvokePrivateInstance<T>(
        object target,
        string methodName,
        params object[] arguments
    )
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            PrivateInstance
        );
        Assert.That(method, Is.Not.Null, $"Missing method {methodName}.");
        object result = method.Invoke(target, arguments);
        return result == null ? default : (T)result;
    }


    private static void SetPrivateField(
        object target,
        string fieldName,
        object value
    )
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            PrivateInstance
        );
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}.");
        field.SetValue(target, value);
    }


    private static T InvokePrivateStatic<T>(
        string methodName,
        params object[] arguments
    )
    {
        MethodInfo method = typeof(ShipTestPanel).GetMethod(
            methodName,
            PrivateStatic
        );

        Assert.That(method, Is.Not.Null, $"Missing method {methodName}.");
        object result = method.Invoke(null, arguments);
        return result == null ? default : (T)result;
    }
}
