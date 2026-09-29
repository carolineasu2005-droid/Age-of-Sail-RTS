public static class CombatAIFireDecision
{
    // The target-aware eligibility query resolves a single bearing and side.
    // Keep the two-side policy explicit for callers with two legal verdicts.
    public static CombatSide? SelectSide(
        bool portCanFire,
        bool starboardCanFire,
        CombatSide? preferredSide
    )
    {
        if (portCanFire && starboardCanFire)
        {
            return preferredSide ?? CombatSide.Port;
        }

        if (portCanFire)
        {
            return CombatSide.Port;
        }

        return starboardCanFire ? CombatSide.Starboard : (CombatSide?)null;
    }

    public static CombatSide? SelectSide(
        FireEligibilityResult eligibility,
        CombatSide? preferredSide
    )
    {
        return SelectSide(
            eligibility.CanFire && eligibility.Side == CombatSide.Port,
            eligibility.CanFire && eligibility.Side == CombatSide.Starboard,
            preferredSide
        );
    }
}
