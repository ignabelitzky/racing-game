using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class TumblerController : MonoBehaviour
{
    public enum driveType { FrontWheelDrive, RearWheelDrive, AllWheelDrive }

    internal const float SpeedToKph = 3.6f;

    internal bool isHandbrakeActive = false;
    internal float steerInput = 0f;

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
    [SerializeField] private float forwardPower = 1200f;
    [SerializeField] private float reversePower = 800f;
    [SerializeField] private float brakingForce = 1000f;
    [SerializeField] private float maxTurnAngle = 40f;
    [SerializeField] private float handbrakeForce = 4000f;
    [SerializeField] private float maxSpeedKPH = 175f;
    [SerializeField] private float maxTurnAngleAtMaxSpeed = 20f;
    [SerializeField] private float steeringResponse = 5f;
    [SerializeField] private float downForce = 150f;

    [Header("Gearbox Settings")]
    [SerializeField] private int numberOfGears = 5;
    private float[] rpmRange = { 0f, 42000, 105000, 187000, 182000, 176000};
    private float[] gearRatios = { 0f, 1.2f, 1.5f, 1.7f, 1.3f, 1.1f };
    private float engineRPM = 0f;
    private float motorTorque = 0f;

    public int currentGear { get; private set;} = 1;
    public driveType currentDriveMode { get; private set; } = driveType.AllWheelDrive;

    private float currentAcceleration = 0f;
    private float currentBrakeForce = 0f;
    private float currentTurnAngle = 0f;

    private void Awake()
    {
        carRigidbody = GetComponent<Rigidbody>();
        if (carRigidbody == null)
        {
            Debug.LogError("Rigidbody component not found on " + gameObject.name);
        }
        tumblerInput = new TumblerInput();
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
        carRigidbody.centerOfMass -= new Vector3(0, 0.1f, 0);
    }
    private void FixedUpdate()
    {
        ApplyDrive();
        HandleSteering();
        UpdateWheels();
        AddDownForce();
    }

    private void Update()
    {
        float throttleInput = tumblerInput.Gameplay.Throttle.ReadValue<float>();
        currentAcceleration = throttleInput * (throttleInput > 0 ? forwardPower : reversePower);
        isHandbrakeActive = tumblerInput.Gameplay.Handbrake.ReadValue<float>() > 0f;
        currentBrakeForce = tumblerInput.Gameplay.Brake.ReadValue<float>() > 0f ? brakingForce : 0f;
        steerInput = tumblerInput.Gameplay.Steer.ReadValue<Vector2>().x;
    }

    private void ApplyDrive()
    {
        HandleGearShifting();

        // Calculate current speed in meters per second
        float currentSpeed = carRigidbody.velocity.magnitude;
        float maxSpeedInMetersPerSecond = maxSpeedKPH / SpeedToKph;

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
            frictionCurve.extremumSlip = 1f;
            wheel.sidewaysFriction = frictionCurve;
        }
    }

    private void ApplyFrontWheelDrive(float currentSpeed, float maxSpeedInMetersPerSecond)
    {
        float torqueModifier = (currentSpeed < maxSpeedInMetersPerSecond) ? 1f : 0f;
        foreach(WheelCollider wheel in frontWheelsColliders)
        {
            wheel.motorTorque = motorTorque * torqueModifier;
        }
    }

    private void ApplyRearWheelDrive(float currentSpeed, float maxSpeedInMetersPerSecond)
    {
        float torqueModifier = (currentSpeed < maxSpeedInMetersPerSecond) ? 1f : 0f;
        foreach(WheelCollider wheel in rearWheelsColliders)
        {
            wheel.motorTorque = motorTorque * torqueModifier;
        }
    }

    private void ApplyAllWheelDrive(float currentSpeed, float maxSpeedInMetersPerSecond)
    {
        float torqueModifier = (currentSpeed < maxSpeedInMetersPerSecond) ? 1f : 0f;
        foreach(WheelCollider wheel in frontWheelsColliders)
        {
            wheel.motorTorque = motorTorque * torqueModifier;
        }
        foreach(WheelCollider wheel in rearWheelsColliders)
        {
            wheel.motorTorque = motorTorque * torqueModifier;
        }
    }

    private void HandleSteering()
    {
        // Calculate the speed factor, which is normalized between 0 and 1
        float speedFactor = Mathf.Clamp01(carRigidbody.velocity.magnitude / (maxSpeedKPH / SpeedToKph));

        // Interpolate between maxTurnAngle at standstill and maxTurnAngleAtMaxSpeed at full speed
        float dynamicMaxTurnAngle = Mathf.Lerp(maxTurnAngle, maxTurnAngleAtMaxSpeed, speedFactor);
        
        // Calculate the target steering angle based on the current steer input (-1 to 1)
        float targetTurnAngle = steerInput * dynamicMaxTurnAngle;

        // Smoothly interpolate the current turn angle towards the target turn angle
        currentTurnAngle = Mathf.Lerp(currentTurnAngle, targetTurnAngle, Time.deltaTime * steeringResponse);

        // Apply the calculated steering angle to all front wheels
        foreach (WheelCollider wheel in frontWheelsColliders)
        {
            wheel.steerAngle = currentTurnAngle;
        }
    }


    private void HandleGearShifting()
    {
        float speed = carRigidbody.velocity.magnitude * SpeedToKph;
        engineRPM = speed * gearRatios[currentGear] * 1000;
        if (engineRPM > rpmRange[currentGear])
        {
            IncrementGear();
        }
        else if (engineRPM < rpmRange[currentGear] / 2)
        {
            DecrementGear();
        }
        motorTorque = currentAcceleration * gearRatios[currentGear];
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
        Vector3 position;
        Quaternion rotation;
        wheel.GetWorldPose(out position, out rotation);
        wheelTransform.position = position;
        wheelTransform.rotation = rotation;
    }

    private void AddDownForce()
    {
        carRigidbody.AddForce(Vector3.down * downForce * carRigidbody.velocity.magnitude);
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

    public bool IsGrounded()
    {
        foreach(WheelCollider wheel in frontWheelsColliders)
        {
            if (wheel.isGrounded) return true;
        }
        return false;
    }

    public void IncrementGear()
    {
        if (currentGear < numberOfGears)
        {
            currentGear++;
            onGearChange.Invoke();
        }
    }

    public void DecrementGear()
    {
        if (currentGear > 1)
        {
            currentGear--;
            onGearChange.Invoke();
        }
    }

    public float GetSpeed()
    {
        return carRigidbody.velocity.magnitude * SpeedToKph;
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

    public bool IsHandbraking()
    {
        return isHandbrakeActive;
    }
}