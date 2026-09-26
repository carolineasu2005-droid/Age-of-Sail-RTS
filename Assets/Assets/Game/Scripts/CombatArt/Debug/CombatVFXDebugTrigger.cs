#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Age of Sail/Debug/Combat VFX Debug Trigger")]
[DisallowMultipleComponent]
public sealed class CombatVFXDebugTrigger : MonoBehaviour
{
    [Header("Event Destination")]

    [SerializeField]
    [Tooltip("Must implement ICombatVFXEventReceiver.")]
    private MonoBehaviour receiverBehaviour;

    [Header("Muzzle Events")]

    [SerializeField]
    private ShipMuzzleSockets muzzleSockets;

    [SerializeField]
    [Min(0)]
    private int portMuzzleIndex;

    [SerializeField]
    [Min(0)]
    private int starboardMuzzleIndex;

    [Header("Impact Events")]

    [SerializeField]
    [Tooltip("Position supplies the impact point; local Up supplies the normal.")]
    private Transform waterImpactReference;

    [SerializeField]
    [Tooltip("Position supplies the impact point; local Up supplies the normal.")]
    private Transform hullImpactReference;

    [SerializeField]
    private GameObject hullTargetShip;


    private void Reset()
    {
        receiverBehaviour = GetComponent<CombatVFXPlaceholderReceiver>();
        muzzleSockets = GetComponent<ShipMuzzleSockets>();
        hullTargetShip = gameObject;
    }


    [ContextMenu("Combat VFX Debug/Trigger Port Muzzle Fire")]
    private void TriggerPortMuzzleFire()
    {
        TriggerMuzzleFire(muzzleSockets?.PortMuzzles, portMuzzleIndex, "Port");
    }


    [ContextMenu("Combat VFX Debug/Trigger Starboard Muzzle Fire")]
    private void TriggerStarboardMuzzleFire()
    {
        TriggerMuzzleFire(
            muzzleSockets?.StarboardMuzzles,
            starboardMuzzleIndex,
            "Starboard"
        );
    }


    [ContextMenu("Combat VFX Debug/Trigger Water Impact")]
    private void TriggerWaterImpact()
    {
        if (!TryGetReceiver(out ICombatVFXEventReceiver receiver)
            || !TryGetImpactReference(
                waterImpactReference,
                "Water Impact",
                out Transform impactReference
            ))
        {
            return;
        }

        receiver.OnWaterImpact(
            new CombatWaterImpactEvent(
                impactReference.position,
                impactReference.up
            )
        );
    }


    [ContextMenu("Combat VFX Debug/Trigger Hull Impact")]
    private void TriggerHullImpact()
    {
        if (!TryGetReceiver(out ICombatVFXEventReceiver receiver)
            || !TryGetImpactReference(
                hullImpactReference,
                "Hull Impact",
                out Transform impactReference
            ))
        {
            return;
        }

        if (hullTargetShip == null)
        {
            Debug.LogError(
                "Combat VFX Debug Trigger requires a Hull Target Ship.",
                this
            );
            return;
        }

        receiver.OnHullImpact(
            new CombatHullImpactEvent(
                impactReference.position,
                impactReference.up,
                hullTargetShip,
                CombatHullRegion.Midship
            )
        );
    }


    private void TriggerMuzzleFire(
        IReadOnlyList<Transform> sockets,
        int socketIndex,
        string sideName
    )
    {
        if (!TryGetReceiver(out ICombatVFXEventReceiver receiver))
        {
            return;
        }

        if (sockets == null || socketIndex < 0 || socketIndex >= sockets.Count)
        {
            Debug.LogError(
                $"Combat VFX Debug Trigger has no {sideName} muzzle at index "
                    + $"{socketIndex}.",
                this
            );
            return;
        }

        Transform socket = sockets[socketIndex];

        if (socket == null)
        {
            Debug.LogError(
                $"Combat VFX Debug Trigger {sideName} muzzle at index "
                    + $"{socketIndex} is null.",
                this
            );
            return;
        }

        receiver.OnMuzzleFire(
            new CombatMuzzleFireEvent(
                socket.position,
                socket.forward,
                muzzleSockets.gameObject,
                socket.name
            )
        );
    }


    private bool TryGetReceiver(out ICombatVFXEventReceiver receiver)
    {
        receiver = receiverBehaviour as ICombatVFXEventReceiver;

        if (receiver != null)
        {
            return true;
        }

        Debug.LogError(
            "Combat VFX Debug Trigger requires a Receiver Behaviour that "
                + "implements ICombatVFXEventReceiver.",
            this
        );
        return false;
    }


    private bool TryGetImpactReference(
        Transform configuredReference,
        string eventName,
        out Transform impactReference
    )
    {
        impactReference = configuredReference;

        if (impactReference != null)
        {
            return true;
        }

        Debug.LogError(
            $"Combat VFX Debug Trigger requires a {eventName} Reference.",
            this
        );
        return false;
    }
}
#endif
