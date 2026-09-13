using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShipExposureReference : MonoBehaviour
{
    private const float MinimumDimensionMeters = 0.0001f;
    private const float MinimumHorizontalDirectionSqrMagnitude = 0.000001f;

    [Header("Target Exposure")]

    [SerializeField]
    private ShipArtDefinition shipArtDefinition;


    public ShipArtDefinition ArtDefinition => shipArtDefinition;


    public bool TryGetReferenceDimensions(out Vector3 dimensionsMeters)
    {
        dimensionsMeters = Vector3.zero;

        if (shipArtDefinition == null
            || !shipArtDefinition.TryGetLength(out float lengthMeters)
            || !shipArtDefinition.TryGetBeam(out float beamMeters)
            || !TryGetHeight(out float heightMeters)
            || !IsValidDimension(lengthMeters)
            || !IsValidDimension(beamMeters)
            || !IsValidDimension(heightMeters))
        {
            return false;
        }

        dimensionsMeters = new Vector3(
            beamMeters,
            heightMeters,
            lengthMeters
        );
        return true;
    }


    public bool TryGetReferenceCenter(out Vector3 centerWorld)
    {
        centerWorld = Vector3.zero;

        if (shipArtDefinition == null
            || shipArtDefinition.CenterReference == null
            || shipArtDefinition.WaterlineReference == null
            || shipArtDefinition.DeckReference == null)
        {
            return false;
        }

        Transform root = shipArtDefinition.transform;
        Vector3 centerLocal = root.InverseTransformPoint(
            shipArtDefinition.CenterReference.position
        );
        float waterlineLocalY = root.InverseTransformPoint(
            shipArtDefinition.WaterlineReference.position
        ).y;
        float deckLocalY = root.InverseTransformPoint(
            shipArtDefinition.DeckReference.position
        ).y;

        centerLocal.y = (waterlineLocalY + deckLocalY) * 0.5f;
        centerWorld = root.TransformPoint(centerLocal);
        return IsFinite(centerWorld);
    }


    public bool TryCalculateExposure(
        Vector3 observerWorldPosition,
        out ExposureRect exposure
    )
    {
        exposure = default;

        if (!IsFinite(observerWorldPosition)
            || !TryGetReferenceDimensions(out Vector3 dimensionsMeters)
            || !TryGetReferenceCenter(out Vector3 centerWorld))
        {
            return false;
        }

        Vector3 planeNormalWorld = centerWorld - observerWorldPosition;
        planeNormalWorld.y = 0f;

        if (!TryNormalizeHorizontal(
            planeNormalWorld,
            out planeNormalWorld
        ))
        {
            return false;
        }

        Vector3 forwardWorld = shipArtDefinition.transform.forward;

        if (!TryNormalizeHorizontal(forwardWorld, out forwardWorld))
        {
            return false;
        }

        Vector3 rightWorld = Vector3.Cross(Vector3.up, forwardWorld);
        float projectedWidth =
            Mathf.Abs(Vector3.Dot(planeNormalWorld, forwardWorld))
                * dimensionsMeters.x
            + Mathf.Abs(Vector3.Dot(planeNormalWorld, rightWorld))
                * dimensionsMeters.z;
        Vector3 horizontalAxisWorld = Vector3.Cross(
            Vector3.up,
            planeNormalWorld
        );

        exposure = new ExposureRect(
            centerWorld,
            planeNormalWorld,
            horizontalAxisWorld,
            Vector3.up,
            projectedWidth,
            dimensionsMeters.y
        );
        return true;
    }


    private bool TryGetHeight(out float heightMeters)
    {
        heightMeters = 0f;

        if (shipArtDefinition.WaterlineReference == null
            || shipArtDefinition.DeckReference == null)
        {
            return false;
        }

        Transform root = shipArtDefinition.transform;
        float waterlineLocalY = root.InverseTransformPoint(
            shipArtDefinition.WaterlineReference.position
        ).y;
        float deckLocalY = root.InverseTransformPoint(
            shipArtDefinition.DeckReference.position
        ).y;

        heightMeters = Mathf.Abs(deckLocalY - waterlineLocalY);
        return true;
    }


    private static bool TryNormalizeHorizontal(
        Vector3 direction,
        out Vector3 normalizedDirection
    )
    {
        normalizedDirection = Vector3.zero;
        direction.y = 0f;

        if (!IsFinite(direction)
            || direction.sqrMagnitude
                <= MinimumHorizontalDirectionSqrMagnitude)
        {
            return false;
        }

        normalizedDirection = direction.normalized;
        return true;
    }


    private static bool IsValidDimension(float value)
    {
        return IsFinite(value) && value > MinimumDimensionMeters;
    }


    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x)
            && IsFinite(value.y)
            && IsFinite(value.z);
    }


    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
