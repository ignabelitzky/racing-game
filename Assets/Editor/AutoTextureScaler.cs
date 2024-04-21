using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Transform))]
public class AutoTextureScaler : Editor
{
    private Transform _transform;
    private Vector3 _lastScale = Vector3.one;

    void OnEnable()
    {
        // Cache the transform when the script is enabled
        _transform = (Transform)target;
        _lastScale = _transform.localScale;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI(); // Draws the default inspector

        if (_transform.localScale != _lastScale)
        {
            ScaleTextureToMatchScale();
            _lastScale = _transform.localScale;
        }
    }

    private void ScaleTextureToMatchScale()
    {
        // Get the Renderer component
        Renderer renderer = _transform.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = renderer.sharedMaterial;
            if (mat != null && mat.mainTexture != null)
            {
                Vector3 scale = _transform.localScale;
                // Set texture scale
                mat.mainTextureScale = new Vector2(scale.x, scale.y);
            }
        }
    }
}
