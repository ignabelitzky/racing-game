using UnityEngine;
using UnityEngine.InputSystem;

public class Boost : MonoBehaviour
{
    internal bool isBoostActive = false;
    public Rigidbody carRigidbody;
    public TumblerInput tumblerInput;
    [Header("Boost Settings")]
    [SerializeField] private float boostForce = 120f;
    [SerializeField] private ParticleSystem boostParticles;
    [SerializeField] private AudioSource jetEngineSound;

    private void Awake()
    {
        tumblerInput = new TumblerInput();
        if (tumblerInput == null)
        {
            Debug.LogError("TumblerInput component not found on " + gameObject.name);
        }
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
        if (jetEngineSound == null)
        {
            Debug.LogError("No jet engine sound assigned on " + gameObject.name);
        }
    }

    void FixedUpdate()
    {
        if (isBoostActive)
        {
            carRigidbody.AddForce(transform.forward * boostForce, ForceMode.Impulse);
        }
    }

    private void Update()
    {
        isBoostActive = tumblerInput.Gameplay.Boost.ReadValue<float>() > 0f;
        if(isBoostActive && !boostParticles.isPlaying)
        {
            boostParticles.Play();
            jetEngineSound.Play();
        }
        else if (!isBoostActive && boostParticles.isPlaying)
        {
            boostParticles.Stop();
            jetEngineSound.Stop();
        }
    }
}