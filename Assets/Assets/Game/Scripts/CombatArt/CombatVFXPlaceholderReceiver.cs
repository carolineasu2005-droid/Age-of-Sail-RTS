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

    [SerializeField]
    private GameObject cannonLingeringSmokePrefab;


    public bool VisualSpawningEnabled
    {
        get => visualSpawningEnabled;
        set => visualSpawningEnabled = value;
    }

    public GameObject CannonLingeringSmokePrefab =>
        cannonLingeringSmokePrefab;


    public void OnMuzzleFire(CombatMuzzleFireEvent eventData)
    {
        if (!visualSpawningEnabled
            || cannonLingeringSmokePrefab == null
            || !IsFinite(eventData.PositionWorld)
            || !TryCreateForwardRotation(
                eventData.DirectionWorld,
                out Quaternion rotationWorld
            )
            || cannonLingeringSmokePrefab
                .GetComponentsInChildren<ParticleSystem>(true)
                .Length == 0)
        {
            return;
        }

        GameObject smoke = Instantiate(
            cannonLingeringSmokePrefab,
            eventData.PositionWorld,
            rotationWorld
        );
        ParticleSystem[] particleSystems = smoke
            .GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem particleSystem in particleSystems)
        {
            particleSystem.Play(true);
        }
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


    private static bool TryCreateForwardRotation(
        Vector3 directionWorld,
        out Quaternion rotationWorld
    )
    {
        rotationWorld = Quaternion.identity;

        if (!IsFinite(directionWorld)
            || directionWorld.sqrMagnitude <= MinimumDirectionSqrMagnitude)
        {
            return false;
        }

        Vector3 forward = directionWorld.normalized;
        Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) < 0.999f
            ? Vector3.up
            : Vector3.forward;
        rotationWorld = Quaternion.LookRotation(forward, up);
        return true;
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


    private static Quaternion RotationFromUp(Vector3 normalWorld)
    {
        if (normalWorld.sqrMagnitude <= MinimumDirectionSqrMagnitude)
        {
            return Quaternion.identity;
        }

        return Quaternion.FromToRotation(
            Vector3.up,
            normalWorld.normalized
        );
    }
}
