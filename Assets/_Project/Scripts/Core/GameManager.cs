using UnityEngine;
using UnityEngine.InputSystem;
public class GameManager : MonoBehaviour
{
    [SerializeField] private GameObject tumbler;
    [SerializeField] private Rigidbody carRigidbody;

    [Header("Reset Settings")]
    [SerializeField] private Vector3 resetPosition = new Vector3(0, 0.5f, 0);
    [SerializeField] private Quaternion resetRotation = Quaternion.identity;

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
    }

    public void ResetCarPosition(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            carRigidbody.linearVelocity = Vector3.zero;
            carRigidbody.angularVelocity = Vector3.zero;
            carRigidbody.position = resetPosition;
            carRigidbody.rotation = resetRotation;
            var controller = carRigidbody.GetComponent<TumblerController>();
            if (controller != null) controller.ResetTransmission();
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
