using UnityEngine;

public sealed class CombatVFXCountingReceiver
    : MonoBehaviour,
        ICombatVFXEventReceiver
{
    public int MuzzleFireCount { get; private set; }

    public int WaterImpactCount { get; private set; }

    public int HullImpactCount { get; private set; }


    public void OnMuzzleFire(CombatMuzzleFireEvent eventData)
    {
        MuzzleFireCount++;
    }


    public void OnWaterImpact(CombatWaterImpactEvent eventData)
    {
        WaterImpactCount++;
    }


    public void OnHullImpact(CombatHullImpactEvent eventData)
    {
        HullImpactCount++;
    }
}
