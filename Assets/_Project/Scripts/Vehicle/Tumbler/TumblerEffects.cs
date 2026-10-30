using UnityEngine;

[DefaultExecutionOrder(50)]
public class TumblerEffects : MonoBehaviour
{
    [Header("Tumbler Settings")]
    [SerializeField] private GameObject tumbler;
    private TumblerController tumblerController;
    private Rigidbody vehicleBody;

    [Header("Wheel References")]
    [SerializeField] private float[] tireMarkWidths;
    [SerializeField] private ParticleSystem[] tireSmokes;
    [SerializeField] private WheelCollider[] tireColliders;

    [Header("Subtle Contact Marks")]
    [SerializeField] private Material skidmarkMaterial;
    [SerializeField, Min(0.1f)] private float minimumSpeed = 1.5f;
    [SerializeField, Min(0.01f)] private float sidewaysSlipThreshold = 0.15f;
    [SerializeField, Min(0.01f)] private float brakingSlipThreshold = 0.08f;
    [SerializeField, Range(0f, 0.5f)] private float maximumOpacity = 0.24f;
    [SerializeField, Min(1f)] private float markLifetime = 25f;
    [SerializeField, Min(0.1f)] private float fadeDuration = 8f;
    [SerializeField, Range(0.1f, 0.5f)] private float sampleSpacing = 0.2f;
    [SerializeField, Range(0.003f, 0.03f)] private float surfaceOffset = 0.008f;

    private SurfaceSkidmarks marks;
    private float[] widths, smokeRemainders;
    private Vector3 previousVehiclePosition;

    private void Awake()
    {
        if (tumbler == null || !tumbler.TryGetComponent(out tumblerController) ||
            !tumbler.TryGetComponent(out vehicleBody))
        {
            Debug.LogError("TumblerEffects requires its vehicle controller and Rigidbody.", this);
            enabled = false;
            return;
        }
        widths = new float[tireColliders.Length];
        for (int i = 0; i < tireColliders.Length; i++)
        {
            widths[i] = tireMarkWidths != null && i < tireMarkWidths.Length
                ? Mathf.Clamp(tireMarkWidths[i], 0.12f, 0.7f) : 0.35f;
        }
        smokeRemainders = new float[tireSmokes.Length];
        foreach (var smoke in tireSmokes)
        {
            if (smoke == null) continue;
            var main = smoke.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = smoke.emission;
            emission.enabled = false;
        }
        if (skidmarkMaterial != null)
        {
            // A separate scene root keeps deposited vertices fixed when the car moves, rolls or resets.
            marks = new GameObject("Surface Skidmarks").AddComponent<SurfaceSkidmarks>();
            marks.Initialize(skidmarkMaterial, tireColliders.Length, sampleSpacing, surfaceOffset,
                markLifetime, fadeDuration);
        }
        previousVehiclePosition = vehicleBody.position;
    }

    private void FixedUpdate()
    {
        if (vehicleBody == null) return;
        float expectedTravel = vehicleBody.linearVelocity.magnitude * Time.deltaTime;
        if (Vector3.Distance(previousVehiclePosition, vehicleBody.position) > Mathf.Max(3f, expectedTravel * 3f))
        {
            if (marks != null) marks.BreakAll();
        }
        previousVehiclePosition = vehicleBody.position;

        for (int i = 0; i < tireColliders.Length; i++)
        {
            var wheel = tireColliders[i];
            if (wheel == null || !wheel.GetGroundHit(out WheelHit hit) || hit.collider == null)
            {
                StopWheel(i);
                continue;
            }
            // Tire effects follow that wheel, even when the other axle is airborne.
            float speed = Vector3.ProjectOnPlane(vehicleBody.GetPointVelocity(hit.point), hit.normal).magnitude;
            float intensity = ContactIntensity(hit.sidewaysSlip, hit.forwardSlip, wheel.brakeTorque, speed);
            if (intensity <= 0f || (hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic))
            {
                StopWheel(i);
                continue;
            }
            if (marks != null) marks.AddContact(i, hit, widths[i], intensity * maximumOpacity);
            EmitSmoke(i, hit, intensity);
        }
    }

    private float ContactIntensity(float sidewaysSlip, float forwardSlip, float brakeTorque, float speed)
    {
        if (speed < minimumSpeed) return 0f;
        float lateral = Mathf.InverseLerp(sidewaysSlipThreshold, 0.65f, Mathf.Abs(sidewaysSlip));
        float braking = brakeTorque > 1f
            ? Mathf.InverseLerp(brakingSlipThreshold, 0.65f, Mathf.Abs(forwardSlip)) : 0f;
        return Mathf.SmoothStep(0f, 1f, Mathf.Max(lateral, braking));
    }

    private void EmitSmoke(int wheel, WheelHit hit, float intensity)
    {
        int i = wheel - 2; // Existing smoke references cover the four rear tires.
        if (i < 0 || i >= tireSmokes.Length || tireSmokes[i] == null) return;
        var smoke = tireSmokes[i];
        smoke.transform.position = hit.point + hit.normal * 0.04f;
        smokeRemainders[i] += Mathf.Max(0f, intensity - 0.35f) * 18f * Time.deltaTime;
        int count = Mathf.FloorToInt(smokeRemainders[i]);
        if (count == 0) return;
        smokeRemainders[i] -= count;
        smoke.Emit(count);
    }

    private void StopWheel(int wheel)
    {
        if (marks != null) marks.BreakTrail(wheel);
        int smoke = wheel - 2;
        if (smoke >= 0 && smoke < smokeRemainders.Length) smokeRemainders[smoke] = 0f;
    }

    private void OnDisable()
    {
        if (marks != null) marks.BreakAll();
    }

    private void OnDestroy()
    {
        if (marks != null) Destroy(marks.gameObject);
    }
}
