using UnityEngine;

public class CarController : MonoBehaviour
{
    [Header("Car Components")]
    [SerializeField] private Rigidbody carRigidbody;

    [Header("Wheel Colliders")]
    [SerializeField] private WheelCollider frontRight;
    [SerializeField] private WheelCollider frontLeft;
    [SerializeField] private WheelCollider rearRight;
    [SerializeField] private WheelCollider rearLeft;

    [Header("Wheel Transforms")]
    [SerializeField] private Transform frontRightTransform;
    [SerializeField] private Transform frontLeftTransform;
    [SerializeField] private Transform rearRightTransform;
    [SerializeField] private Transform rearLeftTransform;

    [Header("Smoke Particles")]
    [SerializeField] private ParticleSystem firstSmoke;
    [SerializeField] private ParticleSystem secondSmoke;
    [SerializeField] private ParticleSystem thirdSmoke;
    [SerializeField] private ParticleSystem fourthSmoke;

    [Header("Performance Settings")]
    [SerializeField] private float acceleration = 500f;
    [SerializeField] private float reverseAcceleration = 400f;
    [SerializeField] private float brakingForce = 800f;
    [SerializeField] private float maxTurnAngle = 30f;
    [SerializeField] private float handbrakeForce = 2000f;
    [SerializeField] private float maxSpeed = 100f;
    [SerializeField] private float maxTurnAngleAtMaxSpeed = 15f;
    [SerializeField] private float steeringResponse = 5f;
    [SerializeField] private float downForce = 10f;

    private float currentAcceleration = 0f;
    private float currentBrakeForce = 0f;
    private float currentTurnAngle = 0f;
    private bool isHandbrakeActive = false;

    internal enum driveType { FrontWheelDrive, RearWheelDrive, AllWheelDrive }
    private driveType drive = driveType.AllWheelDrive;

    private void FixedUpdate() {
        HandleInput();
        ApplyDrive();
        HandleSteering();
        UpdateWheels();
        HandleSmokeEmission();
        addDownForce();
    }

    private void HandleInput() {
        currentAcceleration = Input.GetAxis("Vertical") * (Input.GetAxis("Vertical") > 0 ? acceleration : reverseAcceleration);
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
            rearRight.brakeTorque = handbrakeForce;
            rearLeft.brakeTorque = handbrakeForce;
        } else {
            rearRight.brakeTorque = currentBrakeForce;
            rearLeft.brakeTorque = currentBrakeForce;
        }
    }

    private void ApplyFrontWheelDrive() {
        frontRight.motorTorque = currentAcceleration;
        frontLeft.motorTorque = currentAcceleration;
    }

    private void ApplyRearWheelDrive() {
        rearRight.motorTorque = currentAcceleration;
        rearLeft.motorTorque = currentAcceleration;
    }

    private void ApplyAllWheelDrive() {
        frontRight.motorTorque = currentAcceleration;
        frontLeft.motorTorque = currentAcceleration;
        rearRight.motorTorque = currentAcceleration;
        rearLeft.motorTorque = currentAcceleration;
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
        UpdateWheel(rearRight, rearRightTransform);
        UpdateWheel(rearLeft, rearLeftTransform);
    }

    private void UpdateWheel(WheelCollider wheel, Transform wheelTransform) {
        wheel.GetWorldPose(out Vector3 pos, out Quaternion rot);
        wheelTransform.position = pos;
        wheelTransform.rotation = rot;
    }

    private void HandleSmokeEmission() {
        bool isMoving = Mathf.Abs(currentAcceleration) > 0.1f;
        EmitSmoke(firstSmoke, isMoving);
        EmitSmoke(secondSmoke, isMoving);
        EmitSmoke(thirdSmoke, isMoving);
        EmitSmoke(fourthSmoke, isMoving);
    }

    private void EmitSmoke(ParticleSystem smoke, bool isMoving) {
        if (isMoving && !smoke.isPlaying) {
            smoke.Play();
        } else if (!isMoving && smoke.isPlaying) {
            smoke.Stop();
        }
    }

    private void addDownForce() {
        carRigidbody.AddForce(-transform.up * downForce * carRigidbody.velocity.magnitude);
    }
}
