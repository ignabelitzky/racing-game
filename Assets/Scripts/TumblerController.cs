using TMPro;
using UnityEngine;

public class TumblerController : MonoBehaviour
{
    internal enum driveType { FrontWheelDrive, RearWheelDrive, AllWheelDrive }

    [Header("Car Components")]
    [SerializeField] private Rigidbody carRigidbody;

    [Header("Wheel Colliders")]
    [SerializeField] private WheelCollider frontRight;
    [SerializeField] private WheelCollider frontLeft;
    [SerializeField] private WheelCollider rearRightOutside;
    [SerializeField] private WheelCollider rearRightInside;
    [SerializeField] private WheelCollider rearLeftOutside;
    [SerializeField] private WheelCollider rearLeftInside;

    [Header("Wheel Transforms")]
    [SerializeField] private Transform frontRightTransform;
    [SerializeField] private Transform frontLeftTransform;
    [SerializeField] private Transform rearRightOutsideTransform;
    [SerializeField] private Transform rearRightInsideTransform;
    [SerializeField] private Transform rearLeftOutsideTransform;
    [SerializeField] private Transform rearLeftInsideTransform;

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

    [Header("Car Modes")]
    [SerializeField] private driveType drive = driveType.AllWheelDrive;

    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI driveModeText;

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

        frontRight.brakeTorque = currentBrakeForce;
        frontLeft.brakeTorque = currentBrakeForce;

        if(isHandbrakeActive) {
            rearRightOutside.brakeTorque = handbrakeForce;
            rearRightInside.brakeTorque = handbrakeForce;
            rearLeftOutside.brakeTorque = handbrakeForce;
            rearLeftInside.brakeTorque = handbrakeForce;
        } else {
            rearRightOutside.brakeTorque = currentBrakeForce;
            rearRightInside.brakeTorque = currentBrakeForce;
            rearLeftOutside.brakeTorque = currentBrakeForce;
            rearLeftInside.brakeTorque = currentBrakeForce;
        }
    }

    private void ApplyFrontWheelDrive() {
        frontRight.motorTorque = currentAcceleration;
        frontLeft.motorTorque = currentAcceleration;
    }

    private void ApplyRearWheelDrive() {
        rearRightOutside.motorTorque = currentAcceleration;
        rearRightInside.motorTorque = currentAcceleration;
        rearLeftOutside.motorTorque = currentAcceleration;
        rearLeftInside.motorTorque = currentAcceleration;
    }

    private void ApplyAllWheelDrive() {
        frontRight.motorTorque = currentAcceleration;
        frontLeft.motorTorque = currentAcceleration;
        rearRightOutside.motorTorque = currentAcceleration;
        rearRightInside.motorTorque = currentAcceleration;
        rearLeftOutside.motorTorque = currentAcceleration;
        rearLeftInside.motorTorque = currentAcceleration;
    }

    private void HandleSteering() {
        float speedFactor = Mathf.Clamp01(carRigidbody.velocity.magnitude / maxSpeed);
        float dynamicMaxTurnAngle = Mathf.Lerp(maxTurnAngleAtMaxSpeed, maxTurnAngle, 1 - speedFactor);
        float targetTurnAngle = Input.GetAxis("Horizontal") * dynamicMaxTurnAngle;
        currentTurnAngle = Mathf.Lerp(currentTurnAngle, targetTurnAngle, Time.deltaTime * steeringResponse);

        frontRight.steerAngle = currentTurnAngle;
        frontLeft.steerAngle = currentTurnAngle;
    }

    private void UpdateWheels() {
        UpdateWheel(frontRight, frontRightTransform);
        UpdateWheel(frontLeft, frontLeftTransform);
        UpdateWheel(rearRightOutside, rearRightOutsideTransform);
        UpdateWheel(rearRightInside, rearRightInsideTransform);
        UpdateWheel(rearLeftOutside, rearLeftOutsideTransform);
        UpdateWheel(rearLeftInside, rearLeftInsideTransform);
    }

    private void UpdateWheel(WheelCollider wheel, Transform wheelTransform) {
        Vector3 position;
        Quaternion rotation;
        wheel.GetWorldPose(out position, out rotation);
        wheelTransform.position = position;
        wheelTransform.rotation = rotation;
    }

    private void AddDownForce() {
        carRigidbody.AddForce(-transform.up * downForce * carRigidbody.velocity.magnitude);
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
        UpdateDriveModeUI();
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
