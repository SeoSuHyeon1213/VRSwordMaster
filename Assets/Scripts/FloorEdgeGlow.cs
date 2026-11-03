using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class FloorEdgeGlow : MonoBehaviour
{
    [SerializeField] private Color glowColor = new Color(0f, 2.5f, 3.5f, 1f);
    [SerializeField] private float lineWidth = 0.08f;
    [SerializeField] private float heightOffset = 0.025f;

    private const string GlowObjectName = "Generated Floor Edge Glow";

    private LineRenderer lineRenderer;
    private Material glowMaterial;

    private void OnEnable()
    {
        EnsureLineRenderer();
        RefreshEdge();
    }

    private void OnDisable()
    {
        if (Application.isPlaying && glowMaterial != null)
        {
            Destroy(glowMaterial);
        }
    }

    private void OnValidate()
    {
        lineWidth = Mathf.Max(0.001f, lineWidth);
        heightOffset = Mathf.Max(0f, heightOffset);

        EnsureLineRenderer();
        RefreshEdge();
    }

    private void Update()
    {
        RefreshEdge();
    }

    private void EnsureLineRenderer()
    {
        Transform glowTransform = transform.Find(GlowObjectName);
        if (glowTransform == null)
        {
            var glowObject = new GameObject(GlowObjectName);
            glowTransform = glowObject.transform;
            glowTransform.SetParent(transform, false);
        }

        lineRenderer = glowTransform.GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = glowTransform.gameObject.AddComponent<LineRenderer>();
        }

        lineRenderer.loop = true;
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 4;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.widthMultiplier = lineWidth;
        lineRenderer.textureMode = LineTextureMode.Stretch;

        if (glowMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            glowMaterial = new Material(shader)
            {
                name = "Runtime Floor Edge Glow"
            };
        }

        ApplyGlowColor();
        lineRenderer.sharedMaterial = glowMaterial;
    }

    private void RefreshEdge()
    {
        EnsureLineRenderer();

        Renderer targetRenderer = GetComponent<Renderer>();
        if (targetRenderer == null)
        {
            return;
        }

        Bounds bounds = targetRenderer.bounds;
        float y = bounds.max.y + heightOffset;

        lineRenderer.widthMultiplier = lineWidth;
        lineRenderer.SetPosition(0, new Vector3(bounds.min.x, y, bounds.min.z));
        lineRenderer.SetPosition(1, new Vector3(bounds.max.x, y, bounds.min.z));
        lineRenderer.SetPosition(2, new Vector3(bounds.max.x, y, bounds.max.z));
        lineRenderer.SetPosition(3, new Vector3(bounds.min.x, y, bounds.max.z));
        ApplyGlowColor();
    }

    private void ApplyGlowColor()
    {
        if (glowMaterial == null)
        {
            return;
        }

        if (glowMaterial.HasProperty("_BaseColor"))
        {
            glowMaterial.SetColor("_BaseColor", glowColor);
        }

        if (glowMaterial.HasProperty("_Color"))
        {
            glowMaterial.SetColor("_Color", glowColor);
        }

        if (glowMaterial.HasProperty("_EmissionColor"))
        {
            glowMaterial.SetColor("_EmissionColor", glowColor);
        }
    }
}
