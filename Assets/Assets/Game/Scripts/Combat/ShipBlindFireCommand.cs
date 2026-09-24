public sealed class ShipBlindFireCommand
{
    private readonly ShipFireEligibility fireEligibility;
    private readonly ShipCombatState combatState;


    public ShipBlindFireCommand(
        ShipFireEligibility fireEligibility,
        ShipCombatState combatState
    )
    {
        this.fireEligibility = fireEligibility;
        this.combatState = combatState;
    }


    public bool TryExecuteBlindFire(
        BlindFireAim aim,
        out BlindFireExecutionResult result
    )
    {
        result = default;

        if (!aim.IsValid)
        {
            result = new BlindFireExecutionResult(
                false,
                null,
                aim,
                default,
                BlindFireExecutionFailure.InvalidAim
            );
            return false;
        }

        if (!HasValidDependencies())
        {
            result = new BlindFireExecutionResult(
                false,
                null,
                aim,
                default,
                BlindFireExecutionFailure.EligibilityUnavailable
            );
            return false;
        }

        bool evaluated = aim.HasWorldAimPoint
            ? fireEligibility.TryEvaluateBlindFireAtPoint(
                aim.WorldAimPoint.Value,
                out BlindFireEligibilityResult eligibility
            )
            : fireEligibility.TryEvaluateBlindFireDirection(
                aim.WorldAimDirection,
                out eligibility
            );

        if (!evaluated)
        {
            result = new BlindFireExecutionResult(
                false,
                null,
                aim,
                default,
                BlindFireExecutionFailure.EligibilityUnavailable
            );
            return false;
        }

        if (!eligibility.CanBlindFire || !eligibility.Side.HasValue)
        {
            result = new BlindFireExecutionResult(
                false,
                eligibility.Side,
                eligibility.Aim,
                eligibility,
                BlindFireExecutionFailure.EligibilityRejected
            );
            return false;
        }

        CombatSide side = eligibility.Side.Value;

        if (!combatState.TryCommitBroadsideFire(side))
        {
            result = new BlindFireExecutionResult(
                false,
                side,
                eligibility.Aim,
                eligibility,
                BlindFireExecutionFailure.BroadsideCommitFailed
            );
            return false;
        }

        combatState.SetAutoFireEnabled(false);
        result = new BlindFireExecutionResult(
            true,
            side,
            eligibility.Aim,
            eligibility,
            BlindFireExecutionFailure.None
        );
        return true;
    }


    private bool HasValidDependencies()
    {
        return fireEligibility != null
            && combatState != null
            && fireEligibility.gameObject == combatState.gameObject;
    }
}
