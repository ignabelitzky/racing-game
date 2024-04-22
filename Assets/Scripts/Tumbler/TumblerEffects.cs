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
    private bool tireMarksFlag = false;
    private float currentSpeed = 0f;

    private void Awake()
    {
        if(tumbler != null)
        {
            tumblerController = tumbler.GetComponent<TumblerController>();
            if(tumblerController == null)
            {
                Debug.LogError($"TumblerController component not found on {tumbler.name}");
            }
        }
        else
        {
            Debug.LogError("Tumbler object not assigned or found in scene");
        }
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        CheckDrift();   
    }

    private void CheckDrift()
    {
        currentSpeed = tumblerController.GetSpeed();
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
            foreach(TrailRenderer trail in tireMarks)
            {
                trail.emitting = true;
            }
            foreach(ParticleSystem smoke in tireSmokes)
            {
                smoke.Play();
            }
            tireMarksFlag = true;
        }
    }

    private void StopEmitter()
    {
        if (tireMarksFlag)
        {
            foreach(TrailRenderer trail in tireMarks)
            {
                trail.emitting = false;
            }
            foreach(ParticleSystem smoke in tireSmokes)
            {
                smoke.Stop();
            }
            tireMarksFlag = false;
        }
    }
}
