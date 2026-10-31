using UnityEngine;

/// <summary>Applied brake torque heats each disc; calipers follow steering without wheel spin.</summary>
[DefaultExecutionOrder(75)]
public sealed class BrakeDiscThermal : MonoBehaviour
{
    [SerializeField] private WheelCollider[] wheels = new WheelCollider[0];
    [SerializeField] private Transform[] wheelVisuals = new Transform[0];
    [SerializeField] private Renderer[] rotors = new Renderer[0];
    [SerializeField] private Transform[] calipers = new Transform[0];
    [SerializeField] private Vector3[] wheelCenters = new Vector3[0];
    [SerializeField] private Vector3[] caliperOffsets = new Vector3[0];
    [SerializeField, Min(.001f)] private float heatingRate = .20f;
    [SerializeField, Min(.001f)] private float coolingRate = .04f;
    private Rigidbody body;
    private TumblerSystems systems;
    private float[] heat;
    private MaterialPropertyBlock block;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int Emissive = Shader.PropertyToID("_EmissiveColor");
    public float GetHeat(int wheel) => heat != null && wheel >= 0 && wheel < heat.Length ? heat[wheel] : 0;
    private void Awake()
    {
        body = GetComponent<Rigidbody>(); systems = GetComponent<TumblerSystems>();
        heat = new float[rotors.Length]; block = new MaterialPropertyBlock();
    }
    private void Update()
    {
        float speed = body != null ? body.linearVelocity.magnitude : 0;
        for (int i = 0; i < heat.Length; i++)
        {
            var wheel = i < wheels.Length ? wheels[i] : null;
            float frictionSpeed = wheel != null && !wheel.isGrounded
                ? Mathf.Abs(wheel.rpm) * wheel.radius * (2f * Mathf.PI / 60f) : speed;
            float effort = wheel != null && frictionSpeed > .5f ? Mathf.Clamp(wheel.brakeTorque / 1000f, 0, 1.5f) : 0;
            // Service braking heats all six discs. The existing controller applies handbrake torque only to the rear four.
            AdvanceHeat(i, Time.deltaTime, effort, frictionSpeed);
        }
        ApplyAppearance();
    }
    private void AdvanceHeat(int wheel, float dt, float brakingEffort, float speed)
    {
        float input = heatingRate * brakingEffort * Mathf.Clamp(speed / 12f, 0f, 1.6f);
        heat[wheel] = Mathf.Clamp01(heat[wheel] + dt * (input - coolingRate * (1f + Mathf.Clamp01(speed / 40f))));
    }
    private void ApplyAppearance()
    {
        for (int i = 0; i < rotors.Length; i++)
        {
            if (rotors[i] == null) continue;
            float h = systems != null && systems.IsStealth ? 0f : heat[i];
            var tint = Color.Lerp(new Color(.40f, .42f, .44f), new Color(.75f, .045f, .008f), Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.22f, .9f, h)));
            Color glow = Color.Lerp(new Color(1, .018f, .001f), new Color(1, .07f, .002f), h);
            rotors[i].GetPropertyBlock(block, 0); block.SetColor(BaseColor, tint);
            block.SetColor(Emissive, glow * (Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, 1, h)) * 35));
            rotors[i].SetPropertyBlock(block, 0);
        }
    }
    private void LateUpdate()
    {
        for (int i = 0; i < calipers.Length; i++)
        {
            if (calipers[i] == null || i >= wheelVisuals.Length || wheelVisuals[i] == null) continue;
            Quaternion rotation = transform.rotation * Quaternion.Euler(0, wheels[i].steerAngle, 0);
            calipers[i].SetPositionAndRotation(wheelVisuals[i].TransformPoint(wheelCenters[i]) + rotation * caliperOffsets[i], rotation);
        }
    }
}
