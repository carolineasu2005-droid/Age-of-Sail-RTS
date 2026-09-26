using UnityEngine;

[AddComponentMenu("Age of Sail/Combat/Ship Integrity")]
[DisallowMultipleComponent]
public sealed class ShipIntegrity : MonoBehaviour
{
    [SerializeField]
    private ShipIntegrityProfile integrityProfile;

    private bool initialized;

    private float maximumIntegrity;

    private float currentIntegrity;

    private float combatDisabledThresholdNormalized;

    private float sinkingThresholdNormalized;

    private ShipCombatLifecycleState lifecycleState =
        ShipCombatLifecycleState.Operational;


    public ShipIntegrityProfile IntegrityProfile => integrityProfile;

    public bool IsInitialized => initialized;

    public float MaximumIntegrity => maximumIntegrity;

    public float CurrentIntegrity => currentIntegrity;

    public ShipCombatLifecycleState LifecycleState => lifecycleState;

    public bool IsCombatDisabled =>
        lifecycleState != ShipCombatLifecycleState.Operational;

    public bool IsSinking =>
        lifecycleState == ShipCombatLifecycleState.Sinking;


    private void Awake()
    {
        TryInitialize();
    }


    public bool TryInitialize()
    {
        if (initialized)
        {
            return true;
        }

        return TryInitializeFromProfile();
    }


    public bool TryApplyIntegrityLoss(
        float integrityLoss,
        out ShipIntegrityTransition transition
    )
    {
        transition = default;

        if (!initialized
            || !IsFinite(integrityLoss)
            || integrityLoss <= 0f)
        {
            return false;
        }

        float previousIntegrity = currentIntegrity;
        ShipCombatLifecycleState previousLifecycleState = lifecycleState;

        currentIntegrity = Mathf.Max(0f, currentIntegrity - integrityLoss);
        lifecycleState = EvaluateLifecycleState(currentIntegrity);
        transition = new ShipIntegrityTransition(
            previousIntegrity,
            currentIntegrity,
            previousLifecycleState,
            lifecycleState
        );
        return true;
    }


    private bool TryInitializeFromProfile()
    {
        initialized = false;
        maximumIntegrity = 0f;
        currentIntegrity = 0f;
        combatDisabledThresholdNormalized = 0f;
        sinkingThresholdNormalized = 0f;
        lifecycleState = ShipCombatLifecycleState.Operational;

        if (!HasValidProfile(integrityProfile))
        {
            return false;
        }

        maximumIntegrity = integrityProfile.MaximumIntegrity;
        currentIntegrity = maximumIntegrity;
        combatDisabledThresholdNormalized =
            integrityProfile.CombatDisabledThresholdNormalized;
        sinkingThresholdNormalized =
            integrityProfile.SinkingThresholdNormalized;
        lifecycleState = EvaluateLifecycleState(currentIntegrity);
        initialized = true;
        return true;
    }


    private ShipCombatLifecycleState EvaluateLifecycleState(
        float integrity
    )
    {
        float normalizedIntegrity = integrity / maximumIntegrity;

        if (normalizedIntegrity <= sinkingThresholdNormalized)
        {
            return ShipCombatLifecycleState.Sinking;
        }

        if (normalizedIntegrity <= combatDisabledThresholdNormalized)
        {
            return ShipCombatLifecycleState.CombatDisabled;
        }

        return ShipCombatLifecycleState.Operational;
    }


    private static bool HasValidProfile(ShipIntegrityProfile profile)
    {
        if (profile == null
            || !IsFinite(profile.MaximumIntegrity)
            || profile.MaximumIntegrity <= 0f
            || !IsFinite(profile.CombatDisabledThresholdNormalized)
            || !IsFinite(profile.SinkingThresholdNormalized))
        {
            return false;
        }

        return profile.SinkingThresholdNormalized >= 0f
            && profile.SinkingThresholdNormalized
                <= profile.CombatDisabledThresholdNormalized
            && profile.CombatDisabledThresholdNormalized <= 1f;
    }


    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
