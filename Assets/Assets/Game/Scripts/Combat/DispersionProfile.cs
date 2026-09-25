using UnityEngine;

[CreateAssetMenu(
    fileName = "SO_Dispersion_Foundation",
    menuName = "AgeOfSailRTS/Combat/Dispersion Profile"
)]
public sealed class DispersionProfile : ScriptableObject
{
    // TEMPORARY / PLAYTEST-BOUND / NOT BALANCE-FROZEN.
    // These Foundation angular values exist only to exercise the frozen
    // distance-and-angle geometry. They are not final weapon balance.

    [SerializeField]
    private float horizontalHalfAngleDegrees = 1.5f;

    [SerializeField]
    private float verticalHalfAngleDegrees = 0.75f;

    [SerializeField]
    private float foundationSpreadScale = 1f;


    public float HorizontalHalfAngleDegrees =>
        horizontalHalfAngleDegrees;

    public float VerticalHalfAngleDegrees =>
        verticalHalfAngleDegrees;

    public float FoundationSpreadScale => foundationSpreadScale;
}
