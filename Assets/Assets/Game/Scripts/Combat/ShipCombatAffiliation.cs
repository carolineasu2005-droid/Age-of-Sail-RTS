using UnityEngine;

[AddComponentMenu("Age of Sail/Combat/Ship Combat Affiliation")]
[DisallowMultipleComponent]
public sealed class ShipCombatAffiliation : MonoBehaviour
{
    public const int UnconfiguredTeamId = -1;

    [SerializeField]
    private int teamId = UnconfiguredTeamId;


    public int TeamId => teamId;

    public bool IsConfigured => teamId >= 0;
}
