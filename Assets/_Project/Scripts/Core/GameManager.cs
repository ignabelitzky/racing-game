using System;
using System.ComponentModel.Design;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
public class GameManager : MonoBehaviour
{
    [SerializeField] private GameObject tumbler;
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

    private void Awake()
    {
        tumbler = GameObject.Find("Tumbler");
        if (tumbler == null)
        {
            Debug.LogError("Tumbler not found in the scene. Please assign the Tumbler GameObject in the Inspector.");
        }
        carRigidbody = tumbler.GetComponent<Rigidbody>();
        if (carRigidbody == null)
        {
            Debug.LogError("Rigidbody component not found on the Tumbler GameObject. Please add a Rigidbody component to the Tumbler GameObject.");
        }
        speedometerNeedle = GameObject.Find("SpeedometerNeedle");
        if (speedometerNeedle == null)
        {
            Debug.LogError("SpeedometerNeedle not found in the scene. Please assign the SpeedometerNeedle GameObject in the Inspector.");
        }
    }

    private void FixedUpdate()
    {
        UpdateNeedle();
    }

    private void Update()
    {
    }

    private void UpdateNeedle()
    {
        // Convert velocity from m/s to km/h
        vehicleSpeedKmH = carRigidbody.linearVelocity.magnitude * 3.6f;

        // Calculate the angle of the needle
        float speedNormalized = Mathf.Clamp(vehicleSpeedKmH / maxSpeed, 0f, 1f);
        float needleAngle = Mathf.Lerp(needleStartAngle, needleEndAngle, speedNormalized);

        // Apply the calculated angle to the needle's rotation using Quaternion
        speedometerNeedle.transform.localRotation = Quaternion.Euler(0, 0, needleAngle);
    }

    public void ResetCarPosition(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            carRigidbody.linearVelocity = Vector3.zero;
            carRigidbody.angularVelocity = Vector3.zero;
            carRigidbody.position = resetPosition;
            carRigidbody.rotation = resetRotation;
        }
    }

    public void FlipCar(InputAction.CallbackContext context)
    {
        if (context.started && carRigidbody.linearVelocity.magnitude < 1f)
        {
            Quaternion uprightRotation = Quaternion.LookRotation(carRigidbody.transform.forward, Vector3.up);
            carRigidbody.MoveRotation(uprightRotation);
        }
    }

    public void QuitGame(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
    }
}
