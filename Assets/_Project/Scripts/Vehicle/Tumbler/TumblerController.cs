using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class TumblerController : MonoBehaviour
{
    public enum driveType { FrontWheelDrive, RearWheelDrive, AllWheelDrive }

    internal const float SpeedToKPH = 3.6f;

    internal bool isHandbrakeActive = false;
    internal float steerInput = 0f;
    internal bool shouldResetMotorTorque = false;

    public Rigidbody carRigidbody;
    public TumblerInput tumblerInput;

    public UnityEvent onGearChange;
    public UnityEvent onDriveModeChange;

    [Header("Wheel Colliders")]
    [SerializeField] private WheelCollider[] frontWheelsColliders;
    [SerializeField] private WheelCollider[] rearWheelsColliders;

    [Header("Wheel Transforms")]
    [SerializeField] private Transform[] wheelsTransforms;

    [Header("Performance Settings")]
    [SerializeField] private float forwardPower = 1400f;
    [SerializeField] private float reversePower = 800f;
    [SerializeField] private float brakingForce = 1000f;
    [SerializeField] private float maxTurnAngle = 40f;
    [SerializeField] private float handbrakeForce = 4000f;
    [SerializeField] private float maxSpeedKPH = 260f;
    [SerializeField] private float maxTurnAngleAtMaxSpeed = 20f;
    [SerializeField] private float steeringResponse = 5f;
    [SerializeField] private float downForce = 150f;

    [Header("Steering Assist")]
    [Tooltip("Base cornering demand used to taper steering, in m/s². The separate high-speed allowance keeps steering responsive.")]
    [SerializeField, Min(0.1f)] private float maxSteeringLateralAcceleration = 9f;
    [Tooltip("Soft steering allowance retained at high speed, in degrees.")]
    [SerializeField, Range(0f, 10f)] private float highSpeedSteeringAllowance = 4f;
    private float wheelbase;

    [Header("Gearbox Settings")]
    [SerializeField, Range(1, 7)] private int numberOfGears = 7;
    [SerializeField] private float[] gearRatios = { 5.8f, 4f, 2.9f, 2.25f, 1.8f, 1.4f, 1.08f };
    [SerializeField] private float finalDriveRatio = 5.7f;
    [SerializeField] private float idleRPM = 900f;
    [SerializeField] private float redlineRPM = 7200f;
    [SerializeField] private float upshiftRPM = 6600f;
    [SerializeField] private float downshiftRPM = 3200f;
    [SerializeField] private float maxDriveForce = 18000f;
    [SerializeField] private float reverseMaxSpeedKPH = 80f;
    private const float ReferenceWheelRadius = 0.66f;
    private float engineRPM = 900f;
    private float shiftCooldown;
    private float shiftTimeRemaining;
    private float motorTorque = 0f;

    public int currentGear { get; private set;} = 1;
    public driveType currentDriveMode { get; private set; } = driveType.AllWheelDrive;
    public bool IsReversing => currentPower < -0.01f || Vector3.Dot(carRigidbody.linearVelocity, transform.forward) < -0.5f;

    private float currentPower = 0f;
    private float currentBrakeForce = 0f;
    private float currentTurnAngle = 0f;

    private void Awake()
    {
        tumblerInput = new TumblerInput();
        carRigidbody = GetComponent<Rigidbody>();
        if (carRigidbody == null)
        {
            Debug.LogError("Rigidbody component not found on " + gameObject.name);
        }
        CacheWheelbase();
    }

    private void OnEnable()
    {
        tumblerInput.Enable();
    }

    private void OnDisable()
    {
        tumblerInput.Disable();
    }
    
    private void Start()
    {
        onGearChange.Invoke();
        onDriveModeChange.Invoke();
    }
    private void FixedUpdate()
    {
        ApplyDrive();
        HandleSteering();
        UpdateWheels();
        AddDownForce();
        LimitHorizontalSpeed();
    }

    private void Update()
    {
        float throttleInput = tumblerInput.Gameplay.Throttle.ReadValue<float>();
        float oldPower = currentPower;
        currentPower = throttleInput * (throttleInput > 0 ? forwardPower : reversePower);
        shouldResetMotorTorque = oldPower * currentPower < 0f;
        isHandbrakeActive = tumblerInput.Gameplay.Handbrake.ReadValue<float>() > 0f;
        currentBrakeForce = tumblerInput.Gameplay.Brake.ReadValue<float>() > 0f ? brakingForce : 0f;
        steerInput = tumblerInput.Gameplay.Steer.ReadValue<Vector2>().x;
    }

    private void ApplyDrive()
    {
        HandleGearShifting();

        // Calculate current speed in meters per second
        float currentSpeed = Vector3.ProjectOnPlane(carRigidbody.linearVelocity, Vector3.up).magnitude;
        float maxSpeedInMetersPerSecond = (currentPower < 0f ? reverseMaxSpeedKPH : maxSpeedKPH) / SpeedToKPH;

        // Reset motor torque if the throttle input changes from positive to negative or vice versa
        if (shouldResetMotorTorque)
        {
            ResetMotorTorque();
            shouldResetMotorTorque = false;
        }

        // Apply the appropriate drive mode based on the current drive mode
        switch (currentDriveMode) {
            case driveType.FrontWheelDrive:
                ApplyFrontWheelDrive(currentSpeed, maxSpeedInMetersPerSecond);
                break;
            case driveType.RearWheelDrive:
                ApplyRearWheelDrive(currentSpeed, maxSpeedInMetersPerSecond);
                break;
            case driveType.AllWheelDrive:
                ApplyAllWheelDrive(currentSpeed, maxSpeedInMetersPerSecond);
                break;
        }
        ApplyBrakesAndHandbrake();
    }

    private void ApplyBrakesAndHandbrake()
    {
        foreach(WheelCollider wheel in frontWheelsColliders)
        {
            ApplyBraking(wheel);
        }
        if(isHandbrakeActive) {
            ApplyHandbrake();
        }
        else
        {
            foreach(WheelCollider wheel in rearWheelsColliders)
            {
                ApplyBraking(wheel);
            }
        }
    }

    private void ApplyBraking(WheelCollider wheel)
    {
        wheel.brakeTorque = currentBrakeForce;
        WheelFrictionCurve frictionCurve = wheel.sidewaysFriction;
        frictionCurve.extremumSlip = 0.2f;
        wheel.sidewaysFriction = frictionCurve;
    }

    private void ApplyHandbrake()
    {
        foreach(WheelCollider wheel in rearWheelsColliders)
        {
            wheel.brakeTorque = handbrakeForce;
            WheelFrictionCurve frictionCurve = wheel.sidewaysFriction;
            frictionCurve.extremumSlip = 0.5f;
            wheel.sidewaysFriction = frictionCurve;
        }
    }

    private void ApplyFrontWheelDrive(float currentSpeed, float maxSpeedInMetersPerSecond)
    {
        float torqueModifier = (currentSpeed < maxSpeedInMetersPerSecond) ? 1f : 0f;
        foreach(WheelCollider wheel in frontWheelsColliders)
        {
            wheel.motorTorque = motorTorque * torqueModifier * wheel.radius / ReferenceWheelRadius / frontWheelsColliders.Length;
        }
    }

    private void ApplyRearWheelDrive(float currentSpeed, float maxSpeedInMetersPerSecond)
    {
        float torqueModifier = (currentSpeed < maxSpeedInMetersPerSecond) ? 1f : 0f;
        foreach(WheelCollider wheel in rearWheelsColliders)
        {
            wheel.motorTorque = motorTorque * torqueModifier * wheel.radius / ReferenceWheelRadius / rearWheelsColliders.Length;
        }
    }

    private void ApplyAllWheelDrive(float currentSpeed, float maxSpeedInMetersPerSecond)
    {
        float torqueModifier = (currentSpeed < maxSpeedInMetersPerSecond) ? 1f : 0f;
        foreach(WheelCollider wheel in frontWheelsColliders)
        {
            wheel.motorTorque = motorTorque * torqueModifier * wheel.radius / ReferenceWheelRadius / (frontWheelsColliders.Length + rearWheelsColliders.Length);
        }
        foreach(WheelCollider wheel in rearWheelsColliders)
        {
            wheel.motorTorque = motorTorque * torqueModifier * wheel.radius / ReferenceWheelRadius / (frontWheelsColliders.Length + rearWheelsColliders.Length);
        }
    }

    private void HandleSteering()
    {
        float planarSpeed = Vector3.ProjectOnPlane(carRigidbody.linearVelocity, Vector3.up).magnitude;
        float dynamicMaxTurnAngle = GetMaxSteeringAngle(planarSpeed);
        
        // Calculate the target steering angle based on the current steer input (-1 to 1)
        float targetTurnAngle = steerInput * dynamicMaxTurnAngle;

        // Smoothly interpolate the current turn angle towards the target turn angle
        currentTurnAngle = Mathf.Lerp(currentTurnAngle, targetTurnAngle, Time.fixedDeltaTime * steeringResponse);
        currentTurnAngle = Mathf.Clamp(currentTurnAngle, -dynamicMaxTurnAngle, dynamicMaxTurnAngle);

        // Apply the calculated steering angle to all front wheels
        foreach (WheelCollider wheel in frontWheelsColliders)
        {
            wheel.steerAngle = currentTurnAngle;
        }
    }

    private float GetMaxSteeringAngle(float speed)
    {
        float speedFactor = Mathf.Clamp01(speed / (maxSpeedKPH / SpeedToKPH));
        float originalLimit = Mathf.Lerp(maxTurnAngle, maxTurnAngleAtMaxSpeed, speedFactor);
        if (wheelbase <= 0f || speed <= 0f) return originalLimit;

        // The soft allowance retains useful steering at speed instead of tending to zero.
        float corneringLimit = Mathf.Atan(wheelbase * Mathf.Max(0.1f, maxSteeringLateralAcceleration) / (speed * speed) +
            Mathf.Tan(highSpeedSteeringAllowance * Mathf.Deg2Rad)) * Mathf.Rad2Deg;
        return Mathf.Min(originalLimit, corneringLimit);
    }

    private void CacheWheelbase()
    {
        if (frontWheelsColliders == null || rearWheelsColliders == null ||
            frontWheelsColliders.Length == 0 || rearWheelsColliders.Length == 0) return;

        float frontPosition = 0f;
        float rearPosition = 0f;
        foreach (var wheel in frontWheelsColliders)
        {
            if (wheel == null) return;
            frontPosition += transform.InverseTransformPoint(wheel.transform.position).z;
        }
        foreach (var wheel in rearWheelsColliders)
        {
            if (wheel == null) return;
            rearPosition += transform.InverseTransformPoint(wheel.transform.position).z;
        }
        wheelbase = Mathf.Abs(frontPosition / frontWheelsColliders.Length - rearPosition / rearWheelsColliders.Length);
    }


    private void HandleGearShifting()
    {
        shiftCooldown = Mathf.Max(0f, shiftCooldown - Time.fixedDeltaTime);
        shiftTimeRemaining = Mathf.Max(0f, shiftTimeRemaining - Time.fixedDeltaTime);
        currentGear = Mathf.Clamp(currentGear, 1, Mathf.Min(numberOfGears, gearRatios.Length));
        float forwardSpeed = Vector3.Dot(carRigidbody.linearVelocity, transform.forward);
        float roadWheelRPM = Mathf.Abs(forwardSpeed) * 60f / (2f * Mathf.PI * ReferenceWheelRadius);
        float ratio = currentPower < 0f ? gearRatios[1] : gearRatios[currentGear - 1];
        float roadRPM = roadWheelRPM * ratio * finalDriveRatio;
        if (currentPower >= 0f && IsGrounded() && shiftCooldown <= 0f)
        {
            if (forwardSpeed > 1f && roadRPM > upshiftRPM) IncrementGear();
            else if (roadRPM < downshiftRPM && currentGear > 1) DecrementGear();
        }
        ratio = currentPower < 0f ? gearRatios[1] : gearRatios[currentGear - 1];
        float wheelRPM = GetDrivenWheelRPM();
        float coupledWheelRPM = shiftTimeRemaining > 0f ? roadWheelRPM : Mathf.Max(roadWheelRPM, wheelRPM);
        float targetRPM = Mathf.Clamp(coupledWheelRPM * ratio * finalDriveRatio, idleRPM, redlineRPM);
        engineRPM = Mathf.Lerp(engineRPM, targetRPM, 1f - Mathf.Exp(-12f * Time.fixedDeltaTime));
        float torque = Mathf.Min(Mathf.Abs(currentPower) * ratio * finalDriveRatio, maxDriveForce * ReferenceWheelRadius);
        float shiftDelivery = Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(shiftTimeRemaining / 0.18f));
        motorTorque = Mathf.Sign(currentPower) * torque * shiftDelivery;
    }

    private float GetDrivenWheelRPM()
    {
        float rpm = 0f;
        int count = 0;
        if (currentDriveMode != driveType.RearWheelDrive)
            foreach (var wheel in frontWheelsColliders) { rpm += Mathf.Abs(wheel.rpm) * wheel.radius / ReferenceWheelRadius; count++; }
        if (currentDriveMode != driveType.FrontWheelDrive)
            foreach (var wheel in rearWheelsColliders) { rpm += Mathf.Abs(wheel.rpm) * wheel.radius / ReferenceWheelRadius; count++; }
        return count > 0 ? rpm / count : 0f;
    }

    private void LimitHorizontalSpeed()
    {
        Vector3 velocity = carRigidbody.linearVelocity;
        Vector3 horizontal = Vector3.ProjectOnPlane(velocity, Vector3.up);
        float limit = (currentPower < 0f ? reverseMaxSpeedKPH : maxSpeedKPH) / SpeedToKPH;
        if (horizontal.sqrMagnitude > limit * limit)
            carRigidbody.linearVelocity = horizontal.normalized * limit + Vector3.up * velocity.y;
    }

    private void UpdateWheels()
    {
        for (int i = 0; i < frontWheelsColliders.Length; i++)
        {
            UpdateWheel(frontWheelsColliders[i], wheelsTransforms[i]);
        }
        for (int i = 0; i < rearWheelsColliders.Length; i++)
        {
            UpdateWheel(rearWheelsColliders[i], wheelsTransforms[i + frontWheelsColliders.Length]);
        }
    }

    private void UpdateWheel(WheelCollider wheel, Transform wheelTransform)
    {
        wheel.GetWorldPose(out Vector3 position, out Quaternion rotation);
        wheelTransform.position = position;
        wheelTransform.rotation = rotation;
    }

    private void AddDownForce()
    {
        carRigidbody.AddForce(Vector3.down * downForce * carRigidbody.linearVelocity.magnitude);
    }

    // This function is called by the Unity Event System when the player clicks the Drive Mode button
    public void SwitchDriveMode(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            switch (currentDriveMode)
            {
                case driveType.FrontWheelDrive:
                    currentDriveMode = driveType.RearWheelDrive;
                    break;
                case driveType.RearWheelDrive:
                    currentDriveMode = driveType.AllWheelDrive;
                    break;
                case driveType.AllWheelDrive:
                    currentDriveMode = driveType.FrontWheelDrive;
                    break;
            }
            onDriveModeChange.Invoke();
            ResetMotorTorque();
        }
    }

    private void ResetMotorTorque()
    {
        motorTorque = 0f;
        foreach(WheelCollider wheel in frontWheelsColliders)
        {
            wheel.motorTorque = motorTorque;
        }
        foreach(WheelCollider wheel in rearWheelsColliders)
        {
            wheel.motorTorque = motorTorque;
        }
    }

    public void ResetTransmission()
    {
        currentGear = 1;
        engineRPM = idleRPM;
        shiftCooldown = 0f;
        shiftTimeRemaining = 0f;
        ResetMotorTorque();
        foreach (var wheel in frontWheelsColliders) wheel.rotationSpeed = 0f;
        foreach (var wheel in rearWheelsColliders) wheel.rotationSpeed = 0f;
        onGearChange.Invoke();
    }

    public bool IsGrounded()
    {
        foreach(WheelCollider wheel in frontWheelsColliders)
        {
            if (wheel.isGrounded) return true;
        }
        foreach(WheelCollider wheel in rearWheelsColliders)
        {
            if (wheel.isGrounded) return true;
        }
        return false;
    }

    public void IncrementGear()
    {
        if (currentGear < Mathf.Min(numberOfGears, gearRatios.Length))
        {
            float oldRatio = gearRatios[currentGear - 1];
            currentGear++;
            engineRPM = Mathf.Clamp(engineRPM * gearRatios[currentGear - 1] / oldRatio, idleRPM, redlineRPM);
            shiftCooldown = 0.45f;
            shiftTimeRemaining = 0.18f;
            onGearChange.Invoke();
        }
    }

    public void DecrementGear()
    {
        if (currentGear > 1)
        {
            float oldRatio = gearRatios[currentGear - 1];
            currentGear--;
            engineRPM = Mathf.Clamp(engineRPM * gearRatios[currentGear - 1] / oldRatio, idleRPM, redlineRPM);
            shiftCooldown = 0.45f;
            shiftTimeRemaining = 0.18f;
            onGearChange.Invoke();
        }
    }

    public float GetSpeed()
    {
        return Vector3.ProjectOnPlane(carRigidbody.linearVelocity, Vector3.up).magnitude * SpeedToKPH;
    }

    public float GetEngineRPM()
    {
        return engineRPM;
    }

    public float GetMotorTorque()
    {
        return motorTorque;
    }

    public float GetMaxSpeed()
    {
        return maxSpeedKPH;
    }

    public float GetIdleRPM() => idleRPM;
    public float GetRedlineRPM() => redlineRPM;
    public float GetNormalizedEngineRPM() => Mathf.InverseLerp(idleRPM, redlineRPM, engineRPM);

    public bool IsHandbraking()
    {
        return isHandbrakeActive;
    }
}
