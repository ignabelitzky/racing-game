using UnityEngine;

/// <summary>Local tire-grip multipliers for a marked test-ground surface.</summary>
[DisallowMultipleComponent]
public sealed class DrivingSurface : MonoBehaviour
{
    [Range(0.05f, 1.5f)] public float forwardGrip = 1f;
    [Range(0.05f, 1.5f)] public float sidewaysGrip = 1f;
}
