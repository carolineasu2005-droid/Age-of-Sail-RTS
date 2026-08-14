using UnityEngine;

[CreateAssetMenu(
    fileName = "SailPolarProfile",
    menuName = "AgeOfSailRTS/Sailing/Sail Polar Profile"
)]
public class SailPolarProfile : ScriptableObject
{
    [Header("Polar Settings")]

    [Tooltip("Minimum relative wind angle that can generate sustained sailing power.")]
    [Range(0f, 90f)]
    public float noGoAngle = 65f;

    [Tooltip("Sail efficiency as a function of absolute relative wind angle from 0 to 180 degrees.")]
    public AnimationCurve polarCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(45f, 0f),
        new Keyframe(60f, 0f),
        new Keyframe(65f, 0f),
        new Keyframe(67.5f, 0.35f),
        new Keyframe(75f, 0.50f),
        new Keyframe(90f, 0.80f),
        new Keyframe(120f, 1.00f),
        new Keyframe(150f, 0.90f),
        new Keyframe(180f, 0.75f)
    );

    public float Evaluate(float absoluteRelativeWindAngle)
    {
        float angle = Mathf.Clamp(absoluteRelativeWindAngle, 0f, 180f);

        if (angle <= noGoAngle)
        {
            return 0f;
        }

        return Mathf.Clamp01(polarCurve.Evaluate(angle));
    }
}