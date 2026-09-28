public static class CombatRelationshipResolver
{
    public static CombatRelationship Resolve(
        ShipCombatAffiliation source,
        ShipCombatAffiliation target
    )
    {
        if (source == null
            || target == null
            || !source.IsConfigured
            || !target.IsConfigured)
        {
            return CombatRelationship.Unknown;
        }

        return source.TeamId == target.TeamId
            ? CombatRelationship.Friendly
            : CombatRelationship.Hostile;
    }
}
