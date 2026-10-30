using UnityEngine;
using UnityEngine.Rendering;

/// <summary>A bounded world-space ribbon mesh, projected onto the wheel's actual contact collider.</summary>
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public sealed class SurfaceSkidmarks : MonoBehaviour
{
    private const int Capacity = 2048;
    private const float MaxConnectionDistance = 3f;
    private struct Contact
    {
        public bool valid;
        public Collider surface;
        public Vector3 center, left, right, normal;
        public float strength, distance;
    }

    private Contact[] contacts;
    private readonly Vector3[] vertices = new Vector3[Capacity * 4];
    private readonly Vector2[] uv = new Vector2[Capacity * 4];
    private readonly Vector2[] ageStrength = new Vector2[Capacity * 4];
    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock properties;
    private int cursor;
    private bool dirty;
    private float spacing, offset;

    public void Initialize(Material material, int wheels, float sampleSpacing, float surfaceOffset,
        float lifetime, float fadeDuration)
    {
        contacts = new Contact[wheels];
        spacing = Mathf.Clamp(sampleSpacing, 0.1f, 0.5f);
        offset = Mathf.Clamp(surfaceOffset, 0.003f, 0.03f);
        mesh = new Mesh { name = "Wheel contact skidmarks" };
        mesh.MarkDynamic();
        mesh.vertices = vertices;
        var indices = new int[Capacity * 6];
        for (int i = 0; i < Capacity; i++)
        {
            int v = i * 4, t = i * 6;
            indices[t] = v; indices[t + 1] = v + 2; indices[t + 2] = v + 1;
            indices[t + 3] = v + 1; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
        }
        mesh.triangles = indices;
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        properties = new MaterialPropertyBlock();
        properties.SetFloat("_Lifetime", Mathf.Max(1f, lifetime));
        properties.SetFloat("_FadeDuration", Mathf.Clamp(fadeDuration, 0.1f, lifetime));
    }

    public void BreakTrail(int wheel)
    {
        if (contacts != null && wheel >= 0 && wheel < contacts.Length) contacts[wheel].valid = false;
    }

    public void BreakAll()
    {
        if (contacts == null) return;
        for (int i = 0; i < contacts.Length; i++) BreakTrail(i);
    }

    public void AddContact(int wheel, WheelHit hit, float width, float strength)
    {
        if (contacts == null || wheel < 0 || wheel >= contacts.Length || hit.collider == null) return;
        var previous = contacts[wheel];
        Vector3 normal = hit.normal.normalized;
        float distance = Vector3.Distance(previous.center, hit.point);
        bool connect = previous.valid && previous.surface == hit.collider &&
            distance < MaxConnectionDistance && Vector3.Dot(previous.normal, normal) > 0.5f;
        if (!connect)
        {
            if (TryContact(hit.collider, hit.point, normal, hit.forwardDir, width, out var start))
            {
                start.strength = 0f; // Feather the beginning of each independent stroke.
                contacts[wheel] = start;
            }
            else BreakTrail(wheel);
            return;
        }
        if (distance < spacing) return;

        Vector3 direction = hit.point - previous.center;
        int steps = Mathf.CeilToInt(distance / spacing);
        Vector3 origin = previous.center;
        for (int step = 1; step <= steps; step++)
        {
            Vector3 sample = Vector3.Lerp(origin, hit.point, (float)step / steps);
            if (!TryContact(hit.collider, sample, normal, direction, width, out var next))
            {
                BreakTrail(wheel);
                return;
            }
            next.strength = Mathf.Clamp01(strength);
            next.distance = previous.distance + Vector3.Distance(previous.center, next.center);
            WriteSegment(previous, next);
            previous = next;
        }
        contacts[wheel] = previous;
    }

    private bool TryContact(Collider surface, Vector3 point, Vector3 normal, Vector3 direction,
        float width, out Contact contact)
    {
        contact = default;
        // Collider.Raycast avoids projecting onto the vehicle or unrelated geometry above the road.
        if (!surface.Raycast(new Ray(point + normal * 0.5f, -normal), out var center, 1.1f)) return false;
        normal = center.normal.normalized;
        direction = Vector3.ProjectOnPlane(direction, normal).normalized;
        if (direction.sqrMagnitude < 0.5f) return false;
        Vector3 right = Vector3.Cross(normal, direction) * Mathf.Clamp(width, 0.12f, 0.7f) * 0.5f;
        if (!surface.Raycast(new Ray(center.point - right + normal * 0.4f, -normal), out var left, 0.8f) ||
            !surface.Raycast(new Ray(center.point + right + normal * 0.4f, -normal), out var rightHit, 0.8f))
            return false;
        if (Vector3.Dot(left.normal, normal) < 0.5f || Vector3.Dot(rightHit.normal, normal) < 0.5f) return false;
        contact = new Contact
        {
            valid = true, surface = surface, center = center.point, normal = normal,
            left = left.point + left.normal * offset,
            right = rightHit.point + rightHit.normal * offset
        };
        return true;
    }

    private void WriteSegment(Contact from, Contact to)
    {
        int v = cursor * 4;
        vertices[v] = from.left; vertices[v + 1] = from.right;
        vertices[v + 2] = to.left; vertices[v + 3] = to.right;
        uv[v] = new Vector2(0, from.distance); uv[v + 1] = new Vector2(1, from.distance);
        uv[v + 2] = new Vector2(0, to.distance); uv[v + 3] = new Vector2(1, to.distance);
        float now = Time.time;
        ageStrength[v] = ageStrength[v + 1] = new Vector2(now, from.strength);
        ageStrength[v + 2] = ageStrength[v + 3] = new Vector2(now, to.strength);
        cursor = (cursor + 1) % Capacity;
        dirty = true;
    }

    private void LateUpdate()
    {
        if (mesh == null) return;
        if (dirty)
        {
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.uv2 = ageStrength;
            mesh.RecalculateBounds();
            dirty = false;
        }
        properties.SetFloat("_Now", Time.time);
        meshRenderer.SetPropertyBlock(properties);
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }
}
