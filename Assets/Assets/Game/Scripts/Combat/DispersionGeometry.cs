using UnityEngine;

public readonly struct DispersionPlane
{
    internal DispersionPlane(
        Vector3 centerWorld,
        Vector3 normalWorld,
        Vector3 horizontalAxisWorld,
        Vector3 verticalAxisWorld
    )
    {
        CenterWorld = centerWorld;
        NormalWorld = normalWorld;
        HorizontalAxisWorld = horizontalAxisWorld;
        VerticalAxisWorld = verticalAxisWorld;
    }


    public Vector3 CenterWorld { get; }

    public Vector3 NormalWorld { get; }

    public Vector3 HorizontalAxisWorld { get; }

    public Vector3 VerticalAxisWorld { get; }
}

public readonly struct DispersionRect
{
    internal DispersionRect(
        DispersionPlane plane,
        float widthMeters,
        float heightMeters
    )
    {
        Plane = plane;
        WidthMeters = widthMeters;
        HeightMeters = heightMeters;
    }


    public DispersionPlane Plane { get; }

    public float WidthMeters { get; }

    public float HeightMeters { get; }

    public float HorizontalSemiAxisMeters => WidthMeters * 0.5f;

    public float VerticalSemiAxisMeters => HeightMeters * 0.5f;
}

public readonly struct DispersionEllipse
{
    internal DispersionEllipse(
        DispersionPlane plane,
        float horizontalSemiAxisMeters,
        float verticalSemiAxisMeters
    )
    {
        Plane = plane;
        HorizontalSemiAxisMeters = horizontalSemiAxisMeters;
        VerticalSemiAxisMeters = verticalSemiAxisMeters;
    }


    public DispersionPlane Plane { get; }

    public float HorizontalSemiAxisMeters { get; }

    public float VerticalSemiAxisMeters { get; }
}

public static class DispersionGeometry
{
    private const float MinimumDirectionSqrMagnitude = 0.000001f;
    private const float MaximumHalfAngleDegrees = 90f;


    public static bool TryCalculate(
        FireAimBasis aimBasis,
        DispersionProfile profile,
        out DispersionRect rect,
        out DispersionEllipse ellipse
    )
    {
        rect = default;
        ellipse = default;

        if (profile == null
            || !TryCreatePlane(aimBasis, out DispersionPlane plane)
            || !IsValidHalfAngle(profile.HorizontalHalfAngleDegrees)
            || !IsValidHalfAngle(profile.VerticalHalfAngleDegrees)
            || !IsFinite(profile.FoundationSpreadScale)
            || profile.FoundationSpreadScale <= 0f)
        {
            return false;
        }

        float horizontalSemiAxisMeters =
            aimBasis.AimDistanceMeters
            * Mathf.Tan(
                profile.HorizontalHalfAngleDegrees * Mathf.Deg2Rad
            )
            * profile.FoundationSpreadScale;
        float verticalSemiAxisMeters =
            aimBasis.AimDistanceMeters
            * Mathf.Tan(
                profile.VerticalHalfAngleDegrees * Mathf.Deg2Rad
            )
            * profile.FoundationSpreadScale;

        if (!IsFinite(horizontalSemiAxisMeters)
            || !IsFinite(verticalSemiAxisMeters)
            || horizontalSemiAxisMeters <= 0f
            || verticalSemiAxisMeters <= 0f)
        {
            return false;
        }

        rect = new DispersionRect(
            plane,
            horizontalSemiAxisMeters * 2f,
            verticalSemiAxisMeters * 2f
        );
        ellipse = new DispersionEllipse(
            plane,
            horizontalSemiAxisMeters,
            verticalSemiAxisMeters
        );
        return true;
    }


    private static bool TryCreatePlane(
        FireAimBasis aimBasis,
        out DispersionPlane plane
    )
    {
        plane = default;
        Vector3 normalWorld = aimBasis.AimDirectionWorld;
        normalWorld.y = 0f;

        if (!IsFinite(aimBasis.AimPlaneCenterWorld)
            || !IsFinite(normalWorld)
            || normalWorld.sqrMagnitude <= MinimumDirectionSqrMagnitude
            || !IsFinite(aimBasis.AimDistanceMeters)
            || aimBasis.AimDistanceMeters <= 0f)
        {
            return false;
        }

        normalWorld.Normalize();
        Vector3 verticalAxisWorld = Vector3.up;
        Vector3 horizontalAxisWorld = Vector3.Cross(
            verticalAxisWorld,
            normalWorld
        ).normalized;

        if (!IsFinite(normalWorld)
            || !IsFinite(horizontalAxisWorld))
        {
            return false;
        }

        plane = new DispersionPlane(
            aimBasis.AimPlaneCenterWorld,
            normalWorld,
            horizontalAxisWorld,
            verticalAxisWorld
        );
        return true;
    }


    private static bool IsValidHalfAngle(float degrees)
    {
        return IsFinite(degrees)
            && degrees > 0f
            && degrees < MaximumHalfAngleDegrees;
    }


    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x)
            && IsFinite(value.y)
            && IsFinite(value.z);
    }


    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
