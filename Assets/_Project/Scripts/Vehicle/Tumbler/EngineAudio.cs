using System.Collections;
using UnityEngine;

public class EngineAudio : MonoBehaviour
{
    internal float currentSpeed;
    internal float minBrakeSpeed = 0.2f;
    internal bool tireScreechSoundIsPlaying = false;

    [Header("Tumbler Settings")]
    [SerializeField] private GameObject tumbler;
    [SerializeField] private WheelCollider[] tireColliders;

    [Header("Audio Components")]
    [SerializeField] private AudioSource startingSound;
    [SerializeField] private AudioSource runningSound;
    [SerializeField] private AudioSource idleSound;
    [SerializeField] private AudioSource[] tireScreechSounds;

    [Header("Audio Settings")]
    [SerializeField] private float runningPitch = 0.65f;
    [SerializeField] private float runningMaxPitch = 1.8f;

    private TumblerController tumblerController;

    private void Awake()
    {
        // Error handling for audio sources
        CheckAudioSource(startingSound, "StartingSound");
        CheckAudioSource(runningSound, "RunningSound");
        CheckAudioSource(idleSound, "IdleSound");
        foreach (AudioSource tireScreechSound in tireScreechSounds)
        {
            CheckAudioSource(tireScreechSound, "TireScreechSound");
        }

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
        foreach(AudioSource tireScreechSound in tireScreechSounds)
        {
            tireScreechSound.volume = 0.3f;
        }
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
            if (startingSound.clip != null) yield return new WaitForSeconds(Mathf.Max(0f, startingSound.clip.length - 1f));
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
        if (runningSound == null) return;
        float revs = tumblerController.GetNormalizedEngineRPM();
        runningSound.pitch = Mathf.Lerp(runningPitch, runningMaxPitch, revs);
        runningSound.volume = Mathf.Lerp(0.35f, 0.85f, revs);
        if (idleSound != null) idleSound.volume = Mathf.Lerp(0.55f, 0.12f, revs);
    }

    private void UpdateTireScreech()
    {
        if (!tireScreechSoundIsPlaying && tumblerController.IsGrounded() && tumblerController.IsHandbraking() && currentSpeed > minBrakeSpeed)
        {
            for (int i = 0; i < tireColliders.Length; i++)
            {
                if (tireColliders[i].isGrounded && !tireScreechSounds[i].isPlaying)
                {
                    tireScreechSounds[i].Play();
                }
                else
                {
                    tireScreechSounds[i].Stop();
                }
                tireScreechSoundIsPlaying = true;
            }
        }
        else if (((!tumblerController.IsHandbraking() || currentSpeed < minBrakeSpeed) && tireScreechSoundIsPlaying) || !tumblerController.IsGrounded())
        {
            foreach (AudioSource tireScreechSound in tireScreechSounds)
            {
                tireScreechSound.Stop();
            }
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
