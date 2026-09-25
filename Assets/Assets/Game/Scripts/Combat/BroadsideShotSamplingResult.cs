using System;
using System.Collections.Generic;

public readonly struct BroadsideShotSamplingResult
{
    private static readonly IReadOnlyList<ShotSample> EmptySamples =
        Array.AsReadOnly(Array.Empty<ShotSample>());

    private readonly IReadOnlyList<ShotSample> shotSamples;


    internal BroadsideShotSamplingResult(
        CombatSide side,
        uint broadsideSeed,
        FireAimBasis aimBasis,
        DispersionEllipse dispersionEllipse,
        IReadOnlyList<ShotSample> samples
    )
    {
        Side = side;
        BroadsideSeed = broadsideSeed;
        AimBasis = aimBasis;
        DispersionEllipse = dispersionEllipse;

        ShotSample[] copy = new ShotSample[samples.Count];

        for (int index = 0; index < samples.Count; index++)
        {
            copy[index] = samples[index];
        }

        shotSamples = Array.AsReadOnly(copy);
    }


    public CombatSide Side { get; }

    public uint BroadsideSeed { get; }

    public FireAimBasis AimBasis { get; }

    public DispersionEllipse DispersionEllipse { get; }

    public IReadOnlyList<ShotSample> ShotSamples =>
        shotSamples ?? EmptySamples;

    public int Count => ShotSamples.Count;
}
