using UnityEngine;
using UnityEngine.InputSystem;

public class Boost : MonoBehaviour
{
    public Rigidbody carRigidbody;
    public TumblerInput tumblerInput;
    [Header("Boost Settings")]
    [SerializeField] private float boostForce = 2000f;
    [SerializeField] private ParticleSystem boostParticles;
    [SerializeField] private bool isBoosting = false;

    private void Awake()
    {
        tumblerInput = new TumblerInput();
    }

    private void OnEnable()
    {
        tumblerInput.Enable();
    }

    private void OnDisable()
    {
        tumblerInput.Disable();
    }

   private void Start()
    {
        carRigidbody = GetComponent<Rigidbody>();
        if (carRigidbody == null)
        {
            Debug.LogError("Rigidbody component not found on " + gameObject.name);
        }

        if (boostParticles == null)
        {
            Debug.LogError("No boost particles assigned on " + gameObject.name);
        }
    }

    void FixedUpdate()
    {
        if (isBoosting)
        {
            carRigidbody.AddForce(transform.forward * boostForce, ForceMode.Impulse);
        }
    }

    private void Update()
    {
        isBoosting = tumblerInput.Tumbler.Boost.ReadValue<float>() > 0f;
        if(isBoosting && !boostParticles.isPlaying)
        {
            boostParticles.Play();
        }
        else if (!isBoosting && boostParticles.isPlaying)
        {
            boostParticles.Stop();
        }
    }
}