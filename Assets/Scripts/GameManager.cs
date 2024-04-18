using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private Rigidbody carRigidbody;
    [SerializeField] private GameObject speedometerNeedle;

    [Header("Speedometer Settings")]
    [SerializeField] private float needleStartAngle = 220f;
    [SerializeField] private float needleEndAngle = 0f;
    [SerializeField] private float maxSpeed = 180f;

    [Header("Reset Settings")]
    [SerializeField] private Vector3 resetPosition = new Vector3(0, 0.5f, 0);
    [SerializeField] private Quaternion resetRotation = Quaternion.identity;

    private float vehicleSpeedKmH;

    private void FixedUpdate()
    {
        UpdateNeedle();
        if (carRigidbody.position.y < -1f)
        {
            ResetCarPosition();
        }
    }

    private void UpdateNeedle()
    {
        // Convert velocity from m/s to km/h
        vehicleSpeedKmH = carRigidbody.velocity.magnitude * 3.6f;

        // Calculate the angle of the needle
        float speedNormalized = Mathf.Clamp(vehicleSpeedKmH / maxSpeed, 0f, 1f);
        float needleAngle = Mathf.Lerp(needleStartAngle, needleEndAngle, speedNormalized);

        // Apply the calculated angle to the needle's rotation using Quaternion
        speedometerNeedle.transform.localRotation = Quaternion.Euler(0, 0, needleAngle);
    }

    private void ResetCarPosition()
    {
        carRigidbody.velocity = Vector3.zero;
        carRigidbody.angularVelocity = Vector3.zero;
        carRigidbody.position = resetPosition;
        carRigidbody.rotation = resetRotation;
    }
}
