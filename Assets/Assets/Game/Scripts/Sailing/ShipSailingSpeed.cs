using UnityEngine;

public class ShipSailingSpeed : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private GlobalWind globalWind;

    [SerializeField]
    private SailPolarProfile sailPolarProfile;

    [SerializeField]
    private ShipTurning shipTurning;


    [Header("Speed Settings")]

    [SerializeField]
    [Min(0f)]
    private float baseMaxSpeed = 4f;

    [SerializeField]
    [Min(0.01f)]
    private float accelerationTimeConstant = 8f;

    [SerializeField]
    [Min(0.01f)]
    private float naturalDragTimeConstant = 12f;

    [SerializeField]
    [Min(0f)]
    private float stopThreshold = 0.03f;


    [Header("Turning Drag Settings")]

    [SerializeField]
    [Min(0.01f)]
    private float fullTurnDragTimeConstant = 20f;


    [Header("Runtime Debug")]

    [SerializeField]
    private float relativeWindAngleSigned;

    [SerializeField]
    private float relativeWindAngleAbsolute;

    [SerializeField]
    private float polarEfficiency;

    [SerializeField]
    private float targetSpeed;

    [SerializeField]
    private float currentSpeed;

    [SerializeField]
    private float speedError;

    [SerializeField]
    private float longitudinalAcceleration;

    [SerializeField]
    private bool isInNoGoZone;

    [SerializeField]
    private float turningIntensity;

    [SerializeField]
    private float turningDragIntensity;

    [SerializeField]
    private float turningDragFactor = 1f;

    [Header("Debug Visualization")]

    [SerializeField]
    [Min(1f)]
    private float headingArrowLength = 20f;

    [SerializeField]
    [Min(1f)]
    private float velocityArrowScale = 10f;

    public float CurrentSpeed => currentSpeed;

    private void Update()
    {
    if (globalWind == null || sailPolarProfile == null)
    {
        return;
    }

    CalculateSailingData();
    UpdateCurrentSpeed(Time.deltaTime);
    ApplyTurningDrag(Time.deltaTime);
    MoveShip(Time.deltaTime);
    }


    private void CalculateSailingData()
    {
        Vector3 shipForward = transform.forward;
        Vector3 windFrom = globalWind.WindFromDirection;

        relativeWindAngleSigned = Vector3.SignedAngle(
            shipForward,
            windFrom,
            Vector3.up
        );

        relativeWindAngleAbsolute =
            Mathf.Abs(relativeWindAngleSigned);

        polarEfficiency =
            sailPolarProfile.Evaluate(relativeWindAngleAbsolute);

        targetSpeed =
            baseMaxSpeed
            * polarEfficiency
            * globalWind.windStrength;

        isInNoGoZone =
            relativeWindAngleAbsolute <= sailPolarProfile.noGoAngle;
    }


    private void UpdateCurrentSpeed(float deltaTime)
    {
        float previousSpeed = currentSpeed;

        float timeConstant =
            targetSpeed > currentSpeed
                ? accelerationTimeConstant
                : naturalDragTimeConstant;

        float response =
            1f - Mathf.Exp(-deltaTime / timeConstant);

        currentSpeed =
            Mathf.Lerp(currentSpeed, targetSpeed, response);

        if (targetSpeed <= 0f && currentSpeed < stopThreshold)
        {
            currentSpeed = 0f;
        }

        speedError = targetSpeed - currentSpeed;

        if (deltaTime > 0f)
        {
            longitudinalAcceleration =
                (currentSpeed - previousSpeed) / deltaTime;
        }
        else
        {
            longitudinalAcceleration = 0f;
        
        }
    }


    private void ApplyTurningDrag(float deltaTime)
    {
        if (shipTurning == null)
        {
            turningIntensity = 0f;
            turningDragIntensity = 0f;
            turningDragFactor = 1f;
            return;
        }

        turningIntensity = shipTurning.TurningIntensity;
        turningDragIntensity = turningIntensity * turningIntensity;
        turningDragFactor = Mathf.Exp(
            -turningDragIntensity
            * deltaTime
            / fullTurnDragTimeConstant
        );

        currentSpeed *= turningDragFactor;
    }


    private void MoveShip(float deltaTime)
    {
    Vector3 velocity = transform.forward * currentSpeed;

    transform.position += velocity * deltaTime;
    }
    private void OnDrawGizmos()
    {
        Vector3 origin = transform.position;

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(
            origin,
            origin + transform.forward * headingArrowLength
        );

        Gizmos.color = Color.green;
        Gizmos.DrawLine(
            origin,
            origin + transform.forward * currentSpeed * velocityArrowScale
        );
    }
}
