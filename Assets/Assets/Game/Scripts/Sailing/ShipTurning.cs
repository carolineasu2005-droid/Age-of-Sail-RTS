using UnityEngine;

[RequireComponent(typeof(ShipSailingSpeed))]
public class ShipTurning : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private ShipSailingSpeed shipSailingSpeed;


    [Header("Rudder Settings")]

    [SerializeField]
    [Range(-1f, 1f)]
    private float rudderCommand;

    [SerializeField]
    [Min(0.01f)]
    private float maxRudderAngle = 30f;

    [SerializeField]
    [Min(0f)]
    private float rudderResponse = 45f;

    [SerializeField]
    [Min(0.01f)]
    private float rudderReferenceSpeed = 3f;

    [SerializeField]
    [Min(0f)]
    private float maxTurnRate = 6f;


    [Header("Runtime Debug")]

    [SerializeField]
    private float targetRudderAngle;

    [SerializeField]
    private float currentRudderAngle;

    [SerializeField]
    private float speedFactor;

    [SerializeField]
    private float normalizedRudder;

    [SerializeField]
    private float currentTurnRate;

    [SerializeField]
    private bool isTurning;

    [SerializeField]
    private float estimatedTurnRadius;

    [SerializeField]
    private float turningIntensity;

    [SerializeField]
    private float baseTurnRate;

    [SerializeField]
    private float maneuverYawAssistRate;

    public float TurningIntensity => turningIntensity;

    public void SetManeuverYawAssistRate(float yawRate)
    {
        maneuverYawAssistRate = yawRate;
    }

    public void ClearManeuverYawAssist()
    {
        maneuverYawAssistRate = 0f;
    }

    public void SetRudderCommand(float command)
    {
        rudderCommand = Mathf.Clamp(command, -1f, 1f);
    }

    private void Awake()
    {
        if (shipSailingSpeed == null)
        {
            shipSailingSpeed = GetComponent<ShipSailingSpeed>();
        }
    }


    private void Update()
    {
        if (shipSailingSpeed == null)
        {
            return;
        }

        targetRudderAngle = rudderCommand * maxRudderAngle;
        currentRudderAngle = Mathf.MoveTowards(
            currentRudderAngle,
            targetRudderAngle,
            rudderResponse * Time.deltaTime
        );

        speedFactor = Mathf.Clamp01(
            shipSailingSpeed.CurrentSpeed / rudderReferenceSpeed
        );

        normalizedRudder = currentRudderAngle / maxRudderAngle;

        baseTurnRate =
            maxTurnRate
            * normalizedRudder
            * speedFactor;

        currentTurnRate = baseTurnRate + maneuverYawAssistRate;

        turningIntensity = maxTurnRate > 0f
            ? Mathf.Clamp01(Mathf.Abs(baseTurnRate) / maxTurnRate)
            : 0f;

        transform.Rotate(
            0f,
            currentTurnRate * Time.deltaTime,
            0f,
            Space.Self
        );

        UpdateTurningDebug();
    }


    private void UpdateTurningDebug()
    {
        float currentSpeed = shipSailingSpeed.CurrentSpeed;

        isTurning =
            currentSpeed > 0.01f
            && Mathf.Abs(currentTurnRate) > 0.01f;

        if (!isTurning)
        {
            estimatedTurnRadius = 0f;
            return;
        }

        float angularSpeedRadians =
            Mathf.Abs(currentTurnRate) * Mathf.Deg2Rad;

        estimatedTurnRadius = currentSpeed / angularSpeedRadians;

        if (float.IsNaN(estimatedTurnRadius)
            || float.IsInfinity(estimatedTurnRadius)
            || estimatedTurnRadius < 0f)
        {
            estimatedTurnRadius = 0f;
        }
    }
}
