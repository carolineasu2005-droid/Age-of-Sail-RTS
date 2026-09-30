using UnityEngine;

// Keeps world-space smoke outside the ship-side patch captured for one shot.
public sealed class CannonSmokeHullPush : MonoBehaviour
{
    [SerializeField] private ParticleSystem smokeParticleSystem;
    [SerializeField] private Transform shipRoot;
    [SerializeField] private Transform muzzleSocket;

    [Header("Hull Push")]
    [SerializeField, Min(0f)] private float baseClearance = 0.25f;
    [SerializeField, Min(0f)] private float clearanceVariation = 0.05f;
    [SerializeField, Min(0f)] private float longitudinalRange = 4f;
    [SerializeField, Min(0f)] private float verticalRange = 3f;
    [SerializeField, Min(0f)] private float maxPenetration = 2f;

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos = true;
    [SerializeField] private bool drawCurrentOutwardDirection = true;

    private ParticleSystem.Particle[] particleBuffer;
    private Vector3 localMuzzlePosition;
    private Vector3 localOutwardDirection;
    private bool hasShotContext;
    private bool warnedNonWorldSpace;

    public void Configure(
        ParticleSystem particleSystemRef,
        Transform shipRootRef,
        Transform muzzleSocketRef)
    {
        if (smokeParticleSystem != particleSystemRef)
        {
            particleBuffer = null;
            warnedNonWorldSpace = false;
        }

        smokeParticleSystem = particleSystemRef;
        shipRoot = shipRootRef;
        muzzleSocket = muzzleSocketRef;
        hasShotContext = false;
    }

    public void BeginShot()
    {
        hasShotContext = false;
        if (smokeParticleSystem == null || shipRoot == null || muzzleSocket == null)
        {
            return;
        }

        Vector3 position = shipRoot.InverseTransformPoint(muzzleSocket.position);
        Vector3 outward = shipRoot.InverseTransformDirection(muzzleSocket.forward);
        if (!IsFinite(position) || !IsFinite(outward)
            || outward.sqrMagnitude <= 0.00000001f)
        {
            return;
        }

        localMuzzlePosition = position;
        localOutwardDirection = outward.normalized;
        hasShotContext = true;
    }

    private void LateUpdate()
    {
        if (!hasShotContext || smokeParticleSystem == null || shipRoot == null
            || muzzleSocket == null)
        {
            return;
        }

        ParticleSystem.MainModule main = smokeParticleSystem.main;
        if (main.simulationSpace != ParticleSystemSimulationSpace.World)
        {
            if (!warnedNonWorldSpace)
            {
                Debug.LogWarning(
                    "CannonSmokeHullPush requires World Space particle simulation; hull push is disabled.",
                    this);
                warnedNonWorldSpace = true;
            }

            return;
        }

        if (!HasValidTuning())
        {
            return;
        }

        int activeCount = smokeParticleSystem.particleCount;
        if (activeCount == 0)
        {
            return;
        }

        Vector3 currentOrigin = shipRoot.TransformPoint(localMuzzlePosition);
        Vector3 currentOutward = shipRoot.TransformDirection(localOutwardDirection);
        if (!IsFinite(currentOrigin) || !IsFinite(currentOutward)
            || currentOutward.sqrMagnitude <= 0.00000001f)
        {
            return;
        }

        currentOutward.Normalize();
        int capacity = Mathf.Max(main.maxParticles, activeCount);
        if (particleBuffer == null || particleBuffer.Length < capacity)
        {
            particleBuffer = new ParticleSystem.Particle[capacity];
        }

        int count = smokeParticleSystem.GetParticles(particleBuffer);
        bool changed = false;
        for (int index = 0; index < count; index++)
        {
            ParticleSystem.Particle particle = particleBuffer[index];
            Vector3 worldPosition = particle.position;
            if (!IsFinite(worldPosition))
            {
                continue;
            }

            Vector3 localPosition = shipRoot.InverseTransformPoint(worldPosition);
            if (!IsFinite(localPosition)
                || Mathf.Abs(localPosition.z - localMuzzlePosition.z)
                    > longitudinalRange
                || Mathf.Abs(localPosition.y - localMuzzlePosition.y)
                    > verticalRange)
            {
                continue;
            }

            float sideDistance = Vector3.Dot(
                worldPosition - currentOrigin, currentOutward);
            float particleClearance = baseClearance + clearanceVariation
                * StableSignedVariation(particle.randomSeed);
            if (sideDistance >= particleClearance
                || sideDistance < -maxPenetration)
            {
                continue;
            }

            float correction = particleClearance - sideDistance;
            particle.position = worldPosition + currentOutward * correction;
            particleBuffer[index] = particle;
            changed = true;
        }

        if (changed)
        {
            smokeParticleSystem.SetParticles(particleBuffer, count);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!drawDebugGizmos || shipRoot == null || !hasShotContext
            || !HasValidTuning())
        {
            return;
        }

        Vector3 currentOrigin = shipRoot.TransformPoint(localMuzzlePosition);
        Vector3 currentOutward = shipRoot.TransformDirection(localOutwardDirection);
        if (!IsFinite(currentOrigin) || !IsFinite(currentOutward)
            || currentOutward.sqrMagnitude <= 0.00000001f)
        {
            return;
        }

        currentOutward.Normalize();
        Vector3 longitudinalHalf = Vector3.ProjectOnPlane(
            shipRoot.TransformVector(Vector3.forward * longitudinalRange),
            currentOutward);
        Vector3 verticalHalf = Vector3.ProjectOnPlane(
            shipRoot.TransformVector(Vector3.up * verticalRange),
            currentOutward);
        if (!IsFinite(longitudinalHalf) || !IsFinite(verticalHalf))
        {
            return;
        }

        Gizmos.color = Color.cyan;
        DrawPatchOutline(currentOrigin, longitudinalHalf, verticalHalf);
        Gizmos.DrawSphere(currentOrigin, 0.1f);

        Vector3 clearanceCenter = currentOrigin + currentOutward * baseClearance;
        Gizmos.color = Color.yellow;
        DrawPatchOutline(clearanceCenter, longitudinalHalf, verticalHalf);

        if (drawCurrentOutwardDirection)
        {
            Gizmos.color = Color.magenta;
            Vector3 arrowTip = currentOrigin + currentOutward
                * Mathf.Max(0.5f, baseClearance + 0.5f);
            Gizmos.DrawLine(currentOrigin, arrowTip);
            Gizmos.DrawSphere(arrowTip, 0.07f);
        }
    }

    private static void DrawPatchOutline(
        Vector3 center, Vector3 longitudinalHalf, Vector3 verticalHalf)
    {
        Vector3 upperFront = center + longitudinalHalf + verticalHalf;
        Vector3 lowerFront = center + longitudinalHalf - verticalHalf;
        Vector3 lowerBack = center - longitudinalHalf - verticalHalf;
        Vector3 upperBack = center - longitudinalHalf + verticalHalf;

        Gizmos.DrawLine(upperFront, lowerFront);
        Gizmos.DrawLine(lowerFront, lowerBack);
        Gizmos.DrawLine(lowerBack, upperBack);
        Gizmos.DrawLine(upperBack, upperFront);
    }
#endif

    private bool HasValidTuning()
    {
        return IsFinite(baseClearance) && baseClearance >= 0f
            && IsFinite(clearanceVariation) && clearanceVariation >= 0f
            && IsFinite(longitudinalRange) && longitudinalRange >= 0f
            && IsFinite(verticalRange) && verticalRange >= 0f
            && IsFinite(maxPenetration) && maxPenetration >= 0f;
    }

    private static float StableSignedVariation(uint randomSeed)
    {
        uint hash = randomSeed;
        unchecked
        {
            hash ^= hash >> 16;
            hash *= 0x7feb352du;
            hash ^= hash >> 15;
            hash *= 0x846ca68bu;
            hash ^= hash >> 16;
        }

        return (hash & 0x00ffffffu) * (2f / 16777215f) - 1f;
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
