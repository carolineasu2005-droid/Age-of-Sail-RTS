using System.Collections.Generic;
using UnityEngine;

public sealed class AutoFireMuzzleOrderReceiver
    : MonoBehaviour, ICombatVFXEventReceiver
{
    public List<CombatSide> Sides { get; } = new List<CombatSide>();

    public void OnMuzzleFire(CombatMuzzleFireEvent eventData)
    {
        Transform current = eventData.MuzzleTransform;
        while (current != null && current.name != "Port"
            && current.name != "Starboard")
        {
            current = current.parent;
        }

        if (current != null)
        {
            Sides.Add(current.name == "Port"
                ? CombatSide.Port : CombatSide.Starboard);
        }
    }

    public void OnWaterImpact(CombatWaterImpactEvent eventData) { }

    public void OnHullImpact(CombatHullImpactEvent eventData) { }
}
