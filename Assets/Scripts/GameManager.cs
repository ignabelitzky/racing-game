using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private Rigidbody carRigidbody;
    [SerializeField] private GameObject speedometerNeedle;

    [Header("Speedometer Settings")]
    [SerializeField] private float needleStartAngle = 220f;
    [SerializeField] private float needleEndAngle = -44f;
    [SerializeField] private float maxSpeed = 180f; // Maximum speed corresponding to the gauge's maximum

    private float vehicleSpeedKmH;

    private void FixedUpdate()
    {
        UpdateNeedle();
    }

    private void UpdateNeedle()
    {
        // Convert velocity from m/s to km/h
        vehicleSpeedKmH = carRigidbody.velocity.magnitude * 3.6f;

        // Calculate the angle of the needle
        float speedNormalized = Mathf.Clamp(vehicleSpeedKmH / maxSpeed, 0f, 1f);
        float needleAngle = Mathf.Lerp(needleStartAngle, needleEndAngle, speedNormalized);

        // Apply the calculated angle to the needle's rotation
        speedometerNeedle.transform.eulerAngles = new Vector3(0, 0, needleAngle);
    }
}
