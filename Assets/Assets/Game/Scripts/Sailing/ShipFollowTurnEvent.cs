public readonly struct ShipFollowTurnEvent
{
    public int Sequence { get; }
    public double StartDistance { get; }
    public double EndDistance { get; }
    public float StartingHeading { get; }
    public float EndingHeading { get; }
    public float AccumulatedHeadingDelta { get; }
    public TurnDirection Direction { get; }
    public bool IsComplete { get; }

    internal ShipFollowTurnEvent(double startDistance, double endDistance,
        float startingHeading, float endingHeading, float accumulatedDelta,
        TurnDirection direction, bool isComplete, int sequence)
    {
        Sequence = sequence;
        StartDistance = startDistance;
        EndDistance = endDistance;
        StartingHeading = startingHeading;
        EndingHeading = endingHeading;
        AccumulatedHeadingDelta = accumulatedDelta;
        Direction = direction;
        IsComplete = isComplete;
    }
}
