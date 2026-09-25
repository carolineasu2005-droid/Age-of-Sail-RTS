using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu(
    "Age of Sail/Combat/Ship Projectile Flight Configuration"
)]
public sealed class ShipProjectileFlightConfiguration : MonoBehaviour
{
    [SerializeField]
    private ProjectileFlightProfile projectileFlightProfile;


    public ProjectileFlightProfile ProjectileFlightProfile =>
        projectileFlightProfile;
}
