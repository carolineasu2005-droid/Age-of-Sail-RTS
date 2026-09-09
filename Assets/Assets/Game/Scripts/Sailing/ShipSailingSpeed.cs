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

    [SerializeField]
    private ShipLeeway shipLeeway;


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
    private float polarTargetSpeed;

    [SerializeField]
    private float effectiveTargetSpeed;

    [SerializeField]
    private bool maneuverMinimumTargetSpeedActive;

    [SerializeField]
    private float maneuverMinimumTargetSpeed;

    [SerializeField]
    private bool formationMaximumTargetSpeedActive;

    [SerializeField]
    private float formationMaximumTargetSpeed;

    [SerializeField]
    private bool playerStopSpeedCapActive;

    [SerializeField]
    private float playerStopSpeedCap;

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

    [SerializeField]
    private Vector3 forwardVelocity;

    [SerializeField]
    private Vector3 leewayVelocity;

    [SerializeField]
    private Vector3 actualVelocity;

    [SerializeField]
    private float courseSpeed;

    [SerializeField]
    private float courseHeading;

    [SerializeField]
    private float headingCourseDelta;

    [Header("Debug Visualization")]

    [SerializeField]
    [Min(1f)]
    private float headingArrowLength = 20f;

    [SerializeField]
    [Min(1f)]
    private float velocityArrowScale = 10f;

    public float CurrentSpeed => currentSpeed;

    public float PolarTargetSpeed => polarTargetSpeed;

    public float AvailableTargetSpeed => polarTargetSpeed;

    public float EffectiveTargetSpeed => effectiveTargetSpeed;

    public bool FormationSpeedCapActive => formationMaximumTargetSpeedActive;

    public float FormationSpeedCap => formationMaximumTargetSpeed;

    public bool PlayerStopSpeedCapActive => playerStopSpeedCapActive;

    public float PlayerStopSpeedCap => playerStopSpeedCap;

    public bool IsPlayerStopped => playerStopSpeedCapActive
        && playerStopSpeedCap <= 0f;

    public float BaseMaxSpeed => baseMaxSpeed;

    public float RelativeWindAngleSigned => relativeWindAngleSigned;

    public float RelativeWindAngleAbsolute => relativeWindAngleAbsolute;

    public float PolarEfficiency => polarEfficiency;

    public bool IsInNoGoZone => isInNoGoZone;

    public Vector3 ForwardVelocity => forwardVelocity;

    public Vector3 LeewayVelocity => leewayVelocity;

    public Vector3 ActualVelocity => actualVelocity;

    public float CourseSpeed => courseSpeed;

    public float CourseHeading => courseHeading;

    public float HeadingCourseDelta => headingCourseDelta;

    public void ApplyMovementProfile(ShipMovementProfile movementProfile)
    {
        if (movementProfile == null)
        {
            return;
        }

        baseMaxSpeed = movementProfile.baseMaxSpeed;
        accelerationTimeConstant = movementProfile.accelerationTimeConstant;
        naturalDragTimeConstant = movementProfile.naturalDragTimeConstant;
        fullTurnDragTimeConstant = movementProfile.fullTurnDragTimeConstant;
    }

    public void SetManeuverMinimumTargetSpeed(float minimumTargetSpeed)
    {
        maneuverMinimumTargetSpeed = Mathf.Max(0f, minimumTargetSpeed);
        maneuverMinimumTargetSpeedActive = true;
    }

    public void ClearManeuverMinimumTargetSpeed()
    {
        maneuverMinimumTargetSpeedActive = false;
        maneuverMinimumTargetSpeed = 0f;
    }

    public void SetFormationMaximumTargetSpeed(float maximumTargetSpeed)
    {
        formationMaximumTargetSpeed = Mathf.Max(0f, maximumTargetSpeed);
        formationMaximumTargetSpeedActive = true;
    }

    public void ClearFormationMaximumTargetSpeed()
    {
        formationMaximumTargetSpeedActive = false;
        formationMaximumTargetSpeed = 0f;
    }

    public void SetPlayerStopSpeedCap(float maximumTargetSpeed)
    {
        playerStopSpeedCap = Mathf.Max(0f, maximumTargetSpeed);
        playerStopSpeedCapActive = true;
    }

    public void ClearPlayerStopSpeedCap()
    {
        playerStopSpeedCapActive = false;
        playerStopSpeedCap = 0f;
    }

    public static float ComposeEffectiveTargetSpeed(
        float availableTargetSpeed,
        bool maneuverMinimumActive,
        float maneuverMinimumTargetSpeed,
        bool formationSpeedCapActive,
        float formationSpeedCap,
        bool playerStopSpeedCapActive,
        float playerStopSpeedCap
    )
    {
        float effectiveSpeed = Mathf.Max(0f, availableTargetSpeed);

        if (maneuverMinimumActive)
        {
            effectiveSpeed = Mathf.Max(
                effectiveSpeed,
                Mathf.Max(0f, maneuverMinimumTargetSpeed)
            );
        }

        if (formationSpeedCapActive)
        {
            effectiveSpeed = Mathf.Min(
                effectiveSpeed,
                Mathf.Max(0f, formationSpeedCap)
            );
        }

        if (playerStopSpeedCapActive)
        {
            effectiveSpeed = Mathf.Min(
                effectiveSpeed,
                Mathf.Max(0f, playerStopSpeedCap)
            );
        }

        return effectiveSpeed;
    }

    private void Awake()
    {
        if (shipLeeway == null)
        {
            shipLeeway = GetComponent<ShipLeeway>();
        }
    }

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
        polarTargetSpeed = targetSpeed;
        effectiveTargetSpeed = ComposeEffectiveTargetSpeed(
            polarTargetSpeed,
            maneuverMinimumTargetSpeedActive,
            maneuverMinimumTargetSpeed,
            formationMaximumTargetSpeedActive,
            formationMaximumTargetSpeed,
            playerStopSpeedCapActive,
            playerStopSpeedCap
        );

        isInNoGoZone =
            relativeWindAngleAbsolute <= sailPolarProfile.noGoAngle;
    }


    private void UpdateCurrentSpeed(float deltaTime)
    {
        float previousSpeed = currentSpeed;

        float timeConstant =
            effectiveTargetSpeed > currentSpeed
                ? accelerationTimeConstant
                : naturalDragTimeConstant;

        float response =
            1f - Mathf.Exp(-deltaTime / timeConstant);

        currentSpeed =
            Mathf.Lerp(currentSpeed, effectiveTargetSpeed, response);

        if (effectiveTargetSpeed <= 0f && currentSpeed < stopThreshold)
        {
            currentSpeed = 0f;
        }

        speedError = effectiveTargetSpeed - currentSpeed;

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
    forwardVelocity = transform.forward * currentSpeed;
    leewayVelocity = shipLeeway != null
        ? shipLeeway.CalculateLeewayVelocity(
            currentSpeed,
            relativeWindAngleAbsolute,
            globalWind.WindFlowDirection
        )
        : Vector3.zero;
    actualVelocity = forwardVelocity + leewayVelocity;
    courseSpeed = actualVelocity.magnitude;

    float shipHeading = Mathf.Repeat(transform.eulerAngles.y, 360f);
    Vector3 horizontalActualVelocity = Vector3.ProjectOnPlane(
        actualVelocity,
        Vector3.up
    );

    courseHeading = horizontalActualVelocity.sqrMagnitude > 0.0001f
        ? Mathf.Repeat(
            Mathf.Atan2(
                horizontalActualVelocity.x,
                horizontalActualVelocity.z
            ) * Mathf.Rad2Deg,
            360f
        )
        : shipHeading;
    headingCourseDelta = Mathf.DeltaAngle(shipHeading, courseHeading);

    transform.position += actualVelocity * deltaTime;
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
            origin + actualVelocity * velocityArrowScale
        );
    }
}
