using UnityEngine;

public readonly struct ExposureRect
{
    public ExposureRect(
        Vector3 centerWorld,
        Vector3 planeNormalWorld,
        Vector3 horizontalAxisWorld,
        Vector3 verticalAxisWorld,
        float widthMeters,
        float heightMeters
    )
    {
        CenterWorld = centerWorld;
        PlaneNormalWorld = planeNormalWorld;
        HorizontalAxisWorld = horizontalAxisWorld;
        VerticalAxisWorld = verticalAxisWorld;
        WidthMeters = widthMeters;
        HeightMeters = heightMeters;
    }


    public Vector3 CenterWorld { get; }

    public Vector3 PlaneNormalWorld { get; }

    public Vector3 HorizontalAxisWorld { get; }

    public Vector3 VerticalAxisWorld { get; }

    public float WidthMeters { get; }

    public float HeightMeters { get; }

    public float AreaSquareMeters => WidthMeters * HeightMeters;
}
