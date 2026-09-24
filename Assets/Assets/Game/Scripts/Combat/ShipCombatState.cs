using UnityEngine;

public enum CombatSide
{
    Port,
    Starboard
}

public enum BroadsideReloadState
{
    Ready,
    Reloading
}

[AddComponentMenu("Age of Sail/Combat/Ship Combat State")]
[DisallowMultipleComponent]
public sealed class ShipCombatState : MonoBehaviour
{
    private const float MinimumReloadDurationSeconds = 0.01f;

    [Header("Broadside Reload")]

    [SerializeField]
    [Min(MinimumReloadDurationSeconds)]
    private float broadsideReloadDurationSeconds = 30f;

    private bool autoFireEnabled;

    private GameObject manualTarget;

    private bool blindFireEnabled;

    private BroadsideReloadState portBroadsideState =
        BroadsideReloadState.Ready;

    private float portReloadRemainingSeconds;

    private BroadsideReloadState starboardBroadsideState =
        BroadsideReloadState.Ready;

    private float starboardReloadRemainingSeconds;


    public bool AutoFireEnabled => autoFireEnabled;

    public GameObject ManualTarget => manualTarget;

    public bool BlindFireEnabled => blindFireEnabled;

    public float BroadsideReloadDurationSeconds =>
        GetValidatedReloadDurationSeconds();

    public BroadsideReloadState PortBroadsideState => portBroadsideState;

    public float PortReloadRemainingSeconds =>
        portReloadRemainingSeconds;

    public BroadsideReloadState StarboardBroadsideState =>
        starboardBroadsideState;

    public float StarboardReloadRemainingSeconds =>
        starboardReloadRemainingSeconds;


    private void Update()
    {
        AdvanceReloads(Time.deltaTime);
    }


    private void OnValidate()
    {
        broadsideReloadDurationSeconds =
            GetValidatedReloadDurationSeconds();
    }


    public void SetAutoFireEnabled(bool enabled)
    {
        if (enabled)
        {
            manualTarget = null;
        }

        autoFireEnabled = enabled;
    }


    public bool AssignManualTarget(GameObject targetShipRoot)
    {
        if (!IsValidManualTarget(targetShipRoot))
        {
            return false;
        }

        manualTarget = targetShipRoot;
        autoFireEnabled = false;
        return true;
    }


    public void ClearManualTarget()
    {
        manualTarget = null;
    }


    public void SetBlindFireEnabled(bool enabled)
    {
        blindFireEnabled = enabled;
    }


    private bool IsValidManualTarget(GameObject targetShipRoot)
    {
        return targetShipRoot != null
            && targetShipRoot != gameObject
            && targetShipRoot.GetComponent<ShipCombatState>() != null;
    }


    public bool TryCommitBroadsideFire(CombatSide side)
    {
        switch (side)
        {
            case CombatSide.Port:
                return TryBeginReload(
                    ref portBroadsideState,
                    ref portReloadRemainingSeconds
                );

            case CombatSide.Starboard:
                return TryBeginReload(
                    ref starboardBroadsideState,
                    ref starboardReloadRemainingSeconds
                );

            default:
                return false;
        }
    }


    private bool TryBeginReload(
        ref BroadsideReloadState state,
        ref float reloadRemainingSeconds
    )
    {
        if (state != BroadsideReloadState.Ready)
        {
            return false;
        }

        state = BroadsideReloadState.Reloading;
        reloadRemainingSeconds = GetValidatedReloadDurationSeconds();
        return true;
    }


    private void AdvanceReloads(float elapsedSeconds)
    {
        if (!IsFinite(elapsedSeconds) || elapsedSeconds <= 0f)
        {
            return;
        }

        AdvanceReload(
            ref portBroadsideState,
            ref portReloadRemainingSeconds,
            elapsedSeconds
        );
        AdvanceReload(
            ref starboardBroadsideState,
            ref starboardReloadRemainingSeconds,
            elapsedSeconds
        );
    }


    private static void AdvanceReload(
        ref BroadsideReloadState state,
        ref float reloadRemainingSeconds,
        float elapsedSeconds
    )
    {
        if (state != BroadsideReloadState.Reloading)
        {
            return;
        }

        reloadRemainingSeconds = Mathf.Max(
            0f,
            reloadRemainingSeconds - elapsedSeconds
        );

        if (reloadRemainingSeconds <= 0f)
        {
            reloadRemainingSeconds = 0f;
            state = BroadsideReloadState.Ready;
        }
    }


    private float GetValidatedReloadDurationSeconds()
    {
        if (!IsFinite(broadsideReloadDurationSeconds))
        {
            return MinimumReloadDurationSeconds;
        }

        return Mathf.Max(
            MinimumReloadDurationSeconds,
            broadsideReloadDurationSeconds
        );
    }


    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
