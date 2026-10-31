using UnityEngine;

/// <summary>Finite turbo reserve with delayed recharge and faster airborne recovery.</summary>
public class Boost : MonoBehaviour
{
    internal bool isBoostActive;
    public Rigidbody carRigidbody;
    public TumblerInput tumblerInput;
    [Header("Boost Settings")]
    [SerializeField] private float boostForce = 120f;
    [SerializeField] private ParticleSystem boostParticles;
    [SerializeField] private AudioSource jetEngineSound;
    [SerializeField, Min(.1f)] private float capacitySeconds = 6f;
    [SerializeField, Min(0)] private float rechargeDelay = 2.5f;
    [SerializeField, Min(.1f)] private float groundRechargeSeconds = 12f;
    [SerializeField, Min(1)] private float airborneRechargeMultiplier = 2.5f;
    private TumblerController controller;
    private TumblerSystems systems;
    private float reserve = 1f, delayRemaining;
    private bool releaseRequired;
    public float Reserve01 => reserve;
    public float RechargeDelayRemaining => delayRemaining;
    public bool IsBoostActive => isBoostActive;
    public bool IsRecharging => !isBoostActive && reserve < 1f && delayRemaining <= 0f;
    public ParticleSystem Particles => boostParticles;
    public AudioSource JetAudio => jetEngineSound;
    private void Awake()
    {
        tumblerInput = new TumblerInput(); carRigidbody = GetComponent<Rigidbody>();
        controller = GetComponent<TumblerController>(); systems = GetComponent<TumblerSystems>();
    }
    private void OnEnable() => tumblerInput.Enable();
    private void OnDisable() { tumblerInput.Disable(); SetActive(false); }
    private void OnDestroy() => tumblerInput?.Dispose();
    private void Update()
    {
        bool held = tumblerInput.Gameplay.Boost.IsPressed();
        if (tumblerInput.Gameplay.Boost.WasPressedThisFrame() && systems != null && systems.IsStealth) systems.SetStealth(false);
        AdvanceReserve(Time.deltaTime, held, controller != null && !controller.IsGrounded());
    }
    private void AdvanceReserve(float deltaTime, bool held, bool airborne)
    {
        if (deltaTime <= 0) return;
        if (!held) releaseRequired = false;
        bool active = held && !releaseRequired && reserve > 0f && (systems == null || !systems.IsStealth);
        if (active)
        {
            reserve = Mathf.Max(0f, reserve - deltaTime / Mathf.Max(.1f, capacitySeconds));
            if (reserve < .00001f) reserve = 0f;
            delayRemaining = rechargeDelay;
            if (reserve <= 0f) { releaseRequired = true; active = false; }
        }
        else
        {
            float rechargeTime = Mathf.Max(0f, deltaTime - delayRemaining);
            delayRemaining = Mathf.Max(0f, delayRemaining - deltaTime);
            reserve = Mathf.Min(1f, reserve + rechargeTime / Mathf.Max(.1f, groundRechargeSeconds) *
                (airborne ? airborneRechargeMultiplier : 1f));
        }
        SetActive(active);
    }
    private void FixedUpdate()
    {
        if (isBoostActive && carRigidbody != null)
            carRigidbody.AddForce(transform.forward * (boostForce * Time.fixedDeltaTime / .02f), ForceMode.Impulse);
    }
    private void SetActive(bool active)
    {
        isBoostActive = active;
        if (boostParticles != null)
        {
            if (active && !boostParticles.isPlaying) boostParticles.Play();
            else if (!active && boostParticles.isPlaying) boostParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        if (jetEngineSound != null)
        {
            if (active && !jetEngineSound.isPlaying) jetEngineSound.Play();
            else if (!active && jetEngineSound.isPlaying) jetEngineSound.Stop();
        }
    }
}
