using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ShipFireEligibilityDebugTests
{
    private const BindingFlags PrivateStatic =
        BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private GameObject shooterRoot;
    private GameObject targetRoot;
    private ShipCombatState shooterState;
    private Transform shooterCenter;
    private Transform targetCenter;


    [SetUp]
    public void SetUp()
    {
        shooterRoot = CreateShip(
            "Explicit Debug Shooter",
            true,
            out shooterCenter
        );
        targetRoot = CreateShip(
            "Explicit Debug Target",
            false,
            out targetCenter
        );
        targetRoot.transform.position = Vector3.right * 50f;
        shooterState = shooterRoot.GetComponent<ShipCombatState>();
    }


    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(targetRoot);
        Object.DestroyImmediate(shooterRoot);
    }


    [Test]
    public void DebugEvaluation_UsesExplicitShooterAndTarget()
    {
        bool evaluated = TryEvaluateForDebug(
            shooterRoot,
            targetRoot,
            true,
            out FireEligibilityResult result,
            out string message
        );

        Assert.That(evaluated, Is.True);
        Assert.That(message, Is.EqualTo(string.Empty));
        Assert.That(result.TargetLegal, Is.True);
        Assert.That(result.Side, Is.EqualTo(CombatSide.Starboard));
        Assert.That(result.CanFire, Is.True);
        Assert.That(result.HasObstructionPath, Is.True);
        Assert.That(
            result.ObstructionOriginWorld,
            Is.EqualTo(shooterCenter.position)
        );
        Assert.That(
            result.ObstructionDestinationWorld,
            Is.EqualTo(targetCenter.position)
        );
    }


    [Test]
    public void DebugEvaluation_MissingShooterFailsSafely()
    {
        bool evaluated = TryEvaluateForDebug(
            null,
            targetRoot,
            true,
            out FireEligibilityResult result,
            out string message
        );

        Assert.That(evaluated, Is.False);
        Assert.That(result, Is.EqualTo(default(FireEligibilityResult)));
        Assert.That(message, Does.Contain("Shooter"));
    }


    [Test]
    public void DebugEvaluation_MissingTargetFailsSafely()
    {
        bool evaluated = TryEvaluateForDebug(
            shooterRoot,
            null,
            true,
            out FireEligibilityResult result,
            out string message
        );

        Assert.That(evaluated, Is.False);
        Assert.That(result, Is.EqualTo(default(FireEligibilityResult)));
        Assert.That(message, Does.Contain("Eligibility Target"));
    }


    [Test]
    public void DebugEvaluation_MissingEvaluatorFailsSafely()
    {
        GameObject missingEvaluator = new GameObject("Missing Evaluator");

        try
        {
            bool evaluated = TryEvaluateForDebug(
                missingEvaluator,
                targetRoot,
                true,
                out FireEligibilityResult result,
                out string message
            );

            Assert.That(evaluated, Is.False);
            Assert.That(result, Is.EqualTo(default(FireEligibilityResult)));
            Assert.That(message, Does.Contain("ShipFireEligibility"));
        }
        finally
        {
            Object.DestroyImmediate(missingEvaluator);
        }
    }


    [Test]
    public void DebugEvaluation_DoesNotWriteRootOrCombatState()
    {
        shooterState.TryCommitBroadsideFire(CombatSide.Port);
        Vector3 shooterPosition = shooterRoot.transform.position;
        Quaternion shooterRotation = shooterRoot.transform.rotation;
        Vector3 targetPosition = targetRoot.transform.position;
        Quaternion targetRotation = targetRoot.transform.rotation;
        BroadsideReloadState portState = shooterState.PortBroadsideState;
        float portRemaining = shooterState.PortReloadRemainingSeconds;
        BroadsideReloadState starboardState =
            shooterState.StarboardBroadsideState;
        float starboardRemaining =
            shooterState.StarboardReloadRemainingSeconds;

        bool evaluated = TryEvaluateForDebug(
            shooterRoot,
            targetRoot,
            true,
            out FireEligibilityResult _,
            out string _
        );

        Assert.That(evaluated, Is.True);
        Assert.That(
            shooterRoot.transform.position,
            Is.EqualTo(shooterPosition)
        );
        Assert.That(
            shooterRoot.transform.rotation,
            Is.EqualTo(shooterRotation)
        );
        Assert.That(targetRoot.transform.position, Is.EqualTo(targetPosition));
        Assert.That(targetRoot.transform.rotation, Is.EqualTo(targetRotation));
        Assert.That(shooterState.PortBroadsideState, Is.EqualTo(portState));
        Assert.That(
            shooterState.PortReloadRemainingSeconds,
            Is.EqualTo(portRemaining)
        );
        Assert.That(
            shooterState.StarboardBroadsideState,
            Is.EqualTo(starboardState)
        );
        Assert.That(
            shooterState.StarboardReloadRemainingSeconds,
            Is.EqualTo(starboardRemaining)
        );
    }


    [Test]
    public void DiagnosticSummary_UsesAuthoritativeResultAndReasons()
    {
        ShipFireEligibility evaluator =
            shooterRoot.GetComponent<ShipFireEligibility>();
        SetPrivateField(evaluator, "effectiveRangeMeters", 50f);
        SetPrivateField(evaluator, "maximumRangeMeters", 100f);
        targetRoot.transform.position = Vector3.right * 150f;
        shooterState.TryCommitBroadsideFire(CombatSide.Starboard);
        TryEvaluateForDebug(
            shooterRoot,
            targetRoot,
            false,
            out FireEligibilityResult result,
            out string _
        );

        string summary = InvokePrivateStatic<string>(
            "BuildEligibilitySummary",
            targetRoot,
            result
        );

        Assert.That(summary, Does.Contain("CAN FIRE: NO"));
        Assert.That(summary, Does.Contain("TARGET_ILLEGAL"));
        Assert.That(summary, Does.Contain("BEYOND_MAXIMUM_RANGE"));
        Assert.That(summary, Does.Contain("RELOADING"));
    }


    [Test]
    public void DebugSource_ConsumesEligibilityWithoutDuplicatingRules()
    {
        string sourcePath = Path.Combine(
            Application.dataPath,
            "Assets/Game/Editor/Debug/ShipTestPanel.cs"
        );
        string source = File.ReadAllText(sourcePath);

        Assert.That(source, Does.Contain("evaluator.TryEvaluate("));
        Assert.That(source, Does.Contain("result.FailureReasons"));
        Assert.That(source, Does.Contain("result.ObstructionOriginWorld"));
        Assert.That(source, Does.Contain("Physics.SyncTransforms()"));
        Assert.That(source, Does.Not.Contain("Physics.Raycast"));
        Assert.That(source, Does.Not.Contain("Physics.Linecast"));
        Assert.That(source, Does.Not.Contain("Physics.SphereCast"));
        Assert.That(source, Does.Not.Contain("Physics.Overlap"));
        Assert.That(source, Does.Not.Contain("Raycast"));
        Assert.That(source, Does.Not.Contain("Vector3.Distance"));
        Assert.That(source, Does.Not.Contain(".magnitude"));
        Assert.That(source, Does.Not.Contain("Mathf.Atan2"));
        Assert.That(source, Does.Not.Contain("SignedAngle"));
        Assert.That(source, Does.Not.Contain("InverseTransformDirection"));
        Assert.That(source, Does.Not.Contain("IsInBroadsideArc"));
    }


    [Test]
    public void DebugTool_RemainsInEditorAssembly()
    {
        Assert.That(
            typeof(ShipTestPanel).Assembly.GetName().Name,
            Is.EqualTo("AgeOfSailRTS.Editor")
        );
        Assert.That(
            typeof(ShipFireEligibility).Assembly.GetName().Name,
            Is.EqualTo("AgeOfSailRTS.Runtime")
        );
        Assert.That(
            typeof(ShipTestPanel).Assembly,
            Is.Not.SameAs(typeof(ShipFireEligibility).Assembly)
        );
    }


    private static bool TryEvaluateForDebug(
        GameObject shooter,
        GameObject target,
        bool relationshipAllowsFire,
        out FireEligibilityResult result,
        out string message
    )
    {
        object[] arguments =
        {
            shooter,
            target,
            relationshipAllowsFire,
            default(FireEligibilityResult),
            null
        };
        bool evaluated = InvokePrivateStatic<bool>(
            "TryEvaluateForDebug",
            arguments
        );
        result = (FireEligibilityResult)arguments[3];
        message = (string)arguments[4];
        return evaluated;
    }


    private static GameObject CreateShip(
        string name,
        bool includeEvaluator,
        out Transform centerReference
    )
    {
        GameObject root = new GameObject(name);
        CombatLifecycleTestUtility.AddOperationalIntegrity(root);
        root.AddComponent<ShipCombatState>();
        root.AddComponent<ShipCombatGeometry>();
        ShipArtDefinition artDefinition =
            root.AddComponent<ShipArtDefinition>();
        GameObject center = new GameObject("Center Reference");
        center.transform.SetParent(root.transform, false);
        centerReference = center.transform;
        SetPrivateField(
            artDefinition,
            "centerReference",
            centerReference
        );

        if (includeEvaluator)
        {
            root.AddComponent<ShipFireEligibility>();
        }

        return root;
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
