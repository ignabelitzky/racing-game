using UnityEngine;

public class TumblerEffects : MonoBehaviour
{
    internal const float minSpeed = 0.2f;
    [Header("Tumbler Settings")]
    [SerializeField] private GameObject tumbler;
    private TumblerController tumblerController;

    [Header("Tire Marks")]
    [SerializeField] private TrailRenderer[] tireMarks;
    [SerializeField] private ParticleSystem[] tireSmokes;
    [SerializeField] private WheelCollider[] tireColliders;
    private bool tireMarksFlag = false;
    private float currentSpeed = 0f;

    private void Awake()
    {
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

    // Update is called once per frame
    void Update()
    {
        UpdateEffects();  
    }

    private void UpdateEffects()
    {
        currentSpeed = tumblerController.GetSpeed();

        if (!tumblerController.IsGrounded())
        {
            StopEmitter();
            foreach (ParticleSystem smoke in tireSmokes)
            {
                smoke.Stop();
            }
            return;
        }

        if (tumblerController.IsHandbraking() && currentSpeed > minSpeed)
        {
            StartEmitter();
        }
        else
        {
            StopEmitter();
        }
    }

    private void StartEmitter()
    {
        if (!tireMarksFlag)
        {
            for (int i = 0; i < tireMarks.Length; i++)
            {
                if (tireColliders[i].isGrounded)
                {
                    tireMarks[i].emitting = true;
                    if (i > 1)
                    {
                        tireSmokes[i - 2].Play();
                    }
                }
            }
            tireMarksFlag = true;
        }
    }

    private void StopEmitter()
    {
        if (tireMarksFlag)
        {
            foreach (TrailRenderer trail in tireMarks)
            {
                trail.emitting = false;
            }
            foreach (ParticleSystem smoke in tireSmokes)
            {
                smoke.Stop();
            }
            tireMarksFlag = false;
        }
    }
}