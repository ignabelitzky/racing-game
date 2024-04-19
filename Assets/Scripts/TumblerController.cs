using TMPro;
using UnityEngine;

public class TumblerController : MonoBehaviour
{
    internal enum driveType { FrontWheelDrive, RearWheelDrive, AllWheelDrive }

    internal const float SpeedToKph = 3.6f;

    [Header("Car Components")]
    [SerializeField] private Rigidbody carRigidbody;

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
    [SerializeField] private float maxSpeed = 100f;
    [SerializeField] private float maxTurnAngleAtMaxSpeed = 15f;
    [SerializeField] private float steeringResponse = 5f;
    [SerializeField] private float downForce = 10f;

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
    private bool isHandbrakeActive = false;

    private void Start() {
        UpdateDriveModeUI();
    }
    private void FixedUpdate() {
        HandleInput();
        ApplyDrive();
        HandleSteering();
        UpdateWheels();
        AddDownForce();
    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.LeftAlt)) {
            SwitchDriveMode();
        }
    }

    private void HandleInput() {
        float verticalInput = Input.GetAxis("Vertical");
        currentAcceleration = verticalInput * (verticalInput > 0 ? acceleration : reverseAcceleration);
        currentBrakeForce = Input.GetKey(KeyCode.LeftControl) ? brakingForce : 0f;
        isHandbrakeActive = Input.GetKey(KeyCode.LeftShift);
    }

    private void ApplyDrive() {
        HandleGearShifting();

        switch (drive) {
            case driveType.FrontWheelDrive:
                ApplyFrontWheelDrive();
                break;
            case driveType.RearWheelDrive:
                ApplyRearWheelDrive();
                break;
            case driveType.AllWheelDrive:
                ApplyAllWheelDrive();
                break;
        }

        foreach(WheelCollider wheel in frontWheelsColliders) {
            ApplyBraking(wheel);
        }

        if(isHandbrakeActive) {
            ApplyHandbrake();
        } else {
            foreach(WheelCollider wheel in rearWheelsColliders) {
                ApplyBraking(wheel);
            }
        }
    }

    private void ApplyBraking(WheelCollider wheel) {
        wheel.brakeTorque = currentBrakeForce;
    }

    private void ApplyHandbrake() {
        foreach(WheelCollider wheel in rearWheelsColliders) {
            wheel.brakeTorque = handbrakeForce;
        }
    }

    private void ApplyFrontWheelDrive() {
        foreach(WheelCollider wheel in frontWheelsColliders) {
            wheel.motorTorque = motorTorque;
        }
    }

    private void ApplyRearWheelDrive() {
        foreach(WheelCollider wheel in rearWheelsColliders) {
            wheel.motorTorque = motorTorque;
        }
    }

    private void ApplyAllWheelDrive() {
        foreach(WheelCollider wheel in frontWheelsColliders) {
            wheel.motorTorque = motorTorque;
        }
        foreach(WheelCollider wheel in rearWheelsColliders) {
            wheel.motorTorque = motorTorque;
        }
    }

    private void HandleSteering() {
        float speedFactor = Mathf.Clamp01(carRigidbody.velocity.magnitude / maxSpeed);
        float dynamicMaxTurnAngle = Mathf.Lerp(maxTurnAngleAtMaxSpeed, maxTurnAngle, 1 - speedFactor);
        float targetTurnAngle = Input.GetAxis("Horizontal") * dynamicMaxTurnAngle;
        currentTurnAngle = Mathf.Lerp(currentTurnAngle, targetTurnAngle, Time.deltaTime * steeringResponse);

        foreach(WheelCollider wheel in frontWheelsColliders) {
            wheel.steerAngle = currentTurnAngle;
        }
    }

    private void HandleGearShifting() {
        float speed = carRigidbody.velocity.magnitude * SpeedToKph;
        engineRPM = speed * gearRatios[currentGear] * 1000;
        if (engineRPM > rpmRange[currentGear] && currentGear < numberOfGears) {
            currentGear++;
        } else if (engineRPM < rpmRange[currentGear] / 2 && currentGear > 1) {
            currentGear--;
        }
        motorTorque = currentAcceleration * gearRatios[currentGear];
        gearText.text = currentGear.ToString();
    }

    private void UpdateWheels() {
        for (int i = 0; i < frontWheelsColliders.Length; i++) {
            UpdateWheel(frontWheelsColliders[i], wheelsTransforms[i]);
        }
        for (int i = 0; i < rearWheelsColliders.Length; i++) {
            UpdateWheel(rearWheelsColliders[i], wheelsTransforms[i + frontWheelsColliders.Length]);
        }
    }

    private void UpdateWheel(WheelCollider wheel, Transform wheelTransform) {
        Vector3 position;
        Quaternion rotation;
        wheel.GetWorldPose(out position, out rotation);
        wheelTransform.position = position;
        wheelTransform.rotation = rotation;
    }

    private void AddDownForce() {
        carRigidbody.AddForce(Vector3.down * downForce * carRigidbody.velocity.magnitude);
    }

    private void SwitchDriveMode() {
        switch (drive) {
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

    private void ResetMotorTorques() {
        motorTorque = 0f;
        foreach(WheelCollider wheel in frontWheelsColliders) {
            wheel.motorTorque = motorTorque;
        }
        foreach(WheelCollider wheel in rearWheelsColliders) {
            wheel.motorTorque = motorTorque;
        }
    }

    private void UpdateDriveModeUI() {
        if (driveModeText != null) {
            switch(drive) {
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
}
