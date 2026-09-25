using System;
using System.Collections.Generic;

[Flags]
public enum BroadsideFireExecutionFailure
{
    None = 0,
    InvalidAimBasis = 1 << 0,
    MissingDependencies = 1 << 1,
    ShotSamplingFailed = 1 << 2,
    InvalidProjectilePrefab = 1 << 3,
    BroadsideCommitFailed = 1 << 4,
    ProjectileSpawnFailed = 1 << 5
}

public readonly struct BroadsideFireExecutionResult
{
    internal BroadsideFireExecutionResult(
        bool accepted,
        CombatSide? side,
        uint broadsideSeed,
        FireAimBasis aimBasis,
        BroadsideShotSamplingResult samplingResult,
        int spawnedProjectileCount,
        BroadsideShotSamplingFailure samplingFailure,
        BroadsideFireExecutionFailure failureReasons
    )
    {
        Accepted = accepted;
        Side = side;
        BroadsideSeed = broadsideSeed;
        AimBasis = aimBasis;
        SamplingResult = samplingResult;
        SpawnedProjectileCount = spawnedProjectileCount;
        SamplingFailure = samplingFailure;
        FailureReasons = failureReasons;
    }


    public bool Accepted { get; }

    public CombatSide? Side { get; }

    public uint BroadsideSeed { get; }

    public FireAimBasis AimBasis { get; }

    public BroadsideShotSamplingResult SamplingResult { get; }

    public IReadOnlyList<ShotSample> ShotSamples =>
        SamplingResult.ShotSamples;

    public int ShotCount => ShotSamples.Count;

    public int SpawnedProjectileCount { get; }

    public BroadsideShotSamplingFailure SamplingFailure { get; }

    public BroadsideFireExecutionFailure FailureReasons { get; }
}
