using UnityEngine;

public static class WindBeatingNavigationMath
{
    public readonly struct CloseHauledCandidates
    {
        public float PositiveHeading { get; }

        public float NegativeHeading { get; }


        public CloseHauledCandidates(
            float positiveHeading,
            float negativeHeading
        )
        {
            PositiveHeading = positiveHeading;
            NegativeHeading = negativeHeading;
        }
    }


    public readonly struct RouteReference
    {
        public Vector3 Direction { get; }

        public Vector3 Right { get; }


        public RouteReference(Vector3 direction, Vector3 right)
        {
            Direction = direction;
            Right = right;
        }
    }


    public static float GetHorizontalBearing(
        Vector3 fromPosition,
        Vector3 toPosition,
        float fallbackHeading
    )
    {
        Vector3 horizontalOffset = toPosition - fromPosition;
        horizontalOffset.y = 0f;

        return horizontalOffset.magnitude > 0.0001f
            ? GetHeading(horizontalOffset)
            : fallbackHeading;
    }


    public static float GetAbsoluteBearingRelativeToWind(
        float targetBearing,
        float windFromHeading
    )
    {
        return Mathf.Abs(Mathf.DeltaAngle(
            targetBearing,
            windFromHeading
        ));
    }


    public static bool ShouldEnterBeating(
        float targetBearingRelativeToWind,
        float directSailingThreshold
    )
    {
        return targetBearingRelativeToWind < directSailingThreshold;
    }


    public static bool ShouldResumeDirect(
        float targetBearingRelativeToWind,
        float directResumeThreshold
    )
    {
        return targetBearingRelativeToWind >= directResumeThreshold;
    }


    public static CloseHauledCandidates GetCloseHauledCandidates(
        float windFromHeading,
        float closeHauledAngle
    )
    {
        return new CloseHauledCandidates(
            NormalizeHeading(windFromHeading + closeHauledAngle),
            NormalizeHeading(windFromHeading - closeHauledAngle)
        );
    }


    public static float SelectInitialAutoCloseHauledHeading(
        float desiredBearing,
        float currentHeading,
        CloseHauledCandidates candidates
    )
    {
        float positiveAlignment = Mathf.Abs(Mathf.DeltaAngle(
            desiredBearing,
            candidates.PositiveHeading
        ));
        float negativeAlignment = Mathf.Abs(Mathf.DeltaAngle(
            desiredBearing,
            candidates.NegativeHeading
        ));

        if (!Mathf.Approximately(positiveAlignment, negativeAlignment))
        {
            return positiveAlignment < negativeAlignment
                ? candidates.PositiveHeading
                : candidates.NegativeHeading;
        }

        float positiveTurn = Mathf.Abs(Mathf.DeltaAngle(
            currentHeading,
            candidates.PositiveHeading
        ));
        float negativeTurn = Mathf.Abs(Mathf.DeltaAngle(
            currentHeading,
            candidates.NegativeHeading
        ));

        return positiveTurn <= negativeTurn
            ? candidates.PositiveHeading
            : candidates.NegativeHeading;
    }


    public static float SelectCloseHauledHeadingForDirectedArc(
        float currentHeading,
        CloseHauledCandidates candidates,
        TurnDirection direction
    )
    {
        float positiveArc = CalculateDirectedArc(
            currentHeading,
            candidates.PositiveHeading,
            direction
        );
        float negativeArc = CalculateDirectedArc(
            currentHeading,
            candidates.NegativeHeading,
            direction
        );

        return positiveArc <= negativeArc
            ? candidates.PositiveHeading
            : candidates.NegativeHeading;
    }


    public static RouteReference CalculateRouteReference(
        Vector3 routeOrigin,
        Vector3 routeDestination
    )
    {
        Vector3 horizontalRoute = routeDestination - routeOrigin;
        horizontalRoute.y = 0f;
        Vector3 routeDirection = horizontalRoute.sqrMagnitude > 0.0001f
            ? horizontalRoute.normalized
            : Vector3.zero;

        return new RouteReference(
            routeDirection,
            Vector3.Cross(Vector3.up, routeDirection)
        );
    }


    public static float CalculateDynamicCorridorHalfWidth(
        float distanceToDestination,
        float distanceRatio,
        float minimumHalfWidth,
        float maximumHalfWidth
    )
    {
        return Mathf.Clamp(
            distanceToDestination * distanceRatio,
            minimumHalfWidth,
            maximumHalfWidth
        );
    }


    public static float CalculateCrossTrackDistance(
        Vector3 currentPosition,
        Vector3 routeOrigin,
        Vector3 routeRight
    )
    {
        Vector3 horizontalOffset = currentPosition - routeOrigin;
        horizontalOffset.y = 0f;
        return Vector3.Dot(horizontalOffset, routeRight);
    }


    public static bool ShouldSwitchCloseHauledLeg(
        float crossTrackDistance,
        float currentLegCrossTrackSign,
        float corridorHalfWidth,
        float corridorSwitchFactor
    )
    {
        return crossTrackDistance * currentLegCrossTrackSign
            >= corridorHalfWidth * corridorSwitchFactor;
    }


    public static float GetOppositeCloseHauledHeading(
        float currentLegHeading,
        CloseHauledCandidates candidates
    )
    {
        return Mathf.Abs(Mathf.DeltaAngle(
            currentLegHeading,
            candidates.PositiveHeading
        )) <= 0.1f
            ? candidates.NegativeHeading
            : candidates.PositiveHeading;
    }


    public static float CalculateLegCrossTrackSign(
        float legHeading,
        Vector3 routeRight
    )
    {
        return Mathf.Sign(Vector3.Dot(
            HeadingToDirection(legHeading),
            routeRight
        ));
    }


    public static Vector3 GetHeadingDirection(float heading)
    {
        return HeadingToDirection(heading);
    }


    private static float CalculateDirectedArc(
        float fromHeading,
        float toHeading,
        TurnDirection direction
    )
    {
        return direction == TurnDirection.Clockwise
            ? Mathf.Repeat(toHeading - fromHeading, 360f)
            : Mathf.Repeat(fromHeading - toHeading, 360f);
    }


    private static Vector3 HeadingToDirection(float heading)
    {
        return Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
    }


    private static float GetHeading(Vector3 direction)
    {
        return Mathf.Repeat(
            Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg,
            360f
        );
    }


    private static float NormalizeHeading(float heading)
    {
        return Mathf.Repeat(heading, 360f);
    }
}
