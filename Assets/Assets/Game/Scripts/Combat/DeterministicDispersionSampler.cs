using UnityEngine;

public struct DeterministicRandom32
{
    private const uint ZeroSeedState = 0x6D2B79F5u;

    private uint state;


    public DeterministicRandom32(uint seed)
    {
        state = seed == 0u ? ZeroSeedState : seed;
    }


    public uint State => state;


    public uint NextUInt()
    {
        // xorshift32: a small, explicitly versioned deterministic generator.
        // Zero is remapped on construction and first use because it is an
        // absorbing state; this also makes a default-constructed value safe.
        uint value = state == 0u ? ZeroSeedState : state;
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        state = value;
        return value;
    }


    public float NextUnitFloat()
    {
        // The upper 24 bits map exactly into [0, 1) without global RNG state.
        return (NextUInt() >> 8) * (1f / 16777216f);
    }
}

public static class DeterministicDispersionSampler
{
    public static bool TryMapUnitSquareSample(
        DispersionEllipse ellipse,
        float u,
        float v,
        out Vector3 samplePointWorld
    )
    {
        samplePointWorld = default;

        if (!IsUnitSample(u)
            || !IsUnitSample(v)
            || !IsValidEllipse(ellipse))
        {
            return false;
        }

        float radius = Mathf.Sqrt(u);
        float thetaRadians = 2f * Mathf.PI * v;
        float horizontalMeters =
            ellipse.HorizontalSemiAxisMeters
            * radius
            * Mathf.Cos(thetaRadians);
        float verticalMeters =
            ellipse.VerticalSemiAxisMeters
            * radius
            * Mathf.Sin(thetaRadians);

        samplePointWorld = ellipse.Plane.CenterWorld
            + ellipse.Plane.HorizontalAxisWorld * horizontalMeters
            + ellipse.Plane.VerticalAxisWorld * verticalMeters;
        return IsFinite(samplePointWorld);
    }


    public static bool TrySample(
        ref DeterministicRandom32 random,
        DispersionEllipse ellipse,
        out Vector3 samplePointWorld
    )
    {
        float u = random.NextUnitFloat();
        float v = random.NextUnitFloat();
        return TryMapUnitSquareSample(
            ellipse,
            u,
            v,
            out samplePointWorld
        );
    }


    private static bool IsValidEllipse(DispersionEllipse ellipse)
    {
        return IsFinite(ellipse.Plane.CenterWorld)
            && IsFinite(ellipse.Plane.HorizontalAxisWorld)
            && IsFinite(ellipse.Plane.VerticalAxisWorld)
            && IsFinite(ellipse.HorizontalSemiAxisMeters)
            && IsFinite(ellipse.VerticalSemiAxisMeters)
            && ellipse.HorizontalSemiAxisMeters > 0f
            && ellipse.VerticalSemiAxisMeters > 0f;
    }


    private static bool IsUnitSample(float value)
    {
        return IsFinite(value) && value >= 0f && value < 1f;
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
