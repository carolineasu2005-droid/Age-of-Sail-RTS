using UnityEngine;

public class ShipMovementProfileController : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private ShipMovementProfile movementProfile;

    [SerializeField]
    private ShipSailingSpeed shipSailingSpeed;

    [SerializeField]
    private ShipTurning shipTurning;

    [SerializeField]
    private ShipTacking shipTacking;


    [Header("Runtime Debug")]

    [SerializeField]
    private bool profileApplied;

    [SerializeField]
    private string appliedProfileName;


    private void Awake()
    {
        if (shipSailingSpeed == null)
        {
            shipSailingSpeed = GetComponent<ShipSailingSpeed>();
        }

        if (shipTurning == null)
        {
            shipTurning = GetComponent<ShipTurning>();
        }

        if (shipTacking == null)
        {
            shipTacking = GetComponent<ShipTacking>();
        }

        ApplyProfile();
    }


    public void ApplyProfile()
    {
        profileApplied = false;
        appliedProfileName = string.Empty;

        if (movementProfile == null
            || shipSailingSpeed == null
            || shipTurning == null
            || shipTacking == null)
        {
            return;
        }

        shipSailingSpeed.ApplyMovementProfile(movementProfile);
        shipTurning.ApplyMovementProfile(movementProfile);
        shipTacking.ApplyMovementProfile(movementProfile);

        profileApplied = true;
        appliedProfileName = movementProfile.name;
    }
}
