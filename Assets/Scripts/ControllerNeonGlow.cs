using UnityEngine;

[DisallowMultipleComponent]
public class ControllerNeonGlow : MonoBehaviour
{
    [SerializeField] private Color glowColor = new Color(0f, 2.8f, 3.6f, 1f);
    [SerializeField] private float beamLength = 0.85f;
    [SerializeField] private float beamWidth = 0.045f;
    [SerializeField] private float trailTime = 0.22f;
    [SerializeField] private float pulseSpeed = 4f;
    [SerializeField] private float pulseAmount = 0.25f;
    [SerializeField] private bool addPointLight = true;

    private const string GlowObjectName = "Generated Controller Neon Glow";

    private LineRenderer beam;
    private TrailRenderer trail;
    private Light pointLight;
    private Material glowMaterial;

    private void OnEnable()
    {
        EnsureGlowObjects();
        ApplyGlow();
    }

    private void OnDisable()
    {
        if (Application.isPlaying && glowMaterial != null)
        {
            Destroy(glowMaterial);
        }
    }

    private void Update()
    {
        EnsureGlowObjects();
        ApplyGlow();
    }

    private void EnsureGlowObjects()
    {
        Transform glowTransform = transform.Find(GlowObjectName);
        if (glowTransform == null)
        {
            var glowObject = new GameObject(GlowObjectName);
            glowTransform = glowObject.transform;
            glowTransform.SetParent(transform, false);
        }

        beam = glowTransform.GetComponent<LineRenderer>();
        if (beam == null)
        {
            beam = glowTransform.gameObject.AddComponent<LineRenderer>();
        }

        trail = glowTransform.GetComponent<TrailRenderer>();
        if (trail == null)
        {
            trail = glowTransform.gameObject.AddComponent<TrailRenderer>();
        }

        pointLight = glowTransform.GetComponent<Light>();
        if (addPointLight && pointLight == null)
        {
            pointLight = glowTransform.gameObject.AddComponent<Light>();
            pointLight.type = LightType.Point;
        }
        else if (!addPointLight && pointLight != null)
        {
            pointLight.enabled = false;
        }

        if (glowMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            glowMaterial = new Material(shader)
            {
                name = "Runtime Controller Neon Glow"
            };
        }

        beam.sharedMaterial = glowMaterial;
        trail.sharedMaterial = glowMaterial;
    }

    private void ApplyGlow()
    {
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        Color animatedColor = glowColor * pulse;
        animatedColor.a = glowColor.a;

        ApplyMaterialColor(animatedColor);

        beam.useWorldSpace = false;
        beam.positionCount = 2;
        beam.SetPosition(0, Vector3.zero);
        beam.SetPosition(1, Vector3.forward * beamLength);
        beam.widthMultiplier = beamWidth * pulse;
        beam.numCapVertices = 6;
        beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        beam.receiveShadows = false;

        trail.time = trailTime;
        trail.startWidth = beamWidth * 1.25f;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.015f;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.emitting = true;

        if (pointLight != null && addPointLight)
        {
            pointLight.enabled = true;
            pointLight.color = glowColor;
            pointLight.range = 0.8f;
            pointLight.intensity = 1.4f * pulse;
        }
    }

    private void ApplyMaterialColor(Color color)
    {
        if (glowMaterial == null)
        {
            return;
        }

        if (glowMaterial.HasProperty("_BaseColor"))
        {
            glowMaterial.SetColor("_BaseColor", color);
        }

        if (glowMaterial.HasProperty("_Color"))
        {
            glowMaterial.SetColor("_Color", color);
        }

        if (glowMaterial.HasProperty("_EmissionColor"))
        {
            glowMaterial.SetColor("_EmissionColor", color);
        }
    }
}
