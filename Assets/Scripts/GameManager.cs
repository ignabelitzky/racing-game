using System.ComponentModel.Design;
using UnityEngine;
using UnityEngine.InputSystem;
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
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            QuitGame();
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

    private void QuitGame()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    public void ResetCarPosition(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            carRigidbody.velocity = Vector3.zero;
            carRigidbody.angularVelocity = Vector3.zero;
            carRigidbody.position = resetPosition;
            carRigidbody.rotation = resetRotation;
        }
    }

    public void FlipCar(InputAction.CallbackContext context)
    {
        if (context.started && carRigidbody.velocity.magnitude < 1) {
            Quaternion uprightRotation = Quaternion.LookRotation(carRigidbody.transform.forward, Vector3.up);
            carRigidbody.MoveRotation(uprightRotation);
        }
    }
}
