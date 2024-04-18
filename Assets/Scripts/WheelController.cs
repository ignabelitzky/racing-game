using UnityEngine;

public class WheelController : MonoBehaviour
{
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

    [Header("Dust Particles")]
    [SerializeField] private ParticleSystem rearRightDust;
    [SerializeField] private ParticleSystem rearLeftDust;

    [Header("Performance Settings")]
    [SerializeField] private float acceleration = 1000f;
    [SerializeField] private float reverseAcceleration = 400f;
    [SerializeField] private float brakingForce = 800f;
    [SerializeField] private float maxTurnAngle = 30f;

    private float currentAcceleration = 0f;
    private float currentBrakeForce = 0f;
    private float currentTurnAngle = 0f;

    private void FixedUpdate() {
        HandleInput();
        ApplyDrive();
        HandleSteering();
        UpdateWheels();
        HandleDustEmission();
    }

    private void HandleInput() {
        currentAcceleration = Input.GetAxis("Vertical") * (Input.GetAxis("Vertical") > 0 ? acceleration : reverseAcceleration);
        currentBrakeForce = Input.GetKey(KeyCode.Space) ? brakingForce : 0f;
    }

    private void ApplyDrive() {
        frontRight.motorTorque = currentAcceleration;
        frontLeft.motorTorque = currentAcceleration;
        rearRight.motorTorque = currentAcceleration;
        rearLeft.motorTorque = currentAcceleration;

        frontRight.brakeTorque = currentBrakeForce;
        frontLeft.brakeTorque = currentBrakeForce;
        rearRight.brakeTorque = currentBrakeForce;
        rearLeft.brakeTorque = currentBrakeForce;
    }

    private void HandleSteering() {
        currentTurnAngle = Input.GetAxis("Horizontal") * maxTurnAngle;
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

    private void HandleDustEmission() {
        bool isMoving = Mathf.Abs(currentAcceleration) > 0.1f || currentBrakeForce > 0.1f;
        EmitDust(rearRightDust, isMoving);
        EmitDust(rearLeftDust, isMoving);
    }

    private void EmitDust(ParticleSystem dust, bool isMoving) {
        if (isMoving && !dust.isPlaying) {
            dust.Play();
        } else if (!isMoving && dust.isPlaying) {
            dust.Stop();
        }
    }
}
