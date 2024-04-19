using System.ComponentModel.Design;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private Rigidbody carRigidbody;
    [SerializeField] private GameObject speedometerNeedle;

    [Header("Speedometer Settings")]
    [SerializeField] private float needleStartAngle = 217f;
    [SerializeField] private float needleEndAngle = -40f;
    [SerializeField] private float maxSpeed = 180f;

    [Header("Reset Settings")]
    [SerializeField] private Vector3 resetPosition = new Vector3(0, 0.5f, 0);
    [SerializeField] private Quaternion resetRotation = Quaternion.identity;

    private float vehicleSpeedKmH;

    private void FixedUpdate()
    {
        UpdateNeedle();
        CheckAndResetCarPosition();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetCarPosition();
        }
        if (Input.GetKeyDown(KeyCode.F))
        {
            FlipCar();
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

    private void CheckAndResetCarPosition()
    {
        if (carRigidbody.position.y < -1)
        {
            ResetCarPosition();
        }
    }

    private void ResetCarPosition()
    {
        carRigidbody.velocity = Vector3.zero;
        carRigidbody.angularVelocity = Vector3.zero;
        carRigidbody.position = resetPosition;
        carRigidbody.rotation = resetRotation;
    }

    private void FlipCar()
    {
        Quaternion uprightRotation = Quaternion.LookRotation(carRigidbody.transform.forward, Vector3.up);
        carRigidbody.MoveRotation(uprightRotation);
    }
}
