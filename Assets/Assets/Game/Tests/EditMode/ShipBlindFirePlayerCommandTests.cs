using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ShipBlindFirePlayerCommandTests
{
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags PrivateStatic =
        BindingFlags.Static | BindingFlags.NonPublic;

    private readonly List<GameObject> createdRoots =
        new List<GameObject>();

    private ShipSelectionManager selectionManager;
    private ShipCommandDispatcher commandDispatcher;
    private ShipPlayerCommandInput commandInput;
    private GameObject shooterRoot;
    private ShipDestinationController shooterDestination;
    private ShipCombatState shooterState;


    [SetUp]
    public void SetUp()
    {
        GameObject commandRoot = CreateRoot("Command Root");
        selectionManager = commandRoot.AddComponent<ShipSelectionManager>();
        commandDispatcher = commandRoot.AddComponent<ShipCommandDispatcher>();
        commandInput = commandRoot.AddComponent<ShipPlayerCommandInput>();
        SetPrivateField(
            commandDispatcher,
            "selectionManager",
            selectionManager
        );
        SetPrivateField(commandInput, "selectionManager", selectionManager);
        SetPrivateField(
            commandInput,
            "commandDispatcher",
            commandDispatcher
        );
        InvokePrivate(commandInput, "OnDisable");
        InvokePrivate(commandInput, "OnEnable");

        shooterRoot = CreateCombatShip("Blind Fire Shooter");
        shooterDestination =
            shooterRoot.GetComponent<ShipDestinationController>();
        shooterState = shooterRoot.GetComponent<ShipCombatState>();
        ShipFireEligibility eligibility =
            shooterRoot.GetComponent<ShipFireEligibility>();
        SetPrivateField(eligibility, "effectiveRangeMeters", 100f);
        SetPrivateField(eligibility, "maximumRangeMeters", 200f);
        selectionManager.SelectSingle(shooterDestination);
    }


    [TearDown]
    public void TearDown()
    {
        foreach (CombatProjectile projectile in UnityEngine.Object
            .FindObjectsByType<CombatProjectile>(
                FindObjectsInactive.Include
            ))
        {
            UnityEngine.Object.DestroyImmediate(projectile.gameObject);
        }

        for (int index = createdRoots.Count - 1; index >= 0; index--)
        {
            UnityEngine.Object.DestroyImmediate(createdRoots[index]);
        }

        createdRoots.Clear();
    }


    [Test]
    public void ExactlyOneSelectedCombatShip_CanIssueBlindFire()
    {
        bool accepted = commandInput.TryExecuteBlindFireAtWorldPoint(
            Vector3.left * 50f,
            out BlindFirePlayerCommandResult result
        );

        Assert.That(accepted, Is.True);
        Assert.That(result.Accepted, Is.True);
        Assert.That(result.ShooterShipRoot, Is.SameAs(shooterRoot));
        Assert.That(result.Execution.Side, Is.EqualTo(CombatSide.Port));
    }


    [Test]
    public void MultiSelection_DoesNotArmOrBroadcastBlindFire()
    {
        GameObject secondRoot = CreateCombatShip("Second Shooter");
        ShipCombatState secondState =
            secondRoot.GetComponent<ShipCombatState>();
        selectionManager.AddSelection(
            secondRoot.GetComponent<ShipDestinationController>()
        );

        bool armed = commandInput.TryArmBlindFire();
        bool accepted = commandInput.TryExecuteBlindFireAtWorldPoint(
            Vector3.left * 50f,
            out BlindFirePlayerCommandResult result
        );

        Assert.That(armed, Is.False);
        Assert.That(accepted, Is.False);
        Assert.That(result.FailureReasons.HasFlag(
            BlindFirePlayerCommandFailure.SelectionInvalid
        ), Is.True);
        Assert.That(
            shooterState.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(
            secondState.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
    }


    [Test]
    public void ArmedWorldClick_RoutesToAuthoritativeBlindFireCommand()
    {
        Assert.That(commandInput.TryArmBlindFire(), Is.True);
        SetPrivateField(
            commandInput,
            "rightMouseDownWorldPosition",
            Vector3.right * 50f
        );

        InvokePrivate(commandInput, "CommitNormalDestinationClick");

        BlindFirePlayerCommandResult result =
            commandInput.LastBlindFireCommandResult;
        Assert.That(result.Accepted, Is.True);
        Assert.That(result.Execution.Side,
            Is.EqualTo(CombatSide.Starboard));
        Assert.That(
            shooterState.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Reloading)
        );
    }


    [Test]
    public void ArmedAttempt_DoesNotDispatchDestinationOrFormation()
    {
        commandInput.TryArmBlindFire();
        SetPrivateField(
            commandInput,
            "rightMouseDownWorldPosition",
            Vector3.forward * 50f
        );

        InvokePrivate(commandInput, "CommitNormalDestinationClick");

        Assert.That(shooterDestination.HasDestination, Is.False);
        Assert.That(commandDispatcher.DispatchSequence, Is.Zero);
        Assert.That(commandDispatcher.GroupCommandPending, Is.False);
    }


    [Test]
    public void ArmedAttempt_DoesNotAssignManualTarget()
    {
        commandInput.TryArmBlindFire();
        SetPrivateField(
            commandInput,
            "rightMouseDownWorldPosition",
            Vector3.right * 50f
        );

        InvokePrivate(commandInput, "CommitNormalDestinationClick");

        Assert.That(shooterState.ManualTarget, Is.Null);
    }


    [Test]
    public void NormalWorldClick_StillDispatchesDestinationWhenNotArmed()
    {
        Vector3 destination = new Vector3(80f, 0f, 25f);
        SetPrivateField(commandInput, "commandCamera", null);
        SetPrivateField(
            commandInput,
            "rightMouseDownWorldPosition",
            destination
        );

        InvokePrivate(commandInput, "CommitNormalDestinationClick");

        Assert.That(shooterDestination.HasDestination, Is.True);
        Assert.That(commandDispatcher.DispatchSequence, Is.EqualTo(1));
        Assert.That(commandDispatcher.LastWorldDestination,
            Is.EqualTo(destination));
    }


    [Test]
    public void NormalCombatShipCommand_RemainsManualTargetWhenNotArmed()
    {
        GameObject targetRoot = CreateCombatShip("Manual Target");

        bool assigned = commandInput.TryAssignManualTarget(targetRoot);

        Assert.That(assigned, Is.True);
        Assert.That(shooterState.ManualTarget, Is.SameAs(targetRoot));
        Assert.That(commandInput.BlindFireArmed, Is.False);
    }


    [Test]
    public void LegalAttempt_ClearsOneShotArming()
    {
        commandInput.TryArmBlindFire();
        SetPrivateField(commandInput, "rightMouseDownWorldPosition",
            Vector3.left * 50f);

        InvokePrivate(commandInput, "CommitNormalDestinationClick");

        Assert.That(commandInput.BlindFireArmed, Is.False);
    }


    [Test]
    public void RejectedAttempt_ClearsOneShotArmingWithoutFallthrough()
    {
        commandInput.TryArmBlindFire();
        SetPrivateField(commandInput, "rightMouseDownWorldPosition",
            Vector3.forward * 50f);

        InvokePrivate(commandInput, "CommitNormalDestinationClick");

        Assert.That(commandInput.BlindFireArmed, Is.False);
        Assert.That(
            commandInput.LastBlindFireCommandResult.Accepted,
            Is.False
        );
        Assert.That(shooterDestination.HasDestination, Is.False);
        Assert.That(commandDispatcher.DispatchSequence, Is.Zero);
    }


    [Test]
    public void SuccessfulAttempt_DisablesAutoFireThroughPhaseFourCommand()
    {
        shooterState.SetAutoFireEnabled(true);

        commandInput.TryExecuteBlindFireAtWorldPoint(
            Vector3.right * 50f,
            out BlindFirePlayerCommandResult result
        );

        Assert.That(result.Accepted, Is.True);
        Assert.That(shooterState.AutoFireEnabled, Is.False);
    }


    [Test]
    public void RejectedAttempt_LeavesAutoFireEnabled()
    {
        shooterState.SetAutoFireEnabled(true);

        commandInput.TryExecuteBlindFireAtWorldPoint(
            Vector3.forward * 50f,
            out BlindFirePlayerCommandResult result
        );

        Assert.That(result.Accepted, Is.False);
        Assert.That(shooterState.AutoFireEnabled, Is.True);
    }


    [Test]
    public void ExistingManualTarget_SurvivesSuccessfulBlindFire()
    {
        GameObject targetRoot = CreateCombatShip("Existing Manual Target");
        Assert.That(shooterState.AssignManualTarget(targetRoot), Is.True);

        commandInput.TryExecuteBlindFireAtWorldPoint(
            Vector3.left * 50f,
            out BlindFirePlayerCommandResult result
        );

        Assert.That(result.Accepted, Is.True);
        Assert.That(shooterState.ManualTarget, Is.SameAs(targetRoot));
        Assert.That(shooterState.AutoFireEnabled, Is.False);
    }


    [Test]
    public void SelectionChangeFromShipAToNone_CancelsArming()
    {
        Assert.That(commandInput.TryArmBlindFire(), Is.True);

        selectionManager.ClearSelection();

        Assert.That(commandInput.BlindFireArmed, Is.False);
    }


    [Test]
    public void SelectionChangeFromShipAToShipB_CancelsArming()
    {
        GameObject secondRoot = CreateCombatShip("Second Shooter");
        ShipDestinationController secondDestination =
            secondRoot.GetComponent<ShipDestinationController>();
        Assert.That(commandInput.TryArmBlindFire(), Is.True);

        selectionManager.SelectSingle(secondDestination);

        Assert.That(commandInput.BlindFireArmed, Is.False);
    }


    [Test]
    public void SelectionChangeFromShipAToAPlusB_CancelsArming()
    {
        GameObject secondRoot = CreateCombatShip("Second Shooter");
        ShipDestinationController secondDestination =
            secondRoot.GetComponent<ShipDestinationController>();
        Assert.That(commandInput.TryArmBlindFire(), Is.True);

        selectionManager.AddSelection(secondDestination);

        Assert.That(commandInput.BlindFireArmed, Is.False);
    }


    [Test]
    public void CancelledArming_DoesNotFireNewlySelectedShipOnNextWorldClick()
    {
        GameObject secondRoot = CreateCombatShip("Second Shooter");
        ShipDestinationController secondDestination =
            secondRoot.GetComponent<ShipDestinationController>();
        ShipCombatState secondState =
            secondRoot.GetComponent<ShipCombatState>();
        Assert.That(commandInput.TryArmBlindFire(), Is.True);
        selectionManager.SelectSingle(secondDestination);
        SetPrivateField(
            commandInput,
            "rightMouseDownWorldPosition",
            Vector3.right * 50f
        );

        InvokePrivate(commandInput, "CommitNormalDestinationClick");

        Assert.That(commandInput.BlindFireArmed, Is.False);
        Assert.That(
            secondState.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(secondDestination.HasDestination, Is.True);
    }


    [Test]
    public void ComponentDisable_ClearsArming()
    {
        Assert.That(commandInput.TryArmBlindFire(), Is.True);

        InvokePrivate(commandInput, "OnDisable");

        Assert.That(commandInput.BlindFireArmed, Is.False);
    }


    [Test]
    public void DebugPanel_ConsumesAuthoritativePlayerCommandResult()
    {
        commandInput.TryExecuteBlindFireAtWorldPoint(
            Vector3.right * 50f,
            out BlindFirePlayerCommandResult result
        );
        MethodInfo method = typeof(ShipTestPanel).GetMethod(
            "BuildBlindFireSummary",
            PrivateStatic
        );

        Assert.That(method, Is.Not.Null);
        string summary = (string)method.Invoke(null, new object[] { result });
        Assert.That(summary, Does.Contain("STARBOARD"));
        Assert.That(summary, Does.Contain("Arc: PASS"));
        Assert.That(summary, Does.Contain("Execution: ACCEPTED"));
        Assert.That(ReadPanelSource(), Does.Contain(
            "LastBlindFireCommandResult"
        ));
    }


    [Test]
    public void DebugVisualization_DoesNotClassifySideArcOrRange()
    {
        string source = ReadPanelSource();
        string method = ExtractSource(
            source,
            "private void DrawBlindFireVisualization",
            "private void DrawTargetOwnershipLines"
        );

        Assert.That(method, Does.Contain("LastBlindFireCommandResult"));
        Assert.That(method, Does.Not.Contain("TryEvaluate"));
        Assert.That(method, Does.Not.Contain("ShipBroadsideGeometry"));
        Assert.That(method, Does.Not.Contain("Mathf.Atan2"));
        Assert.That(method, Does.Not.Contain("maximumRange"));
    }


    [Test]
    public void BlindFireInputAdapter_WritesNoMovementState()
    {
        shooterRoot.transform.SetPositionAndRotation(
            new Vector3(12f, 2f, -8f),
            Quaternion.Euler(0f, 25f, 0f)
        );
        Vector3 position = shooterRoot.transform.position;
        Quaternion rotation = shooterRoot.transform.rotation;

        commandInput.TryExecuteBlindFireAtWorldPoint(
            shooterRoot.transform.position + shooterRoot.transform.right * 50f,
            out BlindFirePlayerCommandResult result
        );

        Assert.That(result.Accepted, Is.True);
        Assert.That(shooterRoot.transform.position, Is.EqualTo(position));
        Assert.That(shooterRoot.transform.rotation, Is.EqualTo(rotation));
        string source = GetBlindFireInputSource();
        Assert.That(source, Does.Not.Contain("SetDestination"));
        Assert.That(source, Does.Not.Contain("DispatchDestination"));
        Assert.That(source, Does.Not.Contain("SetTargetHeading"));
        Assert.That(source, Does.Not.Contain("CancelTack"));
        Assert.That(source, Does.Not.Contain("CancelWear"));
        Assert.That(source, Does.Not.Contain("transform.position ="));
        Assert.That(source, Does.Not.Contain("transform.rotation ="));
    }


    [Test]
    public void BlindFireInputAdapter_HasNoPhysicalFiringDependency()
    {
        string source = GetBlindFireInputSource();

        Assert.That(source, Does.Not.Contain("ShotSample"));
        Assert.That(source, Does.Not.Contain("Projectile"));
        Assert.That(source, Does.Not.Contain("CombatVFX"));
        Assert.That(source, Does.Not.Contain("Damage"));
        Assert.That(source, Does.Not.Contain("Dispersion"));
    }


    [Test]
    public void BlindFireInputAdapter_DelegatesExecutionToCommandOwner()
    {
        string source = GetBlindFireInputSource();

        Assert.That(source, Does.Contain("ShipBlindFireCommand"));
        Assert.That(source, Does.Contain(
            "blindFireCommand.TryExecuteBlindFire"
        ));
        Assert.That(source, Does.Not.Contain(
            "fireEligibility.TryExecuteBlindFire"
        ));
        Assert.That(source, Does.Not.Contain("TryCommitBroadsideFire"));
        Assert.That(source, Does.Not.Contain("SetAutoFireEnabled"));
    }


    [Test]
    public void RuntimeAndEditorAssemblyBoundaries_RemainCorrect()
    {
        Assert.That(
            typeof(ShipPlayerCommandInput).Assembly,
            Is.EqualTo(typeof(ShipCombatState).Assembly)
        );
        Assert.That(
            typeof(BlindFirePlayerCommandResult).Assembly,
            Is.EqualTo(typeof(ShipCombatState).Assembly)
        );
        Assert.That(
            typeof(ShipBlindFireCommand).Assembly,
            Is.EqualTo(typeof(ShipCombatState).Assembly)
        );
        Assert.That(
            typeof(ShipTestPanel).Assembly,
            Is.Not.EqualTo(typeof(ShipCombatState).Assembly)
        );
        Assert.That(
            typeof(ShipTestPanel).Assembly.GetName().Name,
            Is.EqualTo("AgeOfSailRTS.Editor")
        );
    }


    [Test]
    public void PlayerCommandResult_IsImmutableReadOnlyValue()
    {
        Type type = typeof(BlindFirePlayerCommandResult);

        Assert.That(type.IsValueType, Is.True);
        Assert.That(type.GetFields(
            BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly
        ).All(field => field.IsInitOnly), Is.True);
        Assert.That(type.GetProperties(
            BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.DeclaredOnly
        ).All(property => property.SetMethod == null), Is.True);
    }


    private GameObject CreateCombatShip(string name)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Assets/Game/Ship/Proxy/"
                + "PF_Ship_Gelderland_Combat_v01.prefab"
        );
        Assert.That(prefab, Is.Not.Null);
        GameObject root = UnityEngine.Object.Instantiate(prefab);
        CombatLifecycleTestUtility.EnsureOperational(root);
        root.name = name;
        root.GetComponent<CombatVFXPlaceholderReceiver>()
            .VisualSpawningEnabled = false;
        createdRoots.Add(root);
        return root;
    }


    private GameObject CreateRoot(string name)
    {
        GameObject root = new GameObject(name);
        createdRoots.Add(root);
        return root;
    }


    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            PrivateInstance
        );
        Assert.That(method, Is.Not.Null, $"Missing method {methodName}.");
        method.Invoke(target, null);
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


    private static string GetBlindFireInputSource()
    {
        string source = File.ReadAllText(Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Sailing/ShipPlayerCommandInput.cs"
        ));
        return ExtractSource(
            source,
            "public bool TryArmBlindFire",
            "private static bool TryResolveCombatShipRoot"
        );
    }


    private static string ReadPanelSource()
    {
        return File.ReadAllText(Path.Combine(
            Application.dataPath,
            "Assets/Game/Editor/Debug/ShipTestPanel.cs"
        ));
    }


    private static string ExtractSource(
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
}
