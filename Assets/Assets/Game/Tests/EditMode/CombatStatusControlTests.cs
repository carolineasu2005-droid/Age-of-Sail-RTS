using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class CombatStatusControlTests
{
    private readonly List<Object> created = new List<Object>();
    private ShipSelectionManager selection;
    private ShipPlayerCommandInput input;
    private CombatStatusPanel panel;
    private GameObject shooter;
    private GameObject target;

    [SetUp]
    public void SetUp()
    {
        GameObject host = CreateObject("Control Host");
        selection = host.AddComponent<ShipSelectionManager>();
        ShipCommandDispatcher dispatcher =
            host.AddComponent<ShipCommandDispatcher>();
        input = host.AddComponent<ShipPlayerCommandInput>();
        SetPrivateField(dispatcher, "selectionManager", selection);
        SetPrivateField(input, "selectionManager", selection);
        SetPrivateField(input, "commandDispatcher", dispatcher);
        BindSelectionChange(input, "HandleSelectionMembershipChanged");

        panel = host.AddComponent<CombatStatusPanel>();
        SetPrivateField(panel, "selectionManager", selection);
        SetPrivateField(panel, "playerInput", input);
        BindSelectionChange(panel, "OnSelectionChanged");

        shooter = CreateCombatShip("Shooter");
        target = CreateCombatShip("Target");
        target.transform.position = Vector3.right * 50f;
        selection.SelectSingle(shooter.GetComponent<ShipDestinationController>());
        RefreshPanel();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (Object item in created)
        {
            if (item != null)
            {
                Object.DestroyImmediate(item);
            }
        }

        created.Clear();
    }

    [Test]
    public void AutoFire_ButtonUsesSelectedStateAndReadback()
    {
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        Assert.That(panel.CurrentStatus.AutoFireEnabled, Is.False);

        Assert.That(panel.TryToggleAutoFire(), Is.True);
        Assert.That(state.AutoFireEnabled, Is.True);
        Assert.That(panel.CurrentStatus.AutoFireEnabled, Is.True);
        Assert.That(panel.DisplayText, Does.Contain("Auto Fire: ON"));

        Assert.That(panel.TryToggleAutoFire(), Is.True);
        Assert.That(state.AutoFireEnabled, Is.False);
        Assert.That(panel.CurrentStatus.AutoFireEnabled, Is.False);
        Assert.That(state.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready));
        Assert.That(state.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready));

        selection.ClearSelection();
        Assert.That(panel.TryToggleAutoFire(), Is.False);
        Assert.That(panel.TryClearManualTarget(), Is.False);
        Assert.That(panel.TryBeginManualTarget(), Is.False);
        Assert.That(panel.TryBeginBlindFire(), Is.False);
        Assert.That(state.AutoFireEnabled, Is.False);
    }

    [Test]
    public void ManualTarget_ArmedClickAssignsThroughExistingCommandAndClear()
    {
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        Assert.That(panel.TryToggleAutoFire(), Is.True);
        Assert.That(panel.TryBeginManualTarget(), Is.True);
        Assert.That(input.ManualTargetArmed, Is.True);
        Assert.That(panel.InteractionHint, Does.Contain("Right-click a ship"));

        GameObject clickedChild = CreateObject("Clicked Child");
        clickedChild.transform.SetParent(target.transform);
        Assert.That(input.TrySubmitArmedManualTarget(clickedChild), Is.True);
        Assert.That(input.ManualTargetArmed, Is.False);
        Assert.That(state.ManualTarget, Is.SameAs(target));
        Assert.That(state.AutoFireEnabled, Is.False);
        RefreshPanel();
        Assert.That(panel.CurrentStatus.ManualTarget, Is.SameAs(target));

        Assert.That(panel.TryClearManualTarget(), Is.True);
        Assert.That(state.ManualTarget, Is.Null);
        Assert.That(state.AutoFireEnabled, Is.False);
        Assert.That(panel.DisplayText, Does.Contain("Manual Target: None"));

        Assert.That(panel.TryBeginManualTarget(), Is.True);
        Assert.That(input.TrySubmitArmedManualTarget(CreateObject("Water")),
            Is.False);
        Assert.That(input.ManualTargetArmed, Is.False);
        Assert.That(state.ManualTarget, Is.Null);

        Assert.That(panel.TryBeginManualTarget(), Is.True);
        Assert.That(input.TrySubmitArmedManualTarget(target), Is.True);
        Assert.That(panel.TryToggleAutoFire(), Is.True);
        Assert.That(state.AutoFireEnabled, Is.True);
        Assert.That(state.ManualTarget, Is.Null);
    }

    [Test]
    public void ManualTarget_ArmedScreenClickUsesExistingRaycastPicker()
    {
        GameObject clickedChild = GameObject.CreatePrimitive(PrimitiveType.Cube);
        created.Add(clickedChild);
        clickedChild.transform.SetParent(target.transform);
        clickedChild.transform.localPosition = Vector3.zero;
        clickedChild.transform.localScale = Vector3.one * 5f;
        GameObject cameraRoot = CreateObject("Pick Camera");
        Camera camera = cameraRoot.AddComponent<Camera>();
        cameraRoot.transform.position = target.transform.position
            + new Vector3(0f, 20f, -30f);
        cameraRoot.transform.LookAt(target.transform.position);
        Vector3 screenPoint = camera.WorldToScreenPoint(
            target.transform.position);
        SetPrivateField(input, "commandCamera", camera);
        SetPrivateField(input, "rightMouseDownScreenPosition",
            new Vector2(screenPoint.x, screenPoint.y));
        Physics.SyncTransforms();

        Assert.That(panel.TryBeginManualTarget(), Is.True);
        InvokePrivate(input, "CommitManualTargetClick");

        Assert.That(shooter.GetComponent<ShipCombatState>().ManualTarget,
            Is.SameAs(target));
        Assert.That(input.ManualTargetArmed, Is.False);
    }

    [Test]
    public void SelectionChangeAndRepeatedActionCancelPendingTargetInput()
    {
        GameObject second = CreateCombatShip("Second Shooter");
        ShipCombatState secondState = second.GetComponent<ShipCombatState>();
        Assert.That(panel.TryBeginManualTarget(), Is.True);
        Assert.That(panel.TryBeginManualTarget(), Is.True);
        Assert.That(input.ManualTargetArmed, Is.False);

        Assert.That(panel.TryBeginManualTarget(), Is.True);
        selection.SelectSingle(second.GetComponent<ShipDestinationController>());
        Assert.That(input.ManualTargetArmed, Is.False);
        Assert.That(input.TrySubmitArmedManualTarget(target), Is.False);
        Assert.That(secondState.ManualTarget, Is.Null);
        Assert.That(shooter.GetComponent<ShipCombatState>().ManualTarget,
            Is.Null);
    }

    [Test]
    public void PanelClickRegionIsExcludedFromWorldInput()
    {
        MovementStatusPanel movementPanel =
            input.gameObject.AddComponent<MovementStatusPanel>();
        Assert.That(panel.ContainsScreenPoint(
            new Vector2(30f, Screen.height - 30f)), Is.True);
        Assert.That(input.IsPointerOverStatusPanel(
            new Vector2(30f, Screen.height - 30f)), Is.True);
        Assert.That(input.IsPointerOverStatusPanel(
            new Vector2(450f, Screen.height - 30f)), Is.True);
        Assert.That(movementPanel.ContainsScreenPoint(
            new Vector2(450f, Screen.height - 30f)), Is.True);
        Assert.That(panel.ContainsScreenPoint(
            new Vector2(500f, Screen.height - 30f)), Is.False);

        selection.ClearSelection();
        Assert.That(panel.ContainsScreenPoint(
            new Vector2(30f, Screen.height - 30f)), Is.False);
        Assert.That(input.IsPointerOverStatusPanel(
            new Vector2(30f, Screen.height - 30f)), Is.False);
        Assert.That(input.IsPointerOverStatusPanel(
            new Vector2(450f, Screen.height - 30f)), Is.True);
    }

    [Test]
    public void BlindFire_ArmedWorldPointStaysFrozenAndTargetless()
    {
        AddIntegrity(shooter);
        shooter.AddComponent<ShipBroadsideFireExecutor>();
        Assert.That(panel.TryBeginBlindFire(), Is.True);
        Assert.That(input.BlindFireArmed, Is.True);
        Assert.That(panel.InteractionHint, Does.Contain("world point"));

        Vector3 point = shooter.transform.position + Vector3.right * 50f;
        input.TrySubmitArmedBlindFireAtWorldPoint(point,
            out BlindFirePlayerCommandResult result);

        Assert.That(result.Attempted, Is.True);
        Assert.That(result.ShooterShipRoot, Is.SameAs(shooter));
        Assert.That(result.WorldAimPoint, Is.EqualTo(point));
        Assert.That(result.Eligibility.Aim.WorldAimPoint,
            Is.EqualTo(point));
        Assert.That(input.BlindFireArmed, Is.False);
        Assert.That(shooter.GetComponent<ShipCombatState>().ManualTarget,
            Is.Null);
        RefreshPanel();
        Assert.That(panel.CurrentStatus.LastBlindFirePointWorld,
            Is.EqualTo(point));
        Assert.That(panel.InteractionHint,
            Does.Contain("Arc Legal").And.Contain("Range 50 / 500 m")
                .And.Contain("Result:"));

        Assert.That(panel.TryBeginBlindFire(), Is.True);
        Assert.That(panel.TryBeginBlindFire(), Is.True);
        Assert.That(input.BlindFireArmed, Is.False);
    }

    [Test]
    public void SelectionChangePreventsStaleBlindFireFromUsingAnotherShip()
    {
        AddIntegrity(shooter);
        shooter.AddComponent<ShipBroadsideFireExecutor>();
        GameObject second = CreateCombatShip("Second Shooter");
        AddIntegrity(second);
        second.AddComponent<ShipBroadsideFireExecutor>();
        Assert.That(panel.TryBeginBlindFire(), Is.True);
        SetPrivateField(input, "rightPlacementGestureActive", true);
        SetPrivateField(input, "blindFireClickPending", true);
        SetPrivateField(input, "rightMouseDownWorldPosition",
            Vector3.right * 50f);

        selection.SelectSingle(second.GetComponent<ShipDestinationController>());
        Assert.That(input.BlindFireArmed, Is.False);
        InvokePrivate(input, "CompleteDestinationGesture");
        Assert.That(input.LastBlindFireCommandResult.Attempted, Is.False);
        Assert.That(input.TrySubmitArmedBlindFireAtWorldPoint(
            Vector3.right * 50f, out BlindFirePlayerCommandResult result),
            Is.False);
        Assert.That(result.Attempted, Is.False);
        Assert.That(second.GetComponent<ShipCombatState>().ManualTarget,
            Is.Null);
        Assert.That(second.GetComponent<ShipCombatState>()
            .StarboardBroadsideState, Is.EqualTo(BroadsideReloadState.Ready));
    }

    [Test]
    public void SwitchingInputActionsDoesNotCreateAnotherCombatMode()
    {
        AddIntegrity(shooter);
        shooter.AddComponent<ShipBroadsideFireExecutor>();
        Assert.That(panel.TryBeginManualTarget(), Is.True);
        Assert.That(panel.TryBeginBlindFire(), Is.True);
        Assert.That(input.ManualTargetArmed, Is.False);
        Assert.That(input.BlindFireArmed, Is.True);
        Assert.That(panel.CurrentStatus.BlindFireArmed, Is.True);

        Assert.That(panel.TryBeginManualTarget(), Is.True);
        Assert.That(input.BlindFireArmed, Is.False);
        Assert.That(panel.CurrentStatus.BlindFireArmed, Is.False);
        Assert.That(panel.TryBeginBlindFire(), Is.True);

        Assert.That(panel.TryToggleAutoFire(), Is.True);
        Assert.That(input.BlindFireArmed, Is.False);
        Assert.That(shooter.GetComponent<ShipCombatState>().AutoFireEnabled,
            Is.True);
        Assert.That(shooter.GetComponent<ShipCombatState>().ManualTarget,
            Is.Null);
    }

    [Test]
    public void ControlSourceDoesNotOwnFireOrObstructionRules()
    {
        string panelSource = File.ReadAllText(Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/UI/CombatStatusPanel.cs"));
        string inputSource = File.ReadAllText(Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Sailing/ShipPlayerCommandInput.cs"));
        Assert.That(panelSource, Does.Not.Contain("ShipBroadsideFireExecutor"));
        Assert.That(panelSource, Does.Not.Contain("TryCommitBroadsideFire"));
        Assert.That(panelSource, Does.Not.Contain("CombatFireObstructionQuery"));
        Assert.That(panelSource, Does.Not.Contain("CombatAITargetAcquisition"));
        Assert.That(panelSource, Does.Not.Contain("ShipAutoTargetSelector"));
        Assert.That(inputSource, Does.Not.Contain("CombatFireObstructionQuery"));
        Assert.That(inputSource, Does.Not.Contain("CombatAITargetAcquisition"));
        Assert.That(inputSource, Does.Not.Contain("TryCommitBroadsideFire"));
    }

    private GameObject CreateCombatShip(string name)
    {
        GameObject ship = CreateObject(name);
        ship.AddComponent<ShipDestinationController>();
        ship.AddComponent<ShipCombatState>();
        ship.AddComponent<ShipFireEligibility>();
        return ship;
    }

    private void AddIntegrity(GameObject ship)
    {
        ShipIntegrityProfile profile =
            ScriptableObject.CreateInstance<ShipIntegrityProfile>();
        created.Add(profile);
        ShipIntegrity integrity = ship.AddComponent<ShipIntegrity>();
        SetPrivateField(integrity, "integrityProfile", profile);
        Assert.That(integrity.TryInitialize(), Is.True);
    }

    private GameObject CreateObject(string name)
    {
        GameObject instance = new GameObject(name);
        created.Add(instance);
        return instance;
    }

    private static void SetPrivateField(object owner, string name, object value)
    {
        FieldInfo field = owner.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(owner, value);
    }

    private void BindSelectionChange(object listener, string methodName)
    {
        MethodInfo method = listener.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        System.Action handler = (System.Action)System.Delegate.CreateDelegate(
            typeof(System.Action), listener, method);
        selection.SelectionMembershipChanged -= handler;
        selection.SelectionMembershipChanged += handler;
    }

    private void RefreshPanel()
    {
        InvokePrivate(panel, "RefreshStatus", true);
    }

    private static void InvokePrivate(object owner, string name,
        params object[] arguments)
    {
        MethodInfo method = owner.GetType().GetMethod(name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(owner, arguments);
    }
}
