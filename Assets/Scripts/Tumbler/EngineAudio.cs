using UnityEngine;

public class EngineAudio : MonoBehaviour
{
    internal float maxSpeed;
    internal float currentSpeed;

    [Header("Tumbler Settings")]
    [SerializeField] private GameObject tumbler;

    [Header("Audio Components")]
    [SerializeField] private AudioSource startingSound;
    [SerializeField] private AudioSource runningSound;
    [SerializeField] private AudioSource idleSound;
    [SerializeField] private AudioSource tireScreechSound;

    [Header("Audio Settings")]
    [SerializeField] private float runningPitch = 0f;
    [SerializeField] private float runningMaxPitch = 1.5f;

    private TumblerController tumblerController;
    private bool isTireScreeching = false;

    private void Awake()
    {
        // Error handling for audio sources
        CheckAudioSource(startingSound, "StartingSound");
        CheckAudioSource(runningSound, "RunningSound");
        CheckAudioSource(idleSound, "IdleSound");
        CheckAudioSource(tireScreechSound, "TireScreechSound");

        // Early initialization
        if (tumbler != null)
        {
            tumblerController = tumbler.GetComponent<TumblerController>();
            if (tumblerController == null)
            {
                Debug.LogError($"TumblerController component not found on {tumbler.name}");
            }
        }
        else
        {
            Debug.LogError("Tumbler object not assigned or found in scene");
        }
    }

    void Start()
    {
        // Start audio after all components are validated
        if (startingSound != null) startingSound.Play();
        if (idleSound != null) idleSound.Play();
        if (runningSound != null)
        {
            runningSound.Play();
            runningSound.pitch = runningPitch;
        }
    }

    void Update()
    {
        if (tumblerController == null) return;

        UpdateEngineSound();
        UpdateTireScreech();
    }

    private void UpdateEngineSound()
    {
        maxSpeed = tumblerController.GetMaxSpeed();
        currentSpeed = tumblerController.GetSpeed();
        float pitch = Mathf.Lerp(runningPitch, runningMaxPitch, currentSpeed / maxSpeed);
        runningSound.pitch = pitch;
    }

    private void UpdateTireScreech()
    {
        bool isCurrentlyHandbraking = tumblerController.IsHandbraking() && currentSpeed > 0.25f;
        if (isCurrentlyHandbraking && !isTireScreeching)
        {
            tireScreechSound.Play();
            isTireScreeching = true;
        }
        else if (!isCurrentlyHandbraking && isTireScreeching)
        {
            tireScreechSound.Stop();
            isTireScreeching = false;
        }
    }

    private void CheckAudioSource(AudioSource source, string name)
    {
        if (source == null)
        {
            Debug.LogError($"No {name} assigned on {gameObject.name}");
        }
    }
}