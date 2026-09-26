using UnityEngine;

[AddComponentMenu("Age of Sail/Combat/Ship Sinking Presentation")]
[DisallowMultipleComponent]
public sealed class ShipSinkingPresentation : MonoBehaviour
{
    [SerializeField]
    private ShipSinkingPresentationProfile presentationProfile;

    private ShipIntegrity shipIntegrity;

    private Transform visualRoot;

    private bool dependenciesResolved;

    private bool hasStarted;

    private bool isComplete;

    private float elapsedSeconds;

    private float progress;

    private Vector3 initialVisualLocalPosition;

    private Quaternion initialVisualLocalRotation;

    private float sinkDurationSeconds;

    private float sinkDepthMeters;

    private float optionalRollDegrees;


    public ShipSinkingPresentationProfile PresentationProfile =>
        presentationProfile;

    public Transform PresentationTransform => visualRoot;

    public bool HasStarted => hasStarted;

    public bool IsComplete => isComplete;

    public float Progress => progress;


    private void Awake()
    {
        dependenciesResolved = TryResolveDependencies();
    }


    private void Update()
    {
        AdvancePresentation(Time.deltaTime);
    }


    private void AdvancePresentation(float deltaTimeSeconds)
    {
        if (isComplete)
        {
            return;
        }

        if (!dependenciesResolved)
        {
            dependenciesResolved = TryResolveDependencies();

            if (!dependenciesResolved)
            {
                return;
            }
        }

        if (!hasStarted)
        {
            if (!shipIntegrity.IsInitialized
                || !shipIntegrity.IsSinking
                || !TrySnapshotPresentation())
            {
                return;
            }

            hasStarted = true;
            ApplyPresentationPose(0f);
            return;
        }

        if (!IsFinite(deltaTimeSeconds) || deltaTimeSeconds <= 0f)
        {
            return;
        }

        elapsedSeconds += deltaTimeSeconds;
        progress = Mathf.Clamp01(elapsedSeconds / sinkDurationSeconds);
        ApplyPresentationPose(progress);

        if (progress >= 1f)
        {
            isComplete = true;
        }
    }


    private bool TryResolveDependencies()
    {
        shipIntegrity = GetComponent<ShipIntegrity>();
        ShipArtDefinition artDefinition = GetComponent<ShipArtDefinition>();
        visualRoot = artDefinition != null
            ? artDefinition.VisualRoot
            : null;

        return shipIntegrity != null
            && visualRoot != null
            && visualRoot != transform
            && visualRoot.IsChildOf(transform);
    }


    private bool TrySnapshotPresentation()
    {
        if (!HasValidProfile(presentationProfile))
        {
            return false;
        }

        initialVisualLocalPosition = visualRoot.localPosition;
        initialVisualLocalRotation = visualRoot.localRotation;
        sinkDurationSeconds = presentationProfile.SinkDurationSeconds;
        sinkDepthMeters = presentationProfile.SinkDepthMeters;
        optionalRollDegrees = presentationProfile.OptionalRollDegrees;
        elapsedSeconds = 0f;
        progress = 0f;
        return true;
    }


    private void ApplyPresentationPose(float normalizedProgress)
    {
        float clampedProgress = Mathf.Clamp01(normalizedProgress);
        visualRoot.localPosition = initialVisualLocalPosition
            + Vector3.up * (-sinkDepthMeters * clampedProgress);
        visualRoot.localRotation = initialVisualLocalRotation
            * Quaternion.AngleAxis(
                optionalRollDegrees * clampedProgress,
                Vector3.forward
            );
    }


    private static bool HasValidProfile(
        ShipSinkingPresentationProfile profile
    )
    {
        return profile != null
            && IsFinite(profile.SinkDurationSeconds)
            && profile.SinkDurationSeconds > 0f
            && IsFinite(profile.SinkDepthMeters)
            && profile.SinkDepthMeters > 0f
            && IsFinite(profile.OptionalRollDegrees);
    }


    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
