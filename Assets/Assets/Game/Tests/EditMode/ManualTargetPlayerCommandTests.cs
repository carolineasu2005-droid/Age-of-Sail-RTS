using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ManualTargetPlayerCommandTests
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
    private GameObject targetRoot;
    private GameObject alternateTargetRoot;
    private ShipDestinationController shooterDestination;
    private ShipCombatState shooterState;
    private ShipFireEligibility eligibility;
    private AutoTargetScoringProfile scoringProfile;


    [SetUp]
    public void SetUp()
    {
        GameObject commandRoot = new GameObject("Command Root");
        createdRoots.Add(commandRoot);
        selectionManager = commandRoot.AddComponent<ShipSelectionManager>();
        commandDispatcher = commandRoot.AddComponent<ShipCommandDispatcher>();
        commandInput = commandRoot.AddComponent<ShipPlayerCommandInput>();
        SetPrivateField(
            commandDispatcher,
            "selectionManager",
            selectionManager
        );
        SetPrivateField(
            commandInput,
            "selectionManager",
            selectionManager
        );
        SetPrivateField(
            commandInput,
            "commandDispatcher",
            commandDispatcher
        );
        commandInput.enabled = false;
        commandInput.enabled = true;

        shooterRoot = CreateCombatShip("Shooter Root", true);
        targetRoot = CreateCombatShip("Target Root", false);
        alternateTargetRoot = CreateCombatShip(
            "Alternate Target Root",
            false
        );
        shooterDestination =
            shooterRoot.GetComponent<ShipDestinationController>();
        shooterState = shooterRoot.GetComponent<ShipCombatState>();
        eligibility = shooterRoot.GetComponent<ShipFireEligibility>();
        scoringProfile = ScriptableObject.CreateInstance<
            AutoTargetScoringProfile
        >();
        ShipAutoTargetScoringConfiguration scoringConfiguration =
            shooterRoot.AddComponent<
                ShipAutoTargetScoringConfiguration
            >();
        SetPrivateField(
            scoringConfiguration,
            "scoringProfile",
            scoringProfile
        );
        SetPrivateField(eligibility, "effectiveRangeMeters", 100f);
        SetPrivateField(eligibility, "maximumRangeMeters", 200f);
        targetRoot.transform.position = Vector3.right * 50f;
        alternateTargetRoot.transform.position = Vector3.left * 150f;
        selectionManager.SelectSingle(shooterDestination);
    }


    [TearDown]
    public void TearDown()
    {
        for (int index = createdRoots.Count - 1; index >= 0; index--)
        {
            Object.DestroyImmediate(createdRoots[index]);
        }

        createdRoots.Clear();
        Object.DestroyImmediate(scoringProfile);
    }


    [Test]
    public void ValidCombatShipClick_AssignsManualTarget()
    {
        bool assigned = commandInput.TryAssignManualTarget(targetRoot);

        Assert.That(assigned, Is.True);
        Assert.That(shooterState.ManualTarget, Is.SameAs(targetRoot));
    }


    [Test]
    public void NormalRightClickHit_InterceptsDestinationForManualTarget()
    {
        GameObject hitChild = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hitChild.name = "Target Click Child";
        hitChild.transform.SetParent(targetRoot.transform, false);
        hitChild.transform.localScale = Vector3.one * 5f;
        GameObject cameraRoot = new GameObject("Command Camera");
        createdRoots.Add(cameraRoot);
        Camera commandCamera = cameraRoot.AddComponent<Camera>();
        cameraRoot.transform.position =
            targetRoot.transform.position + new Vector3(0f, 20f, -50f);
        cameraRoot.transform.rotation = Quaternion.LookRotation(
            targetRoot.transform.position - cameraRoot.transform.position
        );
        Vector3 screenPoint = commandCamera.WorldToScreenPoint(
            targetRoot.transform.position
        );
        SetPrivateField(commandInput, "commandCamera", commandCamera);
        SetPrivateField(
            commandInput,
            "rightMouseDownScreenPosition",
            new Vector2(screenPoint.x, screenPoint.y)
        );
        SetPrivateField(
            commandInput,
            "rightMouseDownWorldPosition",
            new Vector3(75f, 0f, 25f)
        );
        Physics.SyncTransforms();

        InvokePrivateInstance(commandInput, "CommitNormalDestinationClick");

        Assert.That(shooterState.ManualTarget, Is.SameAs(targetRoot));
        Assert.That(shooterDestination.HasDestination, Is.False);
        Assert.That(commandDispatcher.DispatchSequence, Is.Zero);
    }


    [Test]
    public void AutoFireOn_ManualTargetCommandTurnsAutoFireOff()
    {
        shooterState.SetAutoFireEnabled(true);

        bool assigned = commandInput.TryAssignManualTarget(targetRoot);

        Assert.That(assigned, Is.True);
        Assert.That(shooterState.AutoFireEnabled, Is.False);
        Assert.That(shooterState.ManualTarget, Is.SameAs(targetRoot));
    }


    [Test]
    public void ManualTargetCommand_PreventsAutoSelectorTakeover()
    {
        shooterState.SetAutoFireEnabled(true);
        commandInput.TryAssignManualTarget(targetRoot);

        bool selected = ShipAutoTargetSelector.TrySelect(
            shooterRoot,
            new[]
            {
                new AutoTargetCandidate(alternateTargetRoot, true)
            },
            out AutoTargetSelectionResult selection
        );

        Assert.That(selected, Is.False);
        Assert.That(selection.HasAnyTarget, Is.False);
        Assert.That(selection.PortSelection.HasTarget, Is.False);
        Assert.That(selection.StarboardSelection.HasTarget, Is.False);
        Assert.That(shooterState.ManualTarget, Is.SameAs(targetRoot));
    }


    [Test]
    public void NonCombatClick_FallsThroughToExistingDestinationCommand()
    {
        GameObject water = new GameObject("Water");
        createdRoots.Add(water);
        Vector3 destination = new Vector3(80f, 0f, 25f);
        SetPrivateField(commandInput, "commandCamera", null);
        SetPrivateField(
            commandInput,
            "rightMouseDownWorldPosition",
            destination
        );

        bool intercepted = commandInput.TryAssignManualTarget(water);
        InvokePrivateInstance(commandInput, "CommitNormalDestinationClick");

        Assert.That(intercepted, Is.False);
        Assert.That(shooterState.ManualTarget, Is.Null);
        Assert.That(shooterDestination.HasDestination, Is.True);
        Assert.That(
            commandDispatcher.LastDispatchResult,
            Is.EqualTo(ShipCommandDispatcher.DispatchResult.SingleShip)
        );
        Assert.That(
            commandDispatcher.LastWorldDestination,
            Is.EqualTo(destination)
        );
    }


    [Test]
    public void OutOfArcManualTarget_RemainsAssignedWithoutFireEligibility()
    {
        commandInput.TryAssignManualTarget(targetRoot);
        targetRoot.transform.position = Vector3.forward * 50f;

        FireEligibilityResult result = EvaluateManualTarget();

        Assert.That(result.CanFire, Is.False);
        Assert.That(result.InBroadsideArc, Is.False);
        Assert.That(shooterState.ManualTarget, Is.SameAs(targetRoot));
    }


    [Test]
    public void OutOfRangeManualTarget_RemainsAssignedWithoutFireEligibility()
    {
        commandInput.TryAssignManualTarget(targetRoot);
        targetRoot.transform.position = Vector3.right * 201f;

        FireEligibilityResult result = EvaluateManualTarget();

        Assert.That(result.CanFire, Is.False);
        Assert.That(result.WithinMaximumRange, Is.False);
        Assert.That(shooterState.ManualTarget, Is.SameAs(targetRoot));
    }


    [Test]
    public void BlockedManualTarget_RemainsAssignedWithoutFireEligibility()
    {
        commandInput.TryAssignManualTarget(targetRoot);
        GameObject blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blocker.name = "Combat Geometry Blocker";
        blocker.layer = 8;
        blocker.transform.position = Vector3.right * 25f;
        createdRoots.Add(blocker);

        FireEligibilityResult result = EvaluateManualTarget();

        Assert.That(result.CanFire, Is.False);
        Assert.That(result.Blocked, Is.True);
        Assert.That(shooterState.ManualTarget, Is.SameAs(targetRoot));
    }


    [Test]
    public void TargetFailure_DoesNotMoveOrRotateShooter()
    {
        shooterRoot.transform.SetPositionAndRotation(
            new Vector3(10f, 2f, -20f),
            Quaternion.Euler(0f, 35f, 0f)
        );
        targetRoot.transform.position =
            shooterRoot.transform.position + shooterRoot.transform.forward * 50f;
        Vector3 position = shooterRoot.transform.position;
        Quaternion rotation = shooterRoot.transform.rotation;

        commandInput.TryAssignManualTarget(targetRoot);
        FireEligibilityResult result = EvaluateManualTarget();

        Assert.That(result.CanFire, Is.False);
        Assert.That(shooterRoot.transform.position, Is.EqualTo(position));
        Assert.That(shooterRoot.transform.rotation, Is.EqualTo(rotation));
        Assert.That(shooterDestination.HasDestination, Is.False);
        Assert.That(shooterState.ManualTarget, Is.SameAs(targetRoot));
    }


    [Test]
    public void CombatChildClick_ResolvesAuthoritativeShipRoot()
    {
        GameObject combatChild = new GameObject("Combat Geometry Child");
        combatChild.transform.SetParent(targetRoot.transform, false);
        combatChild.AddComponent<BoxCollider>();

        bool assigned = commandInput.TryAssignManualTarget(combatChild);

        Assert.That(assigned, Is.True);
        Assert.That(shooterState.ManualTarget, Is.SameAs(targetRoot));
        Assert.That(shooterState.ManualTarget, Is.Not.SameAs(combatChild));
    }


    [Test]
    public void MultipleSelection_DoesNotBroadcastManualTarget()
    {
        GameObject secondShooter = CreateCombatShip(
            "Second Shooter Root",
            false
        );
        ShipCombatState secondState =
            secondShooter.GetComponent<ShipCombatState>();
        shooterState.SetAutoFireEnabled(true);
        secondState.SetAutoFireEnabled(true);
        selectionManager.AddSelection(
            secondShooter.GetComponent<ShipDestinationController>()
        );

        bool assigned = commandInput.TryAssignManualTarget(targetRoot);

        Assert.That(selectionManager.SelectedCount, Is.EqualTo(2));
        Assert.That(assigned, Is.False);
        Assert.That(shooterState.ManualTarget, Is.Null);
        Assert.That(secondState.ManualTarget, Is.Null);
        Assert.That(shooterState.AutoFireEnabled, Is.True);
        Assert.That(secondState.AutoFireEnabled, Is.True);
    }


    [Test]
    public void ManualTargetCommand_DoesNotCommitReloadOrFire()
    {
        bool assigned = commandInput.TryAssignManualTarget(targetRoot);

        Assert.That(assigned, Is.True);
        Assert.That(
            shooterState.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(
            shooterState.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(shooterState.PortReloadRemainingSeconds, Is.Zero);
        Assert.That(shooterState.StarboardReloadRemainingSeconds, Is.Zero);

        string sourcePath = Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Sailing/ShipPlayerCommandInput.cs"
        );
        string source = File.ReadAllText(sourcePath);
        int commandStart = source.IndexOf(
            "public bool TryAssignManualTarget"
        );
        int commandEnd = source.IndexOf(
            "private static bool TryResolveCombatShipRoot",
            commandStart
        );
        string commandSource = source.Substring(
            commandStart,
            commandEnd - commandStart
        );

        Assert.That(commandSource, Does.Not.Contain("TryCommitBroadsideFire"));
        Assert.That(commandSource, Does.Not.Contain("Projectile"));
        Assert.That(commandSource, Does.Not.Contain("ShipHeadingController"));
        Assert.That(commandSource, Does.Not.Contain("SetDestination"));
        Assert.That(commandSource, Does.Not.Contain("SetTargetHeading"));
        Assert.That(commandSource, Does.Not.Contain("transform.position ="));
        Assert.That(commandSource, Does.Not.Contain("transform.rotation ="));
    }


    [Test]
    public void DebugAutoTargetSelection_ReadsResultWithoutMutation()
    {
        shooterState.SetAutoFireEnabled(true);
        ShipExposureReference targetExposure =
            targetRoot.GetComponent<ShipExposureReference>();
        ShipArtDefinition targetArtDefinition =
            targetExposure.ArtDefinition;
        Transform targetBowReference = targetArtDefinition.BowReference;
        Vector3 targetBowLocalPosition =
            targetBowReference.localPosition;
        Vector3 shooterPosition = shooterRoot.transform.position;
        Quaternion shooterRotation = shooterRoot.transform.rotation;
        BroadsideReloadState portState = shooterState.PortBroadsideState;
        BroadsideReloadState starboardState =
            shooterState.StarboardBroadsideState;
        IReadOnlyList<AutoTargetCandidate> candidates =
            new[]
            {
                new AutoTargetCandidate(targetRoot, true),
                new AutoTargetCandidate(alternateTargetRoot, true)
            };
        object[] arguments =
        {
            shooterRoot,
            candidates,
            default(AutoTargetSelectionResult),
            null
        };

        bool selected = InvokePrivateStatic<bool>(
            typeof(ShipTestPanel),
            "TrySelectAutoTargetForDebug",
            arguments
        );
        AutoTargetSelectionResult result =
            (AutoTargetSelectionResult)arguments[2];

        Assert.That(selected, Is.True);
        Assert.That(result.HasAnyTarget, Is.True);
        Assert.That(result.PortSelection.HasTarget, Is.True);
        Assert.That(
            result.PortSelection.TargetShipRoot,
            Is.SameAs(alternateTargetRoot)
        );
        Assert.That(result.PortSelection.Score.Selectable, Is.True);
        Assert.That(
            result.PortSelection.Score.VisibilityQualityNormalized,
            Is.EqualTo(1f)
        );
        Assert.That(result.StarboardSelection.HasTarget, Is.True);
        Assert.That(
            result.StarboardSelection.TargetShipRoot,
            Is.SameAs(targetRoot)
        );
        Assert.That(result.StarboardSelection.Score.Selectable, Is.True);
        Assert.That(
            result.StarboardSelection.Score.VisibilityQualityNormalized,
            Is.EqualTo(1f)
        );
        Assert.That((string)arguments[3], Is.EqualTo(string.Empty));
        Assert.That(shooterRoot.transform.position, Is.EqualTo(shooterPosition));
        Assert.That(shooterRoot.transform.rotation, Is.EqualTo(shooterRotation));
        Assert.That(shooterState.PortBroadsideState, Is.EqualTo(portState));
        Assert.That(
            shooterState.StarboardBroadsideState,
            Is.EqualTo(starboardState)
        );
        Assert.That(shooterState.AutoFireEnabled, Is.True);
        Assert.That(shooterState.ManualTarget, Is.Null);
        Assert.That(
            targetExposure.ArtDefinition,
            Is.SameAs(targetArtDefinition)
        );
        Assert.That(
            targetArtDefinition.BowReference,
            Is.SameAs(targetBowReference)
        );
        Assert.That(
            targetBowReference.localPosition,
            Is.EqualTo(targetBowLocalPosition)
        );
    }


    [Test]
    public void DebugAutoTargetSelection_MissingScoringConfigurationFailsClosed()
    {
        Object.DestroyImmediate(
            shooterRoot.GetComponent<
                ShipAutoTargetScoringConfiguration
            >()
        );
        shooterState.SetAutoFireEnabled(true);
        object[] arguments =
        {
            shooterRoot,
            new[] { new AutoTargetCandidate(targetRoot, true) },
            default(AutoTargetSelectionResult),
            null
        };

        bool selected = InvokePrivateStatic<bool>(
            typeof(ShipTestPanel),
            "TrySelectAutoTargetForDebug",
            arguments
        );
        AutoTargetSelectionResult result =
            (AutoTargetSelectionResult)arguments[2];

        Assert.That(selected, Is.False);
        Assert.That(result.HasAnyTarget, Is.False);
        Assert.That(shooterState.AutoFireEnabled, Is.True);
        Assert.That(shooterState.ManualTarget, Is.Null);
    }


    private FireEligibilityResult EvaluateManualTarget()
    {
        Physics.SyncTransforms();
        bool evaluated = eligibility.TryEvaluate(
            shooterState.ManualTarget,
            true,
            out FireEligibilityResult result
        );

        Assert.That(evaluated, Is.True);
        return result;
    }


    private GameObject CreateCombatShip(string name, bool includeEligibility)
    {
        GameObject root = new GameObject(name);
        createdRoots.Add(root);
        root.AddComponent<ShipDestinationController>();
        root.AddComponent<ShipCombatState>();
        ShipArtDefinition artDefinition =
            root.AddComponent<ShipArtDefinition>();
        SetArtReferences(root, artDefinition);
        root.AddComponent<ShipCombatGeometry>();
        ShipExposureReference exposure =
            root.AddComponent<ShipExposureReference>();
        SetPrivateField(exposure, "shipArtDefinition", artDefinition);

        if (includeEligibility)
        {
            root.AddComponent<ShipFireEligibility>();
        }

        return root;
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


    private static void InvokePrivateInstance(
        object target,
        string methodName
    )
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            PrivateInstance
        );
        Assert.That(method, Is.Not.Null, $"Missing method {methodName}.");
        method.Invoke(target, null);
    }


    private static T InvokePrivateStatic<T>(
        System.Type type,
        string methodName,
        params object[] arguments
    )
    {
        MethodInfo method = type.GetMethod(methodName, PrivateStatic);
        Assert.That(method, Is.Not.Null, $"Missing method {methodName}.");
        object value = method.Invoke(null, arguments);
        return value == null ? default : (T)value;
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
}
