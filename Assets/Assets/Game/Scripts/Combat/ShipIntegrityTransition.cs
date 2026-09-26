public enum ShipCombatLifecycleState
{
    Operational,
    CombatDisabled,
    Sinking
}

public readonly struct ShipIntegrityTransition
{
    internal ShipIntegrityTransition(
        float previousIntegrity,
        float currentIntegrity,
        ShipCombatLifecycleState previousLifecycleState,
        ShipCombatLifecycleState currentLifecycleState
    )
    {
        PreviousIntegrity = previousIntegrity;
        CurrentIntegrity = currentIntegrity;
        PreviousLifecycleState = previousLifecycleState;
        CurrentLifecycleState = currentLifecycleState;
    }


    public float PreviousIntegrity { get; }

    public float CurrentIntegrity { get; }

    public ShipCombatLifecycleState PreviousLifecycleState { get; }

    public ShipCombatLifecycleState CurrentLifecycleState { get; }

    public bool LifecycleChanged =>
        PreviousLifecycleState != CurrentLifecycleState;
}
