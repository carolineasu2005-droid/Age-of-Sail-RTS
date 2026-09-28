using UnityEngine;

[AddComponentMenu("Age of Sail/Combat/Combat Obstruction Volume")]
[DisallowMultipleComponent]
public sealed class CombatObstructionVolume : MonoBehaviour
{
    public const string LayerName = "CombatObstruction";
    public const int LayerIndex = 9;

    [SerializeField]
    private Collider queryCollider;


    public Collider QueryCollider => queryCollider;

    public bool IsConfigured => queryCollider != null
        && queryCollider.gameObject == gameObject
        && queryCollider.enabled
        && queryCollider.isTrigger
        && gameObject.layer == LayerIndex;


    internal static bool TryResolve(
        Collider collider,
        out CombatObstructionVolume obstruction
    )
    {
        obstruction = null;

        if (collider == null
            || !collider.TryGetComponent(
                out CombatObstructionVolume candidate
            )
            || candidate.QueryCollider != collider
            || !candidate.IsConfigured)
        {
            return false;
        }

        obstruction = candidate;
        return true;
    }
}
