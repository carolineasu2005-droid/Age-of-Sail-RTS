using UnityEngine;

// Experimental Follow history. Values describe observed Movement, never commands.
public readonly struct ShipFollowTrailSample
{
    public Vector3 WorldPosition { get; }
    public float WorldHeading { get; }
    public double CumulativeDistance { get; }
    public float CourseSpeed { get; }
    public double Timestamp { get; }

    internal ShipFollowTrailSample(Vector3 position, float heading,
        double distance, float courseSpeed, double timestamp)
    {
        WorldPosition = position;
        WorldHeading = Mathf.Repeat(heading, 360f);
        CumulativeDistance = distance;
        CourseSpeed = courseSpeed;
        Timestamp = timestamp;
    }
}
