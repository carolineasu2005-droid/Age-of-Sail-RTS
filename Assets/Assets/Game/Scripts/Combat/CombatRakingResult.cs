public readonly struct CombatRakingResult
{
    internal CombatRakingResult(
        bool isValid,
        CombatRakingType type,
        float longitudinalAngleDegrees,
        float damageMultiplier
    )
    {
        IsValid = isValid;
        Type = type;
        LongitudinalAngleDegrees = longitudinalAngleDegrees;
        DamageMultiplier = damageMultiplier;
    }


    public bool IsValid { get; }

    public bool IsRaking => IsValid && Type != CombatRakingType.None;

    public CombatRakingType Type { get; }

    public float LongitudinalAngleDegrees { get; }

    public float DamageMultiplier { get; }
}
