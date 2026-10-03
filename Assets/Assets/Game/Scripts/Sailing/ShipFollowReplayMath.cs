using System;
using System.Collections.Generic;
using UnityEngine;

public enum ShipFollowSpeedState
{
    CatchUp,
    Closing,
    Following,
    TooCloseHold
}

// Pure geometry/propulsion policy. Configuration belongs to the navigation owner.
public static class ShipFollowReplayMath
{
    private const double DistanceEpsilon = 0.000001d;

    public static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        Vector3 offset = second - first;
        offset.y = 0f;
        return offset.magnitude;
    }

    public static float EvaluateSpeed(double gap, float followerAvailable, float leaderCourseSpeed,
        float tooCloseGap, float followingBandMaximum, float catchUpGap,
        out ShipFollowSpeedState state)
    {
        float available = NonnegativeFinite(followerAvailable);
        float reference = Mathf.Min(available, NonnegativeFinite(leaderCourseSpeed));
        if (double.IsNaN(gap) || gap < tooCloseGap)
        {
            state = ShipFollowSpeedState.TooCloseHold;
            return 0f;
        }
        if (gap <= followingBandMaximum)
        {
            state = ShipFollowSpeedState.Following;
            return reference;
        }
        if (gap < catchUpGap)
        {
            state = ShipFollowSpeedState.Closing;
            float fraction = Mathf.InverseLerp(followingBandMaximum, catchUpGap, (float)gap);
            return Mathf.Lerp(reference, available, fraction);
        }
        state = ShipFollowSpeedState.CatchUp;
        return available;
    }

    public static bool TryProjectForward(IReadOnlyList<ShipFollowTrailSample> samples,
        ShipFollowTrailSample head, Vector3 worldPosition, double minimumDistance,
        double maximumDistance, float tolerance, out double progress)
    {
        progress = minimumDistance;
        if (samples == null || samples.Count == 0 || double.IsNaN(minimumDistance)
            || double.IsNaN(maximumDistance) || maximumDistance < minimumDistance
            || minimumDistance < samples[0].CumulativeDistance
            || minimumDistance > head.CumulativeDistance)
            return false;

        maximumDistance = Math.Min(maximumDistance, head.CumulativeDistance);
        // Earliest qualifying segment wins. Nearby crossings cannot select a later branch.
        for (int i = 0; i < samples.Count; i++)
        {
            ShipFollowTrailSample before = samples[i];
            ShipFollowTrailSample after = i + 1 < samples.Count ? samples[i + 1] : head;
            if (after.CumulativeDistance < minimumDistance) continue;
            if (before.CumulativeDistance > maximumDistance) break;
            double lower = Math.Max(minimumDistance, before.CumulativeDistance);
            double upper = Math.Min(maximumDistance, after.CumulativeDistance);
            if (lower > upper) continue;
            double span = after.CumulativeDistance - before.CumulativeDistance;
            Vector3 start = span > DistanceEpsilon
                ? Vector3.Lerp(before.WorldPosition, after.WorldPosition,
                    (float)((lower - before.CumulativeDistance) / span)) : before.WorldPosition;
            Vector3 end = span > DistanceEpsilon
                ? Vector3.Lerp(before.WorldPosition, after.WorldPosition,
                    (float)((upper - before.CumulativeDistance) / span)) : after.WorldPosition;
            start.y = end.y = worldPosition.y;
            Vector3 segment = end - start;
            float fraction = segment.sqrMagnitude > DistanceEpsilon
                ? Mathf.Clamp01(Vector3.Dot(worldPosition - start, segment) / segment.sqrMagnitude)
                : 0f;
            if (HorizontalDistance(worldPosition, start + segment * fraction) > tolerance) continue;
            progress = Math.Max(minimumDistance, lower + (upper - lower) * fraction);
            return true;
        }
        return false;
    }

    public static double ClampAtCurvature(IReadOnlyList<ShipFollowTrailSample> samples,
        ShipFollowTrailSample head, double cursor, double maximumDistance, float headingThreshold)
    {
        bool hasPreviousBearing = false;
        float previousBearing = 0f;
        for (int i = 0; i < samples.Count; i++)
        {
            ShipFollowTrailSample before = samples[i];
            ShipFollowTrailSample after = i + 1 < samples.Count ? samples[i + 1] : head;
            if (before.CumulativeDistance > maximumDistance) break;
            Vector3 offset = after.WorldPosition - before.WorldPosition;
            offset.y = 0f;
            if (offset.sqrMagnitude <= DistanceEpsilon) continue;
            float bearing = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            if (hasPreviousBearing && before.CumulativeDistance > cursor + DistanceEpsilon
                && Mathf.Abs(Mathf.DeltaAngle(previousBearing, bearing)) >= headingThreshold)
                return Math.Min(maximumDistance, before.CumulativeDistance);
            previousBearing = bearing;
            hasPreviousBearing = true;
        }
        return maximumDistance;
    }

    private static float NonnegativeFinite(float value)
        => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
}
