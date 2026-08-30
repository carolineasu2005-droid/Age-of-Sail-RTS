using UnityEngine;

[CreateAssetMenu(
    fileName = "SailPolarProfile",
    menuName = "AgeOfSailRTS/Sailing/Sail Polar Profile"
)]
public class SailPolarProfile : ScriptableObject
{
    [Header("Polar Settings")]

    [Tooltip("Maximum relative wind angle for the severe headwind maneuver zone.")]
    [Range(0f, 90f)]
    public float noGoAngle = 45f;

    [Tooltip("Sail efficiency as a function of absolute relative wind angle from 0 to 180 degrees.")]
    public AnimationCurve polarCurve = new AnimationCurve(
        new Keyframe(0f, 0.25f, 0.0006666667f, 0.0006666667f),
        new Keyframe(30f, 0.27f, 0.0006666667f, 0.002f),
        new Keyframe(45f, 0.30f, 0.002f, 0.03f),
        new Keyframe(50f, 0.45f, 0.03f, 0.01f),
        new Keyframe(60f, 0.55f, 0.01f, 0.0066666667f),
        new Keyframe(75f, 0.65f, 0.0066666667f, 0.01f),
        new Keyframe(90f, 0.80f, 0.01f, 0.0066666667f),
        new Keyframe(120f, 1.00f, 0.0066666667f, -0.0033333333f),
        new Keyframe(150f, 0.90f, -0.0033333333f, -0.005f),
        new Keyframe(180f, 0.75f, -0.005f, -0.005f)
    );

    public float Evaluate(float absoluteRelativeWindAngle)
    {
        float angle = Mathf.Clamp(absoluteRelativeWindAngle, 0f, 180f);

        return Mathf.Clamp01(polarCurve.Evaluate(angle));
    }
}
