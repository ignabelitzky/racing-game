using UnityEngine;

/// <summary>Scales tire stiffness on marked surfaces while retaining controller slip and drive settings.</summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class WheelSurfaceGrip : MonoBehaviour
{
    [SerializeField] private Transform vehicleRoot;
    private WheelCollider[] wheels;
    private float[] forwardBase;
    private float[] sidewaysBase;

    private void OnEnable()
    {
        if (vehicleRoot == null) return;
        wheels = vehicleRoot.GetComponentsInChildren<WheelCollider>(true);
        forwardBase = new float[wheels.Length];
        sidewaysBase = new float[wheels.Length];
        for (int i = 0; i < wheels.Length; i++)
        {
            forwardBase[i] = wheels[i].forwardFriction.stiffness;
            sidewaysBase[i] = wheels[i].sidewaysFriction.stiffness;
        }
    }

    private void FixedUpdate()
    {
        if (wheels == null) return;
        for (int i = 0; i < wheels.Length; i++)
        {
            if (wheels[i] == null) continue;
            DrivingSurface surface = null;
            if (wheels[i].GetGroundHit(out WheelHit hit) && hit.collider != null)
                surface = hit.collider.GetComponentInParent<DrivingSurface>();
            SetStiffness(i, surface == null ? 1f : Mathf.Clamp(surface.forwardGrip, 0.05f, 1.5f),
                surface == null ? 1f : Mathf.Clamp(surface.sidewaysGrip, 0.05f, 1.5f));
        }
    }

    private void SetStiffness(int i, float forward, float sideways)
    {
        var f = wheels[i].forwardFriction;
        var s = wheels[i].sidewaysFriction;
        f.stiffness = forwardBase[i] * forward;
        s.stiffness = sidewaysBase[i] * sideways;
        wheels[i].forwardFriction = f;
        wheels[i].sidewaysFriction = s;
    }

    private void OnDisable()
    {
        if (wheels == null) return;
        for (int i = 0; i < wheels.Length; i++)
            if (wheels[i] != null) SetStiffness(i, 1f, 1f);
        wheels = null;
    }
}
