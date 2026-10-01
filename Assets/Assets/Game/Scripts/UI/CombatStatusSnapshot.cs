using System.Text;
using UnityEngine;

// A value copy for presentation. Combat owners remain the source of every field.
public readonly struct CombatStatusSnapshot
{
    internal CombatStatusSnapshot(
        GameObject shipRoot,
        bool autoFireEnabled,
        GameObject manualTarget,
        bool blindFireArmed,
        Vector3? lastBlindFirePointWorld,
        BroadsideReloadState portReloadState,
        float portReloadRemainingSeconds,
        BroadsideReloadState starboardReloadState,
        float starboardReloadRemainingSeconds,
        float? currentIntegrity,
        float? maximumIntegrity,
        ShipCombatLifecycleState? lifecycleState,
        GameObject fireTarget,
        FireEligibilityResult? fireEligibility,
        float? maximumRangeMeters)
    {
        ShipRoot = shipRoot;
        AutoFireEnabled = autoFireEnabled;
        ManualTarget = manualTarget;
        BlindFireArmed = blindFireArmed;
        LastBlindFirePointWorld = lastBlindFirePointWorld;
        PortReloadState = portReloadState;
        PortReloadRemainingSeconds = portReloadRemainingSeconds;
        StarboardReloadState = starboardReloadState;
        StarboardReloadRemainingSeconds = starboardReloadRemainingSeconds;
        CurrentIntegrity = currentIntegrity;
        MaximumIntegrity = maximumIntegrity;
        LifecycleState = lifecycleState;
        FireTarget = fireTarget;
        FireEligibility = fireEligibility;
        MaximumRangeMeters = maximumRangeMeters;
    }

    public bool HasSelection => ShipRoot != null;
    public GameObject ShipRoot { get; }
    public bool AutoFireEnabled { get; }
    public GameObject ManualTarget { get; }
    public bool BlindFireArmed { get; }
    public Vector3? LastBlindFirePointWorld { get; }
    public BroadsideReloadState PortReloadState { get; }
    public float PortReloadRemainingSeconds { get; }
    public BroadsideReloadState StarboardReloadState { get; }
    public float StarboardReloadRemainingSeconds { get; }
    public float? CurrentIntegrity { get; }
    public float? MaximumIntegrity { get; }
    public ShipCombatLifecycleState? LifecycleState { get; }
    public GameObject FireTarget { get; }
    public FireEligibilityResult? FireEligibility { get; }
    public float? MaximumRangeMeters { get; }
}

public static class CombatStatusReadModel
{
    public static CombatStatusSnapshot Capture(
        ShipSelectionManager selection,
        ShipPlayerCommandInput playerInput,
        GameObject fireTarget,
        FireEligibilityResult? fireEligibility)
    {
        ShipDestinationController selected = selection != null
            ? selection.PrimarySelectedShip
            : null;
        ShipCombatState combatState = selected != null
            ? selected.GetComponent<ShipCombatState>()
            : null;
        if (combatState == null)
        {
            return default;
        }

        GameObject shipRoot = selected.gameObject;
        ShipIntegrity integrity = selected.GetComponent<ShipIntegrity>();
        ShipFireEligibility eligibility =
            selected.GetComponent<ShipFireEligibility>();
        ShipCombatAIController ai =
            selected.GetComponent<ShipCombatAIController>();
        GameObject currentFireTarget = combatState.ManualTarget != null
            ? combatState.ManualTarget
            : ai != null && ai.isActiveAndEnabled
                ? ai.CurrentTargetShipRoot
                : null;
        if (currentFireTarget == null || fireTarget != currentFireTarget)
        {
            fireEligibility = null;
        }

        bool blindFireArmed = playerInput != null
            && playerInput.BlindFireArmed
            && playerInput.SelectedBlindFireShooterRoot == shipRoot;
        Vector3? lastBlindFirePoint = null;
        if (playerInput != null)
        {
            BlindFirePlayerCommandResult last =
                playerInput.LastBlindFireCommandResult;
            if (last.Attempted
                && last.ShooterShipRoot == shipRoot
                && last.EligibilityAvailable
                && last.Eligibility.Aim.IsValid)
            {
                lastBlindFirePoint = last.Eligibility.Aim.WorldAimPoint;
            }
        }

        bool hasIntegrity = integrity != null && integrity.IsInitialized;
        return new CombatStatusSnapshot(
            shipRoot,
            combatState.AutoFireEnabled,
            combatState.ManualTarget,
            blindFireArmed,
            lastBlindFirePoint,
            combatState.PortBroadsideState,
            combatState.PortReloadRemainingSeconds,
            combatState.StarboardBroadsideState,
            combatState.StarboardReloadRemainingSeconds,
            hasIntegrity ? integrity.CurrentIntegrity : (float?)null,
            hasIntegrity ? integrity.MaximumIntegrity : (float?)null,
            hasIntegrity ? integrity.LifecycleState
                : (ShipCombatLifecycleState?)null,
            currentFireTarget,
            fireEligibility,
            eligibility != null ? eligibility.MaximumRangeMeters
                : (float?)null
        );
    }
}

public static class CombatStatusFormatter
{
    public static string Format(CombatStatusSnapshot status)
    {
        if (!status.HasSelection)
        {
            return string.Empty;
        }

        StringBuilder text = new StringBuilder(400);
        text.AppendLine(status.ShipRoot.name);
        text.Append("Auto Fire: ").AppendLine(status.AutoFireEnabled ? "ON" : "OFF");
        text.Append("Manual Target: ").AppendLine(
            status.ManualTarget != null ? status.ManualTarget.name : "None");
        text.AppendLine(FormatBlindFire(
            status.BlindFireArmed, status.LastBlindFirePointWorld));
        text.Append("Port: ").AppendLine(FormatReload(
            status.PortReloadState, status.PortReloadRemainingSeconds));
        text.Append("Starboard: ").AppendLine(FormatReload(
            status.StarboardReloadState,
            status.StarboardReloadRemainingSeconds));
        text.Append("Integrity: ").AppendLine(
            status.CurrentIntegrity.HasValue && status.MaximumIntegrity.HasValue
                ? $"{status.CurrentIntegrity.Value:0} / {status.MaximumIntegrity.Value:0}"
                : "N/A");
        text.Append("Lifecycle: ").AppendLine(
            status.LifecycleState.HasValue
                ? FormatLifecycle(status.LifecycleState.Value)
                : "N/A");
        text.Append("Fire Target: ").AppendLine(
            status.FireTarget != null ? status.FireTarget.name : "None");

        if (!status.FireEligibility.HasValue)
        {
            text.AppendLine("Fire Check: N/A (no resolved verdict)");
            text.AppendLine("Range: N/A");
            text.AppendLine("Port Arc: N/A");
            text.AppendLine("Starboard Arc: N/A");
            text.Append("Obstruction: N/A");
            return text.ToString();
        }

        FireEligibilityResult result = status.FireEligibility.Value;
        text.Append("Fire Check: ").AppendLine(result.CanFire
            ? "Can Fire"
            : "Cannot Fire - " + FormatFailure(result.FailureReasons));
        text.Append("Range: ").AppendLine(
            result.DistanceMeters > 0f && status.MaximumRangeMeters.HasValue
                ? $"{result.DistanceMeters:0} / {status.MaximumRangeMeters.Value:0} m"
                : "N/A");
        text.Append("Port Arc: ").AppendLine(FormatArc(result, CombatSide.Port));
        text.Append("Starboard Arc: ").AppendLine(
            FormatArc(result, CombatSide.Starboard));
        text.Append("Obstruction: ").Append(result.HasObstructionPath
            ? result.Blocked ? "Blocked" : "Clear"
            : "N/A");
        return text.ToString();
    }

    public static string FormatFailure(FireEligibilityFailure reasons)
    {
        if ((reasons & FireEligibilityFailure.LifecycleDisallowsFire) != 0)
            return "Ship Disabled";
        if ((reasons & FireEligibilityFailure.TargetLifecycleIllegal) != 0)
            return "Target Sinking";
        if ((reasons & FireEligibilityFailure.TargetIllegal) != 0)
            return "Target Illegal";
        if ((reasons & FireEligibilityFailure.BeyondMaximumRange) != 0)
            return "Out of Range";
        if ((reasons & FireEligibilityFailure.NoBroadsideArc) != 0)
            return "Wrong Arc";
        if ((reasons & FireEligibilityFailure.Obstructed) != 0)
            return "Obstructed";
        if ((reasons & FireEligibilityFailure.BroadsideReloading) != 0)
            return "Reloading";
        return reasons == FireEligibilityFailure.None
            ? "None"
            : reasons.ToString();
    }

    public static string FormatBlindFire(bool armed, Vector3? lastPointWorld)
    {
        return "Blind Fire: " + (armed ? "Armed" : "Idle")
            + "\nLast Blind Aim: "
            + (lastPointWorld.HasValue
                ? FormatPoint(lastPointWorld.Value)
                : "None");
    }

    public static string FormatLifecycle(ShipCombatLifecycleState state)
    {
        switch (state)
        {
            case ShipCombatLifecycleState.Operational:
                return "Operational";
            case ShipCombatLifecycleState.CombatDisabled:
                return "Disabled";
            case ShipCombatLifecycleState.Sinking:
                return "Sinking";
            default:
                return state.ToString();
        }
    }

    private static string FormatReload(
        BroadsideReloadState state, float remainingSeconds)
    {
        return state == BroadsideReloadState.Reloading
            ? $"RELOADING ({remainingSeconds:0.0} s)"
            : "READY";
    }

    private static string FormatArc(
        FireEligibilityResult result, CombatSide side)
    {
        if ((result.FailureReasons & FireEligibilityFailure.NoBroadsideArc) != 0)
        {
            return "Wrong Arc";
        }

        if (result.Side != side)
        {
            return "N/A";
        }

        return result.InBroadsideArc ? "Legal" : "Wrong Arc";
    }

    private static string FormatPoint(Vector3 point)
    {
        return $"({point.x:0.0}, {point.y:0.0}, {point.z:0.0})";
    }
}
