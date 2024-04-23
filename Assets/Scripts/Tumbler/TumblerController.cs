using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class TumblerController : MonoBehaviour
{
    internal enum driveType { FrontWheelDrive, RearWheelDrive, AllWheelDrive }

    internal const float SpeedToKph = 3.6f;

    internal bool isHandbrakeActive = false;
    internal float steerInput = 0f;

    public Rigidbody carRigidbody;
    public TumblerInput tumblerInput;

    [Header("Wheel Colliders")]
    [SerializeField] private WheelCollider[] frontWheelsColliders;
    [SerializeField] private WheelCollider[] rearWheelsColliders;

    [Header("Wheel Transforms")]
    [SerializeField] private Transform[] wheelsTransforms;

    [Header("Performance Settings")]
    [SerializeField] private float acceleration = 1000f;
    [SerializeField] private float reverseAcceleration = 600f;
    [SerializeField] private float brakingForce = 800f;
    [SerializeField] private float maxTurnAngle = 40f;
    [SerializeField] private float handbrakeForce = 4000f;
    [SerializeField] private float maxSpeedKPH = 175f;
    [SerializeField] private float maxTurnAngleAtMaxSpeed = 20f;
    [SerializeField] private float steeringResponse = 5f;
    [SerializeField] private float downForce = 150f;

    [Header("Gearbox Settings")]
    [SerializeField] private int numberOfGears = 5;
    private int currentGear = 1;
    private float[] rpmRange = { 0f, 42000, 105000, 187000, 182000, 176000};
    private float[] gearRatios = { 0f, 1.2f, 1.5f, 1.7f, 1.3f, 1.1f };
    private float engineRPM = 0f;
    private float motorTorque = 0f;

    [Header("Car Modes")]
    [SerializeField] private driveType drive = driveType.AllWheelDrive;

    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI driveModeText;
    [SerializeField] private TextMeshProUGUI gearText;

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
        UpdateDriveModeUI();

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
        currentAcceleration = throttleInput * (throttleInput > 0 ? acceleration : reverseAcceleration);
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

        switch (drive) {
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
        if (engineRPM > rpmRange[currentGear] && currentGear < numberOfGears)
        {
            currentGear++;
        } else if (engineRPM < rpmRange[currentGear] / 2 && currentGear > 1)
        {
            currentGear--;
        }
        motorTorque = currentAcceleration * gearRatios[currentGear];
        gearText.text = currentGear.ToString();
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
            switch (drive)
            {
                case driveType.FrontWheelDrive:
                    drive = driveType.RearWheelDrive;
                    break;
                case driveType.RearWheelDrive:
                    drive = driveType.AllWheelDrive;
                    break;
                case driveType.AllWheelDrive:
                    drive = driveType.FrontWheelDrive;
                    break;
            }
            ResetMotorTorques();
            UpdateDriveModeUI();
        }
    }

    private void ResetMotorTorques()
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

    private void UpdateDriveModeUI()
    {
        if (driveModeText != null)
        {
            switch(drive)
            {
                case driveType.FrontWheelDrive:
                    driveModeText.text = "FWD";
                    break;
                case driveType.RearWheelDrive:
                    driveModeText.text = "RWD";
                    break;
                case driveType.AllWheelDrive:
                    driveModeText.text = "AWD";
                    break;
            }
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