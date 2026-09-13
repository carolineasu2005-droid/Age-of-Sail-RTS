using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatVFXPlaceholderReceiver
    : MonoBehaviour,
        ICombatVFXEventReceiver
{
    private const float PlaceholderLifetimeSeconds = 2f;
    private const float MinimumDirectionSqrMagnitude = 0.000001f;

    [Header("Placeholder VFX")]

    [SerializeField]
    private bool visualSpawningEnabled = true;


    public bool VisualSpawningEnabled
    {
        get => visualSpawningEnabled;
        set => visualSpawningEnabled = value;
    }


    public void OnMuzzleFire(CombatMuzzleFireEvent eventData)
    {
        SpawnPlaceholder(
            "Combat VFX Placeholder - Muzzle Fire",
            PrimitiveType.Cube,
            eventData.PositionWorld,
            RotationFromForward(eventData.DirectionWorld),
            new Vector3(0.2f, 0.2f, 0.8f)
        );
    }


    public void OnWaterImpact(CombatWaterImpactEvent eventData)
    {
        SpawnPlaceholder(
            "Combat VFX Placeholder - Water Impact",
            PrimitiveType.Cylinder,
            eventData.PositionWorld,
            RotationFromUp(eventData.NormalWorld),
            new Vector3(0.7f, 0.15f, 0.7f)
        );
    }


    public void OnHullImpact(CombatHullImpactEvent eventData)
    {
        SpawnPlaceholder(
            "Combat VFX Placeholder - Hull Impact",
            PrimitiveType.Cube,
            eventData.PositionWorld,
            RotationFromUp(eventData.NormalWorld),
            new Vector3(0.35f, 0.15f, 0.35f)
        );
    }


    private void SpawnPlaceholder(
        string placeholderName,
        PrimitiveType primitiveType,
        Vector3 positionWorld,
        Quaternion rotationWorld,
        Vector3 scaleMeters
    )
    {
        if (!visualSpawningEnabled)
        {
            return;
        }

        GameObject placeholder = GameObject.CreatePrimitive(primitiveType);
        placeholder.name = placeholderName;
        placeholder.transform.SetPositionAndRotation(
            positionWorld,
            rotationWorld
        );
        placeholder.transform.localScale = scaleMeters;

        Collider collider = placeholder.GetComponent<Collider>();

        if (collider != null)
        {
            collider.enabled = false;
            Destroy(collider);
        }

        Destroy(placeholder, PlaceholderLifetimeSeconds);
    }


    private static Quaternion RotationFromForward(Vector3 directionWorld)
    {
        if (directionWorld.sqrMagnitude <= MinimumDirectionSqrMagnitude)
        {
            return Quaternion.identity;
        }

        return Quaternion.FromToRotation(
            Vector3.forward,
            directionWorld.normalized
        );
    }


    private static Quaternion RotationFromUp(Vector3 normalWorld)
    {
        if (normalWorld.sqrMagnitude <= MinimumDirectionSqrMagnitude)
        {
            return Quaternion.identity;
        }

        return Quaternion.FromToRotation(Vector3.up, normalWorld.normalized);
    }
}
