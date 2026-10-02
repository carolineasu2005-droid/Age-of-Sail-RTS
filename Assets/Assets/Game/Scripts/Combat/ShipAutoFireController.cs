using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Age of Sail/Combat/Ship Auto Fire Controller")]
[DisallowMultipleComponent]
public sealed class ShipAutoFireController : MonoBehaviour
{
    private const float DefaultEvaluationIntervalSeconds = 0.25f;

    [SerializeField]
    [Min(0.01f)]
    private float evaluationIntervalSeconds = DefaultEvaluationIntervalSeconds;

    private bool hasScheduledEvaluation;
    private float nextEvaluationTime;

    public float EvaluationIntervalSeconds => GetValidatedInterval();

    public int EvaluationCount { get; private set; }

    public float LastEvaluationTime { get; private set; }

    public bool HasLastSelection { get; private set; }

    public AutoTargetSelectionResult LastAutoTargetSelectionResult { get; private set; }

    public bool LastPortCommandAttempted { get; private set; }

    public TargetedFireExecutionResult LastPortExecutionResult { get; private set; }

    public bool LastStarboardCommandAttempted { get; private set; }

    public TargetedFireExecutionResult LastStarboardExecutionResult { get; private set; }


    private void Update()
    {
        TryRunAutoFire(Time.time);
    }


    private void OnDisable()
    {
        hasScheduledEvaluation = false;
    }


    private void OnValidate()
    {
        evaluationIntervalSeconds = GetValidatedInterval();
    }


    // The first observed enabled call runs immediately. Inactive calls reset the
    // schedule so re-enabling also runs on the next Update.
    public bool TryRunAutoFire(float currentTime)
    {
        ShipCombatState combatState = GetComponent<ShipCombatState>();
        if (!isActiveAndEnabled
            || combatState == null
            || !combatState.AutoFireEnabled
            || combatState.ManualTarget != null)
        {
            hasScheduledEvaluation = false;
            return false;
        }

        if (!IsFinite(currentTime)
            || (hasScheduledEvaluation && currentTime < nextEvaluationTime
                && currentTime >= LastEvaluationTime))
        {
            return false;
        }

        hasScheduledEvaluation = true;
        nextEvaluationTime = currentTime + EvaluationIntervalSeconds;
        LastEvaluationTime = currentTime;
        EvaluationCount++;
        HasLastSelection = false;
        LastAutoTargetSelectionResult = default;
        LastPortCommandAttempted = false;
        LastPortExecutionResult = default;
        LastStarboardCommandAttempted = false;
        LastStarboardExecutionResult = default;

        ShipFireEligibility eligibility = GetComponent<ShipFireEligibility>();
        ShipBroadsideFireExecutor executor =
            GetComponent<ShipBroadsideFireExecutor>();
        if (eligibility == null
            || executor == null
            || !ShipAutoTargetCandidateProvider.TryCollect(gameObject,
                out IReadOnlyList<AutoTargetCandidate> candidates)
            || !ShipAutoTargetSelector.TrySelect(gameObject, candidates,
                out AutoTargetSelectionResult selection))
        {
            return true;
        }

        HasLastSelection = true;
        LastAutoTargetSelectionResult = selection;
        ShipTargetedFireCommand command =
            new ShipTargetedFireCommand(eligibility, executor);

        if (selection.PortSelection.HasTarget
            && TryResolveSourceCandidate(candidates, selection.PortSelection,
                out AutoTargetCandidate portCandidate))
        {
            LastPortCommandAttempted = true;
            command.TryExecuteWithRuntimeSeed(
                portCandidate.TargetShipRoot,
                portCandidate.RelationshipAllowsFire,
                out TargetedFireExecutionResult portResult);
            LastPortExecutionResult = portResult;
        }

        if (combatState.AutoFireEnabled
            && combatState.ManualTarget == null
            && selection.StarboardSelection.HasTarget
            && TryResolveSourceCandidate(candidates,
                selection.StarboardSelection,
                out AutoTargetCandidate starboardCandidate))
        {
            LastStarboardCommandAttempted = true;
            command.TryExecuteWithRuntimeSeed(
                starboardCandidate.TargetShipRoot,
                starboardCandidate.RelationshipAllowsFire,
                out TargetedFireExecutionResult starboardResult);
            LastStarboardExecutionResult = starboardResult;
        }

        return true;
    }


    private static bool TryResolveSourceCandidate(
        IReadOnlyList<AutoTargetCandidate> candidates,
        AutoTargetSideSelectionResult selection,
        out AutoTargetCandidate candidate)
    {
        candidate = default;
        int index = selection.CandidateIndex;
        if (!selection.HasTarget
            || candidates == null
            || index < 0
            || index >= candidates.Count
            || selection.TargetShipRoot == null
            || candidates[index].TargetShipRoot != selection.TargetShipRoot)
        {
            return false;
        }

        candidate = candidates[index];
        return true;
    }


    private float GetValidatedInterval()
    {
        return IsFinite(evaluationIntervalSeconds)
            && evaluationIntervalSeconds > 0f
            ? evaluationIntervalSeconds
            : DefaultEvaluationIntervalSeconds;
    }


    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
