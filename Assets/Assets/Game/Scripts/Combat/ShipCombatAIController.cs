using UnityEngine;

public enum CombatAITargetChangeReason
{
    None,
    Acquired,
    Sinking,
    RelationshipChanged,
    TargetInvalid,
    ShooterIncapable
}

[AddComponentMenu("Age of Sail/Combat/Ship Combat AI Controller")]
[DisallowMultipleComponent]
public sealed class ShipCombatAIController : MonoBehaviour
{
    [SerializeField]
    private CombatAIProfile profile;

    private CombatAIState state = CombatAIState.NoTarget;
    private GameObject currentTargetShipRoot;
    private float nextThinkTimeSeconds;
    private GameObject preferredSideTargetShipRoot;
    private CombatSide? preferredSide;
    private CombatAIMovementAdapter movementAdapter;
    private ShipDestinationController movementDestinationController;
    private CombatBroadsidePoseResult lastPose;
    private FireEligibilityResult lastFireEligibility;
    private bool hasLastFireEligibility;
    private TargetedFireExecutionResult lastFireResult;
    private bool hasLastFireResult;
    private CombatSide? lastFireSide;
    private CombatRelationship targetRelationship;
    private ShipCombatLifecycleState? targetLifecycle;
    private CombatAITargetChangeReason lastTargetChangeReason;

    public CombatAIProfile Profile => profile;
    public CombatAIState State => state;
    public GameObject CurrentTargetShipRoot => currentTargetShipRoot;
    public CombatBroadsidePoseResult LastPose => lastPose;
    public bool HasLastFireEligibility => hasLastFireEligibility;
    public FireEligibilityResult LastFireEligibility => lastFireEligibility;
    public bool HasLastFireResult => hasLastFireResult;
    public TargetedFireExecutionResult LastFireResult => lastFireResult;
    public CombatSide? LastFireSide => lastFireSide;
    public CombatRelationship TargetRelationship => targetRelationship;
    public ShipCombatLifecycleState? TargetLifecycle => targetLifecycle;
    public CombatAITargetChangeReason LastTargetChangeReason =>
        lastTargetChangeReason;
    public CombatAIMovementCommandStatus LastMovementCommandStatus =>
        movementAdapter != null
            ? movementAdapter.LastCommandStatus
            : CombatAIMovementCommandStatus.Unavailable;

    private void OnEnable()
    {
        state = CombatAIState.NoTarget;
        currentTargetShipRoot = null;
        nextThinkTimeSeconds = 0f;
        preferredSideTargetShipRoot = null;
        preferredSide = null;
        movementAdapter = null;
        movementDestinationController = null;
        lastPose = default;
        lastFireEligibility = default;
        hasLastFireEligibility = false;
        lastFireResult = default;
        hasLastFireResult = false;
        lastFireSide = null;
        targetRelationship = CombatRelationship.Unknown;
        targetLifecycle = null;
        lastTargetChangeReason = CombatAITargetChangeReason.None;
    }

    private void Update()
    {
        Think(Time.time);
    }

    // Returns true only when tactical decisions ran. Public for deterministic
    // EditMode coverage; the component's Update supplies Unity time in play.
    public bool Think(float currentTimeSeconds)
    {
        if (float.IsNaN(currentTimeSeconds)
            || float.IsInfinity(currentTimeSeconds)
            || currentTimeSeconds < nextThinkTimeSeconds)
        {
            return false;
        }

        float interval = profile != null && profile.IsValid
            ? profile.ThinkIntervalSeconds
            : 0.25f;
        nextThinkTimeSeconds = currentTimeSeconds + interval;
        lastPose = default;
        lastFireEligibility = default;
        hasLastFireEligibility = false;
        targetRelationship = CombatRelationship.Unknown;
        targetLifecycle = null;

        GameObject previousTarget = currentTargetShipRoot;
        ShipIntegrity integrity = GetComponent<ShipIntegrity>();
        if (profile == null
            || !profile.IsValid
            || integrity == null
            || !integrity.IsInitialized
            || integrity.IsCombatDisabled
            || GetComponent<ShipCombatGeometry>() == null)
        {
            currentTargetShipRoot = null;
            state = CombatAIState.CombatIncapable;
            if (previousTarget != null)
            {
                lastTargetChangeReason =
                    CombatAITargetChangeReason.ShooterIncapable;
            }
            ResetTacticalMovementState();
            return true;
        }

        currentTargetShipRoot = CombatAITargetAcquisition.Select(
            gameObject,
            currentTargetShipRoot,
            profile.TargetAcquisitionRadiusMeters
        );
        if (currentTargetShipRoot != previousTarget)
        {
            lastTargetChangeReason = ClassifyTargetChange(previousTarget);
        }
        state = currentTargetShipRoot == null
            ? CombatAIState.NoTarget
            : CombatAIState.Maneuvering;

        if (currentTargetShipRoot == null)
        {
            ResetTacticalMovementState();
            return true;
        }

        targetRelationship = CombatRelationshipResolver.Resolve(
            GetComponent<ShipCombatAffiliation>(),
            currentTargetShipRoot.GetComponent<ShipCombatAffiliation>()
        );
        ShipIntegrity targetIntegrity =
            currentTargetShipRoot.GetComponent<ShipIntegrity>();
        targetLifecycle = targetIntegrity != null && targetIntegrity.IsInitialized
            ? targetIntegrity.LifecycleState
            : (ShipCombatLifecycleState?)null;

        if (preferredSideTargetShipRoot != currentTargetShipRoot)
        {
            preferredSide = null;
            preferredSideTargetShipRoot = currentTargetShipRoot;
        }

        if (!CombatBroadsidePoseSolver.TrySolve(
            gameObject,
            currentTargetShipRoot,
            preferredSide,
            profile,
            out CombatBroadsidePoseResult pose
        ))
        {
            preferredSide = null;
            movementAdapter?.ResetTacticalState();
            return true;
        }

        preferredSide = pose.PreferredSide;
        lastPose = pose;
        ShipDestinationController destination =
            GetComponent<ShipDestinationController>();
        if (destination != movementDestinationController)
        {
            movementDestinationController = destination;
            movementAdapter = destination != null
                ? new CombatAIMovementAdapter(destination)
                : null;
        }

        movementAdapter?.TryApply(pose, currentTargetShipRoot, profile);

        ShipFireEligibility eligibility = GetComponent<ShipFireEligibility>();
        ShipBroadsideFireExecutor executor =
            GetComponent<ShipBroadsideFireExecutor>();
        if (eligibility == null || executor == null
            || !CombatAITargetAcquisition.IsLegal(
                gameObject,
                currentTargetShipRoot,
                profile.TargetAcquisitionRadiusMeters
            ))
        {
            return true;
        }

        targetRelationship = CombatRelationshipResolver.Resolve(
            GetComponent<ShipCombatAffiliation>(),
            currentTargetShipRoot.GetComponent<ShipCombatAffiliation>()
        );
        bool hostile = targetRelationship == CombatRelationship.Hostile;
        if (!hostile
            || !eligibility.TryEvaluate(
                currentTargetShipRoot,
                hostile,
                out FireEligibilityResult fireVerdict
            ))
        {
            return true;
        }

        lastFireEligibility = fireVerdict;
        hasLastFireEligibility = true;

        if (!fireVerdict.LifecycleAllowsFire)
        {
            currentTargetShipRoot = null;
            state = CombatAIState.CombatIncapable;
            ResetTacticalMovementState();
            return true;
        }

        CombatSide? firingSide = CombatAIFireDecision.SelectSide(
            fireVerdict,
            preferredSide
        );
        if (!firingSide.HasValue)
        {
            return true;
        }

        // The targeted command re-evaluates eligibility before sampling or
        // committing a side reload. One call is the entire Think fire budget.
        ShipTargetedFireCommand command = new ShipTargetedFireCommand(
            eligibility,
            executor
        );
        bool accepted = command.TryExecuteWithRuntimeSeed(
            currentTargetShipRoot,
            hostile,
            out lastFireResult
        );
        hasLastFireResult = true;
        lastFireEligibility = lastFireResult.Eligibility;
        if (accepted)
        {
            lastFireSide = lastFireResult.BroadsideExecution.Side;
            state = CombatAIState.Engaging;
        }
        return true;
    }

    private void ResetTacticalMovementState()
    {
        preferredSideTargetShipRoot = null;
        preferredSide = null;
        movementAdapter?.ResetTacticalState();
    }

    private CombatAITargetChangeReason ClassifyTargetChange(
        GameObject previousTarget
    )
    {
        if (ReferenceEquals(previousTarget, null))
        {
            return CombatAITargetChangeReason.Acquired;
        }

        if (previousTarget == null)
        {
            return CombatAITargetChangeReason.TargetInvalid;
        }

        ShipIntegrity previousIntegrity =
            previousTarget.GetComponent<ShipIntegrity>();
        if (previousIntegrity != null && previousIntegrity.IsSinking)
        {
            return CombatAITargetChangeReason.Sinking;
        }

        CombatRelationship previousRelationship =
            CombatRelationshipResolver.Resolve(
                GetComponent<ShipCombatAffiliation>(),
                previousTarget.GetComponent<ShipCombatAffiliation>()
            );
        return previousRelationship != CombatRelationship.Hostile
            ? CombatAITargetChangeReason.RelationshipChanged
            : CombatAITargetChangeReason.TargetInvalid;
    }
}
