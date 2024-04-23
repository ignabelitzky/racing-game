using System.Collections;
using UnityEngine;

public class EngineAudio : MonoBehaviour
{
    internal float maxSpeed;
    internal float currentSpeed;
    internal float minBrakeSpeed = 0.2f;
    internal bool tireScreechSoundIsPlaying = false;

    [Header("Tumbler Settings")]
    [SerializeField] private GameObject tumbler;

    [Header("Audio Components")]
    [SerializeField] private AudioSource startingSound;
    [SerializeField] private AudioSource runningSound;
    [SerializeField] private AudioSource idleSound;
    [SerializeField] private AudioSource tireScreechSound;

    [Header("Audio Settings")]
    [SerializeField] private float runningPitch = 0.1f;
    [SerializeField] private float runningMaxPitch = 1.5f;

    private TumblerController tumblerController;

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
        StartCoroutine(PlayEngineStartSequence());
        maxSpeed = tumblerController.GetMaxSpeed();
    }

    void Update()
    {
        UpdateEngineSound();
        UpdateTireScreech();
    }

    private IEnumerator PlayEngineStartSequence()
    {
        if(startingSound != null)
        {
            startingSound.Play();
            yield return new WaitForSeconds(startingSound.clip.length - 1f);
        }
        if(idleSound != null)
        {
            idleSound.Play();
        }
        if (runningSound != null)
        {
            runningSound.Play();
            runningSound.pitch = runningPitch;
        }
    }

    private void UpdateEngineSound()
    {
        currentSpeed = tumblerController.GetSpeed();
        runningSound.pitch = Mathf.Lerp(runningPitch, runningMaxPitch, currentSpeed / maxSpeed);
    }

    private void UpdateTireScreech()
    {
        if (!tireScreechSoundIsPlaying && tumblerController.IsGrounded() && tumblerController.IsHandbraking() && currentSpeed > minBrakeSpeed)
        {
            tireScreechSound.Play();
            tireScreechSoundIsPlaying = true;
        }
        else if (((!tumblerController.IsHandbraking() || currentSpeed < minBrakeSpeed) && tireScreechSoundIsPlaying) || !tumblerController.IsGrounded())
        {
            tireScreechSound.Stop();
            tireScreechSoundIsPlaying = false;
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