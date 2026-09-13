public interface ICombatVFXEventReceiver
{
    void OnMuzzleFire(CombatMuzzleFireEvent eventData);

    void OnWaterImpact(CombatWaterImpactEvent eventData);

    void OnHullImpact(CombatHullImpactEvent eventData);
}
