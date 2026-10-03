using UnityEngine;
using UnityEngine.InputSystem;

// Temporary single-ship IMGUI surface; commands remain with ShipPlayerCommandInput.
[DisallowMultipleComponent]
public sealed class MovementStatusPanel : MonoBehaviour
{
    [SerializeField]
    private Rect panelRect = new Rect(434f, 12f, 400f, 445f);

    [SerializeField]
    private ShipSelectionManager selectionManager;

    [SerializeField]
    private ShipPlayerCommandInput playerInput;

    [SerializeField]
    private ShipDirectedHeadingPreviewController headingPreview;

    private MovementStatusSnapshot currentStatus;
    private string displayText = string.Empty;
    private string commandHint = string.Empty;
    private bool turnPointerCaptured;

    public MovementStatusSnapshot CurrentStatus => currentStatus;
    public string DisplayText => displayText;
    public string CommandHint => commandHint;
    public bool IsTurnPointerCaptured => turnPointerCaptured;
    public Rect PanelRect => panelRect;

    public bool ContainsScreenPoint(Vector2 screenPoint)
    {
        return isActiveAndEnabled && ContainsGuiPoint(new Vector2(
            screenPoint.x, Screen.height - screenPoint.y));
    }

    public bool ContainsGuiPoint(Vector2 guiPoint)
    {
        return isActiveAndEnabled && panelRect.Contains(guiPoint);
    }

    public bool TryDecreaseSpeed()
    {
        if (!CanUseControls() || HasActiveHold)
        {
            return false;
        }

        bool accepted = playerInput.TryDecreaseSelectedSpeedOrder();
        commandHint = accepted ? string.Empty : "Speed command unavailable";
        RefreshStatus();
        return accepted;
    }

    public bool TryIncreaseSpeed()
    {
        if (!CanUseControls() || HasActiveHold)
        {
            return false;
        }

        bool accepted = playerInput.TryIncreaseSelectedSpeedOrder();
        commandHint = accepted ? string.Empty : "Speed command unavailable";
        RefreshStatus();
        return accepted;
    }

    public bool TryStop()
    {
        if (!CanUseControls() || HasActiveHold)
        {
            return false;
        }

        playerInput.StopSelectedShips();
        commandHint = string.Empty;
        RefreshStatus();
        return true;
    }

    public bool BeginTurnHold(TurnDirection direction, double unscaledTime)
    {
        if (turnPointerCaptured || headingPreview == null
            || headingPreview.IsPreviewActive || !CanUseControls())
        {
            return false;
        }

        if (!headingPreview.BeginPreview(currentStatus.Ship, direction, unscaledTime))
        {
            return false;
        }

        turnPointerCaptured = true;
        commandHint = string.Empty;
        RefreshStatus();
        return true;
    }

    public bool ReleaseTurnHold(double unscaledTime)
    {
        if (!turnPointerCaptured)
        {
            return false;
        }

        turnPointerCaptured = false;
        bool accepted = headingPreview != null
            && headingPreview.CommitPreview(unscaledTime, out _);
        RefreshStatus();
        return accepted;
    }

    public void CancelTurnHold()
    {
        turnPointerCaptured = false;
        if (headingPreview != null)
        {
            headingPreview.CancelPreview();
        }

        RefreshStatus();
    }

    // Event-state boundary for IMGUI and focused input tests.
    public bool HandlePointerEvent(
        EventType type, int button, Vector2 guiPosition, double unscaledTime)
    {
        if (button != 0)
        {
            return false;
        }

        if (type == EventType.MouseUp && turnPointerCaptured)
        {
            ReleaseTurnHold(unscaledTime);
            return true;
        }

        if (type != EventType.MouseDown)
        {
            return false;
        }

        if (turnPointerCaptured)
        {
            return true;
        }

        if (CounterClockwiseRect.Contains(guiPosition))
        {
            return BeginTurnHold(TurnDirection.CounterClockwise, unscaledTime);
        }

        return ClockwiseRect.Contains(guiPosition)
            && BeginTurnHold(TurnDirection.Clockwise, unscaledTime);
    }

    private Rect CounterClockwiseRect => new Rect(
        panelRect.x + 10f, panelRect.y + 338f,
        (panelRect.width - 30f) / 2f, 32f);

    private Rect ClockwiseRect => new Rect(
        panelRect.x + 20f + (panelRect.width - 30f) / 2f,
        panelRect.y + 338f, (panelRect.width - 30f) / 2f, 32f);

    private bool HasActiveHold => turnPointerCaptured
        || (headingPreview != null && headingPreview.IsPreviewActive);

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (selectionManager != null)
        {
            selectionManager.SelectionMembershipChanged += OnSelectionChanged;
        }

        RefreshStatus();
    }

    private void OnDisable()
    {
        CancelTurnHold();
        if (selectionManager != null)
        {
            selectionManager.SelectionMembershipChanged -= OnSelectionChanged;
        }

        currentStatus = default;
        displayText = string.Empty;
        commandHint = string.Empty;
    }

    private void Update()
    {
        if (turnPointerCaptured)
        {
            if (headingPreview == null || !headingPreview.IsPreviewActive)
            {
                turnPointerCaptured = false;
            }
            else if (Mouse.current != null
                && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                ReleaseTurnHold(Time.unscaledTimeAsDouble);
            }
        }

        RefreshStatus();
    }

    private void OnGUI()
    {
        Event current = Event.current;
        if (current != null && HandlePointerEvent(
            current.type, current.button, current.mousePosition,
            Time.unscaledTimeAsDouble))
        {
            current.Use();
        }

        RefreshStatus();
        GUI.Box(panelRect, string.Empty);
        GUI.Label(new Rect(panelRect.x + 10f, panelRect.y + 10f,
            panelRect.width - 20f, 275f), displayText);

        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && CanUseControls()
            && !HasActiveHold;
        float third = (panelRect.width - 40f) / 3f;
        if (GUI.Button(new Rect(panelRect.x + 10f, panelRect.y + 296f,
            third, 32f), "- SPEED"))
        {
            TryDecreaseSpeed();
        }

        if (GUI.Button(new Rect(panelRect.x + 20f + third,
            panelRect.y + 296f, third, 32f), "STOP"))
        {
            TryStop();
        }

        if (GUI.Button(new Rect(panelRect.x + 30f + 2f * third,
            panelRect.y + 296f, third, 32f), "+ SPEED"))
        {
            TryIncreaseSpeed();
        }

        GUI.enabled = previousEnabled && CanUseControls();
        GUI.Box(CounterClockwiseRect, "< COUNTER-CLOCKWISE");
        GUI.Box(ClockwiseRect, "CLOCKWISE >");
        GUI.enabled = previousEnabled;
        GUI.Label(new Rect(panelRect.x + 10f, panelRect.y + 382f,
            panelRect.width - 20f, 50f), commandHint);
    }

    private bool CanUseControls()
    {
        RefreshStatus();
        return isActiveAndEnabled && currentStatus.HasControllableShip
            && selectionManager != null
            && selectionManager.SelectedCount == 1
            && playerInput != null && playerInput.isActiveAndEnabled;
    }

    private void OnSelectionChanged()
    {
        CancelTurnHold();
        commandHint = string.Empty;
        RefreshStatus();
    }

    private void ResolveReferences()
    {
        if (selectionManager == null)
        {
            selectionManager = GetComponent<ShipSelectionManager>();
        }

        if (playerInput == null)
        {
            playerInput = GetComponent<ShipPlayerCommandInput>();
        }

        if (headingPreview == null)
        {
            headingPreview = GetComponent<ShipDirectedHeadingPreviewController>();
        }
    }

    private void RefreshStatus()
    {
        currentStatus = MovementStatusReadModel.Capture(
            selectionManager, headingPreview);
        displayText = MovementStatusFormatter.Format(currentStatus);
    }
}
