using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

// One shared, dormant-until-subscribed history of this Root's actual Movement.
[DisallowMultipleComponent]
public sealed class ShipFollowTrailRecorder : MonoBehaviour
{
    [Header("Experimental Follow Sampling")]
    [SerializeField, Min(0.01f)]
    private float sampleDistanceThreshold = 4f;
    [SerializeField, Min(0.01f)]
    private float sampleHeadingThreshold = 2.5f;
    [SerializeField, Min(0.01f)]
    private float turnHeadingThreshold = 0.25f;

    [Header("Experimental Follow History")]
    [SerializeField, Min(0f)]
    private float trimSafetyMargin = 25f;

    private readonly List<ShipFollowTrailSample> samples = new();
    private readonly List<ShipFollowTurnEvent> turnEvents = new();
    private readonly Dictionary<ShipFollowController, Progress> followers = new();
    private readonly List<ShipFollowController> unavailableFollowers = new();
    private ReadOnlyCollection<ShipFollowTrailSample> samplesView;
    private ReadOnlyCollection<ShipFollowTurnEvent> turnsView;
    private ShipFollowTrailSample headSample;
    private int activeTurnIndex = -1;
    private int nextTurnSequence;

    private readonly struct Progress
    {
        public readonly double Start;
        public readonly double Consumed;
        public Progress(double start, double consumed)
        {
            Start = start;
            Consumed = consumed;
        }
    }

    public bool IsRecording => followers.Count > 0;
    public int SubscriberCount => followers.Count;
    public int SampleCount => samples.Count;
    public double HeadDistance => headSample.CumulativeDistance;
    public ShipFollowTrailSample HeadSample => headSample;
    internal int SubscriptionTurnSequence => activeTurnIndex >= 0
        ? turnEvents[activeTurnIndex].Sequence : nextTurnSequence;
    public IReadOnlyList<ShipFollowTrailSample> Samples
        => samplesView ??= samples.AsReadOnly();
    public IReadOnlyList<ShipFollowTurnEvent> TurnEvents
        => turnsView ??= turnEvents.AsReadOnly();

    internal bool TrySubscribe(ShipFollowController follower, out double startDistance)
    {
        startDistance = 0d;
        if (follower == null || !follower.IsMovementEntityValid()
            || !TryReadPose(Time.timeAsDouble, out ShipFollowTrailSample pose))
        {
            return false;
        }

        if (followers.TryGetValue(follower, out Progress existing))
        {
            startDistance = existing.Start;
            return true;
        }

        if (IsRecording)
        {
            ObservePose(pose);
        }
        else
        {
            headSample = pose;
            samples.Add(pose);
        }

        startDistance = HeadDistance;
        followers.Add(follower, new Progress(startDistance, startDistance));
        return true;
    }

    internal bool CanSubscribe(ShipFollowController follower)
    {
        return follower != null && follower.IsMovementEntityValid()
            && TryReadPose(Time.timeAsDouble, out _);
    }

    internal void Unsubscribe(ShipFollowController follower)
    {
        followers.Remove(follower);
        if (!IsRecording)
        {
            ResetHistory();
        }
        else
        {
            TrimHistory();
        }
    }

    public bool TryGetFollowerProgress(ShipFollowController follower,
        out double startDistance, out double consumedDistance)
    {
        startDistance = 0d;
        consumedDistance = 0d;
        if (follower == null || !followers.TryGetValue(follower, out Progress progress))
        {
            return false;
        }

        startDistance = progress.Start;
        consumedDistance = progress.Consumed;
        return true;
    }

    internal bool TryReportProgress(ShipFollowController follower, double distance)
    {
        if (!IsFinite(distance) || follower == null
            || !followers.TryGetValue(follower, out Progress progress)
            || distance < progress.Consumed || distance > HeadDistance)
        {
            return false;
        }

        followers[follower] = new Progress(progress.Start, distance);
        TrimHistory();
        return true;
    }

    // Reads actual pose/ground speed; explicit timestamp makes EditMode sampling deterministic.
    public bool CaptureCurrentPose(double timestamp)
    {
        if (!IsRecording)
        {
            return false;
        }

        if (!TryReadPose(timestamp, out ShipFollowTrailSample pose))
        {
            if (!IsLeaderAvailable())
            {
                StopAndNotifyFollowers();
            }

            return false;
        }

        PruneUnavailableFollowers();
        if (!IsRecording)
        {
            return false;
        }

        ObservePose(pose);
        TrimHistory();
        return true;
    }

    public bool TryGetSampleAtDistance(double distance, out ShipFollowTrailSample sample)
    {
        sample = default;
        if (!IsRecording || !IsFinite(distance) || samples.Count == 0
            || distance < samples[0].CumulativeDistance || distance > HeadDistance)
        {
            return false;
        }

        // Upper bound selects the latest heading when several samples share one distance.
        int low = 0;
        int high = samples.Count;
        while (low < high)
        {
            int middle = low + (high - low) / 2;
            if (samples[middle].CumulativeDistance <= distance)
                low = middle + 1;
            else
                high = middle;
        }

        ShipFollowTrailSample before = samples[low - 1];
        ShipFollowTrailSample after = low < samples.Count ? samples[low] : headSample;
        double span = after.CumulativeDistance - before.CumulativeDistance;
        if (span <= 0d)
        {
            sample = after;
            return true;
        }

        float fraction = (float)((distance - before.CumulativeDistance) / span);
        sample = new ShipFollowTrailSample(
            Vector3.Lerp(before.WorldPosition, after.WorldPosition, fraction),
            before.WorldHeading + Mathf.DeltaAngle(before.WorldHeading,
                after.WorldHeading) * fraction,
            distance,
            Mathf.Lerp(before.CourseSpeed, after.CourseSpeed, fraction),
            before.Timestamp + (after.Timestamp - before.Timestamp) * fraction);
        return true;
    }

    private bool TryReadPose(double timestamp, out ShipFollowTrailSample pose)
    {
        pose = default;
        if (!IsFinite(timestamp) || !IsLeaderAvailable())
        {
            return false;
        }

        Vector3 position = transform.position;
        float heading = transform.eulerAngles.y;
        float speed = GetComponent<ShipSailingSpeed>().CourseSpeed;
        if (!IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z)
            || !IsFinite(heading) || !IsFinite(speed))
        {
            return false;
        }

        pose = new ShipFollowTrailSample(position, heading, 0d, speed, timestamp);
        return true;
    }

    private bool IsLeaderAvailable()
    {
        ShipFollowController leader = GetComponent<ShipFollowController>();
        return isActiveAndEnabled && leader != null && leader.IsMovementEntityValid();
    }

    private void ObservePose(ShipFollowTrailSample pose)
    {
        double deltaX = (double)pose.WorldPosition.x - headSample.WorldPosition.x;
        double deltaZ = (double)pose.WorldPosition.z - headSample.WorldPosition.z;
        double distance = HeadDistance + Math.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
        headSample = new ShipFollowTrailSample(pose.WorldPosition, pose.WorldHeading,
            distance, pose.CourseSpeed, Math.Max(headSample.Timestamp, pose.Timestamp));
        ShipFollowTrailSample previous = samples[samples.Count - 1];
        if (distance - previous.CumulativeDistance < Mathf.Max(0.01f, sampleDistanceThreshold)
            && Mathf.Abs(Mathf.DeltaAngle(previous.WorldHeading, headSample.WorldHeading))
                < Mathf.Max(0.01f, sampleHeadingThreshold))
        {
            return;
        }

        samples.Add(headSample);
        DeriveTurn(previous, headSample);
    }

    private void DeriveTurn(ShipFollowTrailSample previous, ShipFollowTrailSample current)
    {
        float delta = Mathf.DeltaAngle(previous.WorldHeading, current.WorldHeading);
        if (Mathf.Abs(delta) < Mathf.Max(0.01f, turnHeadingThreshold))
        {
            CloseActiveTurn();
            return;
        }

        TurnDirection direction = delta > 0f
            ? TurnDirection.Clockwise : TurnDirection.CounterClockwise;
        if (activeTurnIndex >= 0 && turnEvents[activeTurnIndex].Direction != direction)
        {
            CloseActiveTurn();
        }

        if (activeTurnIndex < 0)
        {
            activeTurnIndex = turnEvents.Count;
            turnEvents.Add(new ShipFollowTurnEvent(previous.CumulativeDistance,
                current.CumulativeDistance, previous.WorldHeading, current.WorldHeading,
                delta, direction, false, nextTurnSequence++));
        }
        else
        {
            ShipFollowTurnEvent turn = turnEvents[activeTurnIndex];
            turnEvents[activeTurnIndex] = new ShipFollowTurnEvent(turn.StartDistance,
                current.CumulativeDistance, turn.StartingHeading, current.WorldHeading,
                turn.AccumulatedHeadingDelta + delta, turn.Direction, false, turn.Sequence);
        }
    }

    private void CloseActiveTurn()
    {
        if (activeTurnIndex < 0) return;
        ShipFollowTurnEvent turn = turnEvents[activeTurnIndex];
        turnEvents[activeTurnIndex] = new ShipFollowTurnEvent(turn.StartDistance,
            turn.EndDistance, turn.StartingHeading, turn.EndingHeading,
            turn.AccumulatedHeadingDelta, turn.Direction, true, turn.Sequence);
        activeTurnIndex = -1;
    }

    private void TrimHistory()
    {
        double minimum = double.PositiveInfinity;
        foreach (Progress progress in followers.Values)
        {
            minimum = Math.Min(minimum, progress.Consumed);
        }

        double cutoff = minimum - Mathf.Max(0f, trimSafetyMargin);
        // Keep the entire turn boundary if it is active or intersects retained history.
        foreach (ShipFollowTurnEvent turn in turnEvents)
        {
            if (!turn.IsComplete || turn.EndDistance >= cutoff)
            {
                cutoff = Math.Min(cutoff, turn.StartDistance);
            }
        }

        int removeCount = 0;
        while (removeCount + 1 < samples.Count
            && samples[removeCount + 1].CumulativeDistance < cutoff)
        {
            removeCount++;
        }

        if (removeCount == 0) return;
        samples.RemoveRange(0, removeCount); // Preserve one interpolation anchor behind cutoff.
        int oldTurns = 0;
        while (oldTurns < turnEvents.Count && turnEvents[oldTurns].IsComplete
            && turnEvents[oldTurns].EndDistance < samples[0].CumulativeDistance)
        {
            oldTurns++;
        }

        if (oldTurns > 0)
        {
            turnEvents.RemoveRange(0, oldTurns);
            if (activeTurnIndex >= 0) activeTurnIndex -= oldTurns;
        }
    }

    private void PruneUnavailableFollowers()
    {
        unavailableFollowers.Clear();
        foreach (ShipFollowController follower in followers.Keys)
        {
            if (follower == null || !follower.IsMovementEntityValid()
                || !follower.IsFollowing || follower.TargetTrailRecorder != this)
            {
                unavailableFollowers.Add(follower);
            }
        }

        foreach (ShipFollowController follower in unavailableFollowers)
        {
            followers.Remove(follower);
            if (follower != null) follower.RefreshFollowRelationship();
        }

        unavailableFollowers.Clear();
        if (!IsRecording) ResetHistory();
    }

    private void StopAndNotifyFollowers()
    {
        unavailableFollowers.Clear();
        unavailableFollowers.AddRange(followers.Keys);
        followers.Clear();
        ResetHistory();
        foreach (ShipFollowController follower in unavailableFollowers)
        {
            if (follower != null) follower.OnTrailRecorderUnavailable(this);
        }

        unavailableFollowers.Clear();
    }

    private void ResetHistory()
    {
        samples.Clear();
        turnEvents.Clear();
        headSample = default;
        activeTurnIndex = -1;
        nextTurnSequence = 0;
    }

    private static bool IsFinite(double value)
        => !double.IsNaN(value) && !double.IsInfinity(value);

    private void LateUpdate() => CaptureCurrentPose(Time.timeAsDouble);
    private void OnDisable() => StopAndNotifyFollowers();
    private void OnDestroy() => StopAndNotifyFollowers();
}
