using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ShipTestPanelTests
{
    private const BindingFlags PrivateStatic =
        BindingFlags.Static | BindingFlags.NonPublic;

    private GameObject shipRoot;
    private GameObject nonCombatRoot;
    private ShipCombatState combatState;


    [SetUp]
    public void SetUp()
    {
        shipRoot = new GameObject("Combat Ship Root");
        nonCombatRoot = new GameObject("Movement-Only Ship Root");
        combatState = shipRoot.AddComponent<ShipCombatState>();
    }


    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(nonCombatRoot);
        UnityEngine.Object.DestroyImmediate(shipRoot);
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
    public void BlindFireAction_DelegatesToCombatCommand()
    {
        InvokePrivateStatic<object>("ToggleBlindFire", combatState);

        Assert.That(combatState.BlindFireEnabled, Is.True);

        InvokePrivateStatic<object>("ToggleBlindFire", combatState);

        Assert.That(combatState.BlindFireEnabled, Is.False);
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
        InvokePrivateStatic<object>("ToggleBlindFire", combatState);
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
    public void PanelSource_HasNoStateBypassOrProjectileDependency()
    {
        string sourcePath = Path.Combine(
            Application.dataPath,
            "Assets/Game/Editor/Debug/ShipTestPanel.cs"
        );
        string source = File.ReadAllText(sourcePath);

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
