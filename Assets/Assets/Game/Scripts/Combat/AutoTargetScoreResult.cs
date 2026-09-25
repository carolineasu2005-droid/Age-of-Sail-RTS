public readonly struct AutoTargetScoreResult
{
    internal AutoTargetScoreResult(
        float exposureNormalized,
        float rangeQualityNormalized,
        float visibilityQualityNormalized
    )
    {
        Selectable = true;
        ExposureNormalized = exposureNormalized;
        RangeQualityNormalized = rangeQualityNormalized;
        VisibilityQualityNormalized = visibilityQualityNormalized;
        FinalScore = exposureNormalized
            * rangeQualityNormalized
            * visibilityQualityNormalized;
    }


    public bool Selectable { get; }

    public float ExposureNormalized { get; }

    public float RangeQualityNormalized { get; }

    public float VisibilityQualityNormalized { get; }

    public float FinalScore { get; }
}
