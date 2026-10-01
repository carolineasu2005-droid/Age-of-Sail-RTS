using UnityEngine;

// Temporary IMGUI status and player-command surface for one selected ship.
[DisallowMultipleComponent]
public sealed class CombatStatusPanel : MonoBehaviour
{
    private const float ManualEligibilityRefreshSeconds = 1f;
    private static readonly Rect PanelRect = new Rect(12f, 12f, 410f, 450f);

    [SerializeField] private ShipSelectionManager selectionManager;
    [SerializeField] private ShipPlayerCommandInput playerInput;

    private GameObject lastShipRoot;
    private GameObject lastFireTarget;
    private float nextManualEligibilityTime;
    private bool hasManualEligibility;
    private FireEligibilityResult manualEligibility;
    private CombatStatusSnapshot currentStatus;
    private string displayText = string.Empty;

    public CombatStatusSnapshot CurrentStatus => currentStatus;
    public string DisplayText => displayText;
    public string InteractionHint => playerInput != null
        ? playerInput.ManualTargetArmed
            ? "Right-click a ship (button again cancels)"
            : playerInput.BlindFireArmed
                ? "Right-click world point (button again cancels)"
                : FormatLastBlindFireCheck()
        : string.Empty;

    private string FormatLastBlindFireCheck()
    {
        BlindFirePlayerCommandResult last =
            playerInput.LastBlindFireCommandResult;
        if (!currentStatus.HasSelection
            || !last.Attempted
            || last.ShooterShipRoot != currentStatus.ShipRoot)
        {
            return string.Empty;
        }

        if (!last.EligibilityAvailable)
        {
            return "Last Blind Check: Eligibility unavailable";
        }

        BlindFireEligibilityResult eligibility = last.Eligibility;
        string side = eligibility.Side.HasValue
            ? eligibility.Side.Value.ToString()
            : "No Side";
        string arc = eligibility.InBroadsideArc ? "Legal" : "Wrong Arc";
        string range = eligibility.Aim.AimPointDistanceMeters.HasValue
            && currentStatus.MaximumRangeMeters.HasValue
            ? $"{eligibility.Aim.AimPointDistanceMeters.Value:0} / "
                + $"{currentStatus.MaximumRangeMeters.Value:0} m"
            : "N/A";
        string outcome = last.Accepted
            ? "Accepted"
            : eligibility.FailureReasons != BlindFireEligibilityFailure.None
                ? eligibility.FailureReasons.ToString()
                : last.FailureReasons.ToString();
        return $"Last Blind: {side} Arc {arc}, Range {range}"
            + $"\nResult: {outcome}";
    }

    public bool ContainsScreenPoint(Vector2 screenPoint)
    {
        return isActiveAndEnabled && currentStatus.HasSelection
            && PanelRect.Contains(new Vector2(
                screenPoint.x, Screen.height - screenPoint.y));
    }

    public bool TryToggleAutoFire()
    {
        if (playerInput == null || !playerInput.TryToggleSelectedAutoFire())
        {
            return false;
        }

        RefreshStatus(true);
        return true;
    }

    public bool TryBeginManualTarget()
    {
        if (playerInput == null)
        {
            return false;
        }

        if (playerInput.ManualTargetArmed)
        {
            playerInput.CancelManualTarget();
            RefreshStatus(false);
            return true;
        }

        bool armed = playerInput.TryArmManualTarget();
        RefreshStatus(false);
        return armed;
    }

    public bool TryClearManualTarget()
    {
        if (playerInput == null
            || !playerInput.TryClearSelectedManualTarget())
        {
            return false;
        }

        RefreshStatus(true);
        return true;
    }

    public bool TryBeginBlindFire()
    {
        if (playerInput == null)
        {
            return false;
        }

        if (playerInput.BlindFireArmed)
        {
            playerInput.CancelBlindFire();
            RefreshStatus(false);
            return true;
        }

        bool armed = playerInput.TryArmBlindFire();
        RefreshStatus(false);
        return armed;
    }

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

        RefreshStatus(true);
    }

    private void OnDisable()
    {
        if (selectionManager != null)
        {
            selectionManager.SelectionMembershipChanged -= OnSelectionChanged;
        }

        currentStatus = default;
        displayText = string.Empty;
        ClearManualEligibility();
    }

    private void Update()
    {
        RefreshStatus(false);
    }

    private void OnGUI()
    {
        if (!currentStatus.HasSelection)
        {
            return;
        }

        GUI.Box(PanelRect, string.Empty);
        GUI.Label(new Rect(22f, 20f, 390f, 302f), displayText);

        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && playerInput != null
            && selectionManager != null
            && selectionManager.SelectedCount == 1;
        if (GUI.Button(new Rect(22f, 330f, 188f, 30f),
            currentStatus.AutoFireEnabled ? "Auto Fire: ON" : "Auto Fire: OFF"))
        {
            TryToggleAutoFire();
        }

        if (GUI.Button(new Rect(214f, 330f, 188f, 30f),
            "Manual Target"))
        {
            TryBeginManualTarget();
        }

        if (GUI.Button(new Rect(22f, 366f, 188f, 30f),
            "Clear Target"))
        {
            TryClearManualTarget();
        }

        if (GUI.Button(new Rect(214f, 366f, 188f, 30f),
            "Blind Fire"))
        {
            TryBeginBlindFire();
        }

        GUI.enabled = previousEnabled;
        GUI.Label(new Rect(22f, 400f, 390f, 55f), InteractionHint);
    }

    private void OnSelectionChanged()
    {
        RefreshStatus(true);
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
    }

    private void RefreshStatus(bool forceEligibility)
    {
        ShipDestinationController selected = selectionManager != null
            ? selectionManager.PrimarySelectedShip
            : null;
        ShipCombatState combatState = selected != null
            ? selected.GetComponent<ShipCombatState>()
            : null;
        GameObject shipRoot = combatState != null
            ? selected.gameObject
            : null;
        GameObject manualTarget = combatState != null
            ? combatState.ManualTarget
            : null;
        ShipCombatAIController ai = selected != null
            ? selected.GetComponent<ShipCombatAIController>()
            : null;
        GameObject fireTarget = manualTarget != null
            ? manualTarget
            : ai != null && ai.isActiveAndEnabled
                ? ai.CurrentTargetShipRoot
                : null;

        if (shipRoot != lastShipRoot || fireTarget != lastFireTarget)
        {
            lastShipRoot = shipRoot;
            lastFireTarget = fireTarget;
            ClearManualEligibility();
            forceEligibility = true;
        }

        FireEligibilityResult? result = null;
        if (fireTarget != null
            && ai != null
            && ai.isActiveAndEnabled
            && ai.CurrentTargetShipRoot == fireTarget
            && ai.HasLastFireEligibility)
        {
            result = ai.LastFireEligibility;
            ClearManualEligibility();
        }
        else if (manualTarget != null && shipRoot != null)
        {
            ShipFireEligibility eligibility =
                selected.GetComponent<ShipFireEligibility>();
            if (eligibility != null
                && (forceEligibility
                    || Time.unscaledTime >= nextManualEligibilityTime))
            {
                CombatRelationship relationship =
                    CombatRelationshipResolver.Resolve(
                        selected.GetComponent<ShipCombatAffiliation>(),
                        manualTarget.GetComponent<ShipCombatAffiliation>());
                hasManualEligibility = eligibility.TryEvaluate(
                    manualTarget,
                    relationship == CombatRelationship.Hostile,
                    out manualEligibility);
                nextManualEligibilityTime = Time.unscaledTime
                    + ManualEligibilityRefreshSeconds;
            }

            if (hasManualEligibility)
            {
                result = manualEligibility;
            }
        }

        currentStatus = CombatStatusReadModel.Capture(
            selectionManager, playerInput, fireTarget, result);
        displayText = CombatStatusFormatter.Format(currentStatus);
    }

    private void ClearManualEligibility()
    {
        hasManualEligibility = false;
        manualEligibility = default;
        nextManualEligibilityTime = 0f;
    }
}
