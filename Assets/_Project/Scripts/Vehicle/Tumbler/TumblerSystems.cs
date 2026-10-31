using UnityEngine;

/// <summary>Headlights and reversible visual/audio stealth; driving remains available.</summary>
[DefaultExecutionOrder(-50)]
public sealed class TumblerSystems : MonoBehaviour
{
    public enum LightMode { Off, Low, High }
    [SerializeField] private LightMode selectedLights;
    [SerializeField] private Light[] headlights = new Light[0];
    [SerializeField] private Renderer[] headlampLenses = new Renderer[0];
    [SerializeField] private Light[] lowBeamLamps = new Light[0];
    [SerializeField] private ParticleSystem[] turbineParticles = new ParticleSystem[0];
    [SerializeField] private Behaviour volumetricHaze;
    [SerializeField, Min(0)] private float lowBeamIntensity = 30000f;
    [SerializeField, Min(0)] private float highBeamIntensity = 90000f;
    private TumblerInput input;
    private EngineAudio engine;
    private Boost boost;
    private Light[] vehicleLights;
    private bool[] lightStates, particleStates;
    private MaterialPropertyBlock block;
    private static readonly int Emissive = Shader.PropertyToID("_EmissiveColor");
    public bool IsStealth { get; private set; }
    public LightMode SelectedLights => selectedLights;
    public LightMode EffectiveLights => IsStealth ? LightMode.Off : selectedLights;
    private void Awake()
    {
        input = new TumblerInput(); engine = GetComponent<EngineAudio>(); boost = GetComponent<Boost>();
        block = new MaterialPropertyBlock(); vehicleLights = GetComponentsInChildren<Light>(true);
        lightStates = new bool[vehicleLights.Length]; particleStates = new bool[turbineParticles.Length];
        RefreshLights();
    }
    private void OnEnable() { input.Enable(); RefreshLights(); }
    private void OnDisable()
    {
        input.Disable();
        if (IsStealth)
        {
            IsStealth = false;
            if (engine != null) engine.SetStealthMuted(false);
            for (int i = 0; i < vehicleLights.Length; i++) if (vehicleLights[i] != null) vehicleLights[i].enabled = lightStates[i];
        }
        foreach (var light in headlights) if (light != null) light.enabled = false;
        if (volumetricHaze != null) volumetricHaze.enabled = false;
    }
    private void OnDestroy() => input?.Dispose();
    private void Update()
    {
        if (input.Gameplay.ToggleStealth.WasPressedThisFrame()) SetStealth(!IsStealth);
        if (input.Gameplay.CycleLights.WasPressedThisFrame()) CycleLights();
    }
    public void CycleLights()
    {
        if (IsStealth) return;
        SetLightMode((LightMode)(((int)selectedLights + 1) % 3));
    }
    public void SetLightMode(LightMode mode)
    {
        selectedLights = mode;
        if (block == null) block = new MaterialPropertyBlock();
        RefreshLights();
    }
    public void SetStealth(bool active)
    {
        if (IsStealth == active) return;
        if (active)
        {
            for (int i = 0; i < vehicleLights.Length; i++)
            { lightStates[i] = vehicleLights[i] != null && vehicleLights[i].enabled; if (vehicleLights[i] != null) vehicleLights[i].enabled = false; }
            for (int i = 0; i < turbineParticles.Length; i++)
            {
                var p = turbineParticles[i]; if (p == null) continue;
                particleStates[i] = p.isPlaying; p.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
        IsStealth = active;
        if (engine != null) engine.SetStealthMuted(active);
        if (!active)
        {
            for (int i = 0; i < vehicleLights.Length; i++) if (vehicleLights[i] != null) vehicleLights[i].enabled = lightStates[i];
            for (int i = 0; i < turbineParticles.Length; i++)
            {
                var p = turbineParticles[i]; if (p == null || !particleStates[i] || !p.gameObject.activeInHierarchy) continue;
                if (boost != null && boost.Particles != null && p.transform.IsChildOf(boost.Particles.transform)) continue;
                if (!p.isPlaying) p.Play(false);
            }
        }
        RefreshLights();
    }
    private void RefreshLights()
    {
        var mode = EffectiveLights;
        for (int i = 0; i < headlights.Length; i++)
        {
            var light = headlights[i];
            bool on = mode == LightMode.High || (mode == LightMode.Low && System.Array.IndexOf(lowBeamLamps, light) >= 0);
            if (light != null)
            {
                light.enabled = on; light.intensity = mode == LightMode.High ? highBeamIntensity : lowBeamIntensity;
                light.range = mode == LightMode.High ? 85f : 38f;
                light.spotAngle = mode == LightMode.High ? 34f : 56f;
                light.innerSpotAngle = mode == LightMode.High ? 22f : 38f;
                light.transform.localRotation = Quaternion.Euler(mode == LightMode.High ? 1.8f : 7f, 0, 0);
            }
            if (i < headlampLenses.Length && headlampLenses[i] != null)
            {
                headlampLenses[i].GetPropertyBlock(block);
                block.SetColor(Emissive, on ? new Color(1f, .87f, .66f) * (mode == LightMode.High ? 1200 : 650) : Color.black);
                headlampLenses[i].SetPropertyBlock(block);
            }
        }
        if (volumetricHaze != null) volumetricHaze.enabled = mode != LightMode.Off;
    }
}
