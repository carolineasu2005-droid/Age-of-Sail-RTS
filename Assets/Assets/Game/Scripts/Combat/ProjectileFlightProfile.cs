using UnityEngine;

[CreateAssetMenu(
    fileName = "SO_ProjectileFlight_Foundation",
    menuName = "AgeOfSailRTS/Combat/Projectile Flight Profile"
)]
public sealed class ProjectileFlightProfile : ScriptableObject
{
    // TEMPORARY / PLAYTEST-BOUND / NOT BALANCE-FROZEN.
    // This is launch-state tuning only. It is not a runtime physics body,
    // range, hit, damage, or ammunition-inventory configuration.

    [SerializeField]
    private float nominalHorizontalSpeedMetersPerSecond = 120f;

    [SerializeField]
    private float gravityMagnitudeMetersPerSecondSquared = 9.81f;

    [SerializeField]
    private float maxLifetimeSeconds = 10f;


    public float NominalHorizontalSpeedMetersPerSecond =>
        nominalHorizontalSpeedMetersPerSecond;

    public float GravityMagnitudeMetersPerSecondSquared =>
        gravityMagnitudeMetersPerSecondSquared;

    public float MaxLifetimeSeconds => maxLifetimeSeconds;
}
