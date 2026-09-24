using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ShipCombatModeArbitrationTests
{
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly List<GameObject> createdObjects = new();

    private GameObject shooterRoot;
    private GameObject targetRoot;
    private GameObject secondTargetRoot;
    private ShipCombatState combatState;
    private ShipFireEligibility eligibility;


    [SetUp]
    public void SetUp()
    {
        shooterRoot = CreateCombatShip("Shooter Root", true);
        targetRoot = CreateCombatShip("Target Root", true);
        secondTargetRoot = CreateCombatShip("Second Target Root", true);
        combatState = shooterRoot.GetComponent<ShipCombatState>();
        eligibility = shooterRoot.AddComponent<ShipFireEligibility>();
        SetPrivateField(eligibility, "effectiveRangeMeters", 100f);
        SetPrivateField(eligibility, "maximumRangeMeters", 200f);
    }


    [TearDown]
    public void TearDown()
    {
        for (int index = createdObjects.Count - 1; index >= 0; index--)
        {
            Object.DestroyImmediate(createdObjects[index]);
        }

        createdObjects.Clear();
    }


    [Test]
    public void AssignManualTarget_WhileAutoFireOff_AcceptsManualMode()
    {
        bool assigned = combatState.AssignManualTarget(targetRoot);

        Assert.That(assigned, Is.True);
        Assert.That(combatState.ManualTarget, Is.SameAs(targetRoot));
        Assert.That(combatState.AutoFireEnabled, Is.False);
    }


    [Test]
    public void AssignManualTarget_WhileAutoFireOn_DisablesAutoImmediately()
    {
        combatState.SetAutoFireEnabled(true);

        bool assigned = combatState.AssignManualTarget(targetRoot);

        Assert.That(assigned, Is.True);
        Assert.That(combatState.ManualTarget, Is.SameAs(targetRoot));
        Assert.That(combatState.AutoFireEnabled, Is.False);
    }


    [Test]
    public void EnableAutoFire_ClearsManualTarget()
    {
        combatState.AssignManualTarget(targetRoot);

        combatState.SetAutoFireEnabled(true);

        Assert.That(combatState.AutoFireEnabled, Is.True);
        Assert.That(combatState.ManualTarget, Is.Null);
    }


    [Test]
    public void ClearManualTarget_DoesNotEnableAutoFire()
    {
        combatState.AssignManualTarget(targetRoot);

        combatState.ClearManualTarget();

        Assert.That(combatState.ManualTarget, Is.Null);
        Assert.That(combatState.AutoFireEnabled, Is.False);
    }


    [Test]
    public void RepeatedManualAssignments_ReplaceTargetDeterministically()
    {
        Assert.That(
            combatState.AssignManualTarget(targetRoot),
            Is.True
        );
        Assert.That(
            combatState.AssignManualTarget(secondTargetRoot),
            Is.True
        );
        Assert.That(
            combatState.AssignManualTarget(secondTargetRoot),
            Is.True
        );

        Assert.That(
            combatState.ManualTarget,
            Is.SameAs(secondTargetRoot)
        );
        Assert.That(combatState.AutoFireEnabled, Is.False);
    }


    [Test]
    public void InvalidManualTargets_AreRejectedWithoutChangingMode()
    {
        GameObject nonCombatRoot = CreateObject("Non-Combat Root");
        combatState.AssignManualTarget(targetRoot);

        Assert.That(combatState.AssignManualTarget(null), Is.False);
        Assert.That(combatState.AssignManualTarget(shooterRoot), Is.False);
        Assert.That(
            combatState.AssignManualTarget(nonCombatRoot),
            Is.False
        );
        Assert.That(combatState.ManualTarget, Is.SameAs(targetRoot));
        Assert.That(combatState.AutoFireEnabled, Is.False);

        combatState.ClearManualTarget();
        combatState.SetAutoFireEnabled(true);

        Assert.That(
            combatState.AssignManualTarget(nonCombatRoot),
            Is.False
        );
        Assert.That(combatState.ManualTarget, Is.Null);
        Assert.That(combatState.AutoFireEnabled, Is.True);
    }


    [Test]
    public void ManualTarget_RemainsAssignedWhenEligibilityIsOutOfArc()
    {
        combatState.AssignManualTarget(targetRoot);
        targetRoot.transform.position = Vector3.forward * 50f;

        FireEligibilityResult result = Evaluate(targetRoot);

        Assert.That(result.InBroadsideArc, Is.False);
        Assert.That(result.CanFire, Is.False);
        Assert.That(combatState.ManualTarget, Is.SameAs(targetRoot));
        Assert.That(combatState.AutoFireEnabled, Is.False);
    }


    [Test]
    public void ManualTarget_RemainsAssignedWhenEligibilityIsOutOfRange()
    {
        combatState.AssignManualTarget(targetRoot);
        targetRoot.transform.position = Vector3.right * 201f;

        FireEligibilityResult result = Evaluate(targetRoot);

        Assert.That(result.WithinMaximumRange, Is.False);
        Assert.That(result.CanFire, Is.False);
        Assert.That(combatState.ManualTarget, Is.SameAs(targetRoot));
        Assert.That(combatState.AutoFireEnabled, Is.False);
    }


    [Test]
    public void ManualTarget_RemainsAssignedWhenBroadsideIsReloading()
    {
        combatState.AssignManualTarget(targetRoot);
        targetRoot.transform.position = Vector3.right * 50f;
        combatState.TryCommitBroadsideFire(CombatSide.Starboard);

        FireEligibilityResult result = Evaluate(targetRoot);

        Assert.That(result.ReloadReady, Is.False);
        Assert.That(result.CanFire, Is.False);
        Assert.That(combatState.ManualTarget, Is.SameAs(targetRoot));
        Assert.That(combatState.AutoFireEnabled, Is.False);
    }


    [Test]
    public void ManualTarget_RemainsAssignedWhenLineOfFireIsBlocked()
    {
        combatState.AssignManualTarget(targetRoot);
        targetRoot.transform.position = Vector3.right * 50f;
        GameObject blocker = CreateObject("Blocking Combat Geometry");
        blocker.layer = 8;
        blocker.transform.position = Vector3.right * 25f;
        BoxCollider collider = blocker.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(4f, 4f, 4f);

        FireEligibilityResult result = Evaluate(targetRoot);

        Assert.That(result.Blocked, Is.True);
        Assert.That(result.CanFire, Is.False);
        Assert.That(combatState.ManualTarget, Is.SameAs(targetRoot));
        Assert.That(combatState.AutoFireEnabled, Is.False);
    }


    [Test]
    public void ModeCommands_DoNotChangeRootPose()
    {
        shooterRoot.transform.SetPositionAndRotation(
            new Vector3(14f, 2f, -9f),
            Quaternion.Euler(0f, 117f, 0f)
        );
        shooterRoot.transform.localScale = new Vector3(1.1f, 0.9f, 1.2f);
        Vector3 position = shooterRoot.transform.position;
        Quaternion rotation = shooterRoot.transform.rotation;
        Vector3 scale = shooterRoot.transform.localScale;

        combatState.SetAutoFireEnabled(true);
        combatState.AssignManualTarget(targetRoot);
        combatState.ClearManualTarget();
        combatState.SetAutoFireEnabled(false);

        Assert.That(shooterRoot.transform.position, Is.EqualTo(position));
        Assert.That(shooterRoot.transform.rotation, Is.EqualTo(rotation));
        Assert.That(shooterRoot.transform.localScale, Is.EqualTo(scale));
    }


    [Test]
    public void ModeCommands_HaveNoMovementCommandDependency()
    {
        string source = ReadModeCommandSource();

        Assert.That(source, Does.Not.Contain("ShipDestinationController"));
        Assert.That(source, Does.Not.Contain("ShipManeuverPlanner"));
        Assert.That(source, Does.Not.Contain("FormationCommandController"));
        Assert.That(source, Does.Not.Contain("ShipTurning"));
        Assert.That(source, Does.Not.Contain("SetDestination"));
        Assert.That(source, Does.Not.Contain("transform."));
    }


    [Test]
    public void ModeCommands_HaveNoProjectileOrFiringDependency()
    {
        string source = ReadModeCommandSource();

        Assert.That(source, Does.Not.Contain("Projectile"));
        Assert.That(source, Does.Not.Contain("ShotSample"));
        Assert.That(source, Does.Not.Contain("ShipFireEligibility"));
        Assert.That(source, Does.Not.Contain("TryEvaluate"));
        Assert.That(source, Does.Not.Contain("TryCommitBroadsideFire"));
    }


    private FireEligibilityResult Evaluate(GameObject target)
    {
        Physics.SyncTransforms();
        bool evaluated = eligibility.TryEvaluate(
            target,
            true,
            out FireEligibilityResult result
        );

        Assert.That(evaluated, Is.True);
        return result;
    }


    private GameObject CreateCombatShip(
        string name,
        bool includeCombatGeometry
    )
    {
        GameObject root = CreateObject(name);
        root.AddComponent<ShipCombatState>();
        ShipArtDefinition artDefinition =
            root.AddComponent<ShipArtDefinition>();
        GameObject center = new GameObject("Center Reference");
        center.transform.SetParent(root.transform, false);
        SetPrivateField(
            artDefinition,
            "centerReference",
            center.transform
        );

        if (includeCombatGeometry)
        {
            root.AddComponent<ShipCombatGeometry>();
        }

        return root;
    }


    private GameObject CreateObject(string name)
    {
        GameObject created = new GameObject(name);
        createdObjects.Add(created);
        return created;
    }


    private static string ReadModeCommandSource()
    {
        string sourcePath = Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Combat/ShipCombatState.cs"
        );
        string source = File.ReadAllText(sourcePath);
        int start = source.IndexOf("public void SetAutoFireEnabled");
        int end = source.IndexOf("public bool TryCommitBroadsideFire");

        Assert.That(start, Is.GreaterThanOrEqualTo(0));
        Assert.That(end, Is.GreaterThan(start));
        return source.Substring(start, end - start);
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
