using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu(
    "Age of Sail/Combat/Ship Dispersion Configuration"
)]
public sealed class ShipDispersionConfiguration : MonoBehaviour
{
    [SerializeField]
    private DispersionProfile dispersionProfile;


    public DispersionProfile DispersionProfile => dispersionProfile;
}
