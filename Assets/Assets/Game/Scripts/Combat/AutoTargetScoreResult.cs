public readonly struct AutoTargetScoreResult
{
    internal AutoTargetScoreResult(
        float exposureNormalized,
        float rangeQualityNormalized
    )
    {
        Selectable = true;
        ExposureNormalized = exposureNormalized;
        RangeQualityNormalized = rangeQualityNormalized;
        FinalScore = exposureNormalized * rangeQualityNormalized;
    }


    public bool Selectable { get; }

    public float ExposureNormalized { get; }

    public float RangeQualityNormalized { get; }

    public float FinalScore { get; }
}
