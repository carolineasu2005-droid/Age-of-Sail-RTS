public readonly struct CombatTerminalResolutionDiagnostics
{
    internal CombatTerminalResolutionDiagnostics(
        FoundationShotOutcome outcome,
        bool damageResolutionSucceeded,
        CombatDamageResult damageResult,
        CombatDamageResolutionFailure damageFailure
    )
    {
        Outcome = outcome;
        DamageResolutionSucceeded = damageResolutionSucceeded;
        DamageResult = damageResult;
        DamageFailure = damageFailure;
    }


    public FoundationShotOutcome Outcome { get; }

    public bool DamageResolutionSucceeded { get; }

    public CombatDamageResult DamageResult { get; }

    public CombatDamageResolutionFailure DamageFailure { get; }
}
