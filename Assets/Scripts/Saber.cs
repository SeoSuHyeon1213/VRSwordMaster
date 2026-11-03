using UnityEngine;
using UnityEngine.XR;

public class Saber : MonoBehaviour
{
    public enum SaberColor { Auto, Red, Blue }

    public LayerMask layer;

    [Tooltip("이 검이 칠 수 있는 노트 색. Auto 는 컨트롤러 이름으로 결정 (Left = 빨강, Right = 파랑)")]
    [SerializeField] private SaberColor saberColor = SaberColor.Auto;

    [Header("Break Effect")]
    [SerializeField] private GameObject breakEffectPrefab;
    [SerializeField] private float effectLifeTime = 1.2f;

    [Header("Haptics")]
    [SerializeField, Range(0f, 1f)] private float hapticAmplitude = 0.6f;
    [SerializeField] private float hapticDuration = 0.08f;

    Vector3 prevPos;
    int ownNoteLayer = -1;
    XRNode? handNode;

    void Start()
    {
        prevPos = transform.position;
        handNode = ResolveHandNode();
        ownNoteLayer = ResolveOwnNoteLayer();
    }

    // 컨트롤러 이름(Left/Right)으로 이 검을 쥔 손을 결정
    XRNode? ResolveHandNode()
    {
        for (Transform t = transform; t != null; t = t.parent)
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains("left")) return XRNode.LeftHand;
            if (n.Contains("right")) return XRNode.RightHand;
        }

        return null;
    }

    int ResolveOwnNoteLayer()
    {
        SaberColor color = saberColor;
        if (color == SaberColor.Auto && handNode.HasValue)
        {
            color = handNode.Value == XRNode.LeftHand ? SaberColor.Red : SaberColor.Blue;
        }

        if (color == SaberColor.Red) return LayerMask.NameToLayer("R");
        if (color == SaberColor.Blue) return LayerMask.NameToLayer("B");

        Debug.LogWarning("[Saber] Could not determine saber color, any note can be cut: " + name);
        return -1;
    }

    void Update()
    {
        RaycastHit hit;

        if (Physics.Raycast(transform.position, transform.forward, out hit, 1, layer))
        {
            // 다른 색 노트를 치면 콤보가 끊기고 체력이 감소한다
            if (ownNoteLayer >= 0 && hit.transform.gameObject.layer != ownNoteLayer)
            {
                if (ComboManager.Instance != null) ComboManager.Instance.RegisterMiss();
                if (HealthManager.Instance != null) HealthManager.Instance.TakeDamage();

                Destroy(hit.transform.gameObject);
                prevPos = transform.position;
                return;
            }

            Vector3 v1 = transform.position - prevPos;

            if (Vector3.Angle(v1, hit.transform.up) > 130)
            {
                if (ComboManager.Instance != null) ComboManager.Instance.AddCombo();
                if (HealthManager.Instance != null) HealthManager.Instance.Heal();

                PlayBreakEffect(hit);
                PlayHaptic();
                Destroy(hit.transform.gameObject);
            }
        }

        prevPos = transform.position;
    }

    void PlayHaptic()
    {
        if (!handNode.HasValue || hapticAmplitude <= 0f || hapticDuration <= 0f) return;

        InputDevice device = InputDevices.GetDeviceAtXRNode(handNode.Value);
        if (device.isValid)
        {
            device.SendHapticImpulse(0, hapticAmplitude, hapticDuration);
        }
    }

    void PlayBreakEffect(RaycastHit hit)
    {
        Color effectColor = GetEffectColor(hit.transform.gameObject);

        if (breakEffectPrefab != null)
        {
            GameObject effect = Instantiate(breakEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
            ApplyParticleColor(effect, effectColor);
            Destroy(effect, effectLifeTime);
            return;
        }

        GameObject burst = new GameObject("Break Effect");
        burst.transform.position = hit.point;
        burst.transform.rotation = Quaternion.LookRotation(hit.normal);

        ParticleSystem particles = burst.AddComponent<ParticleSystem>();
        ConfigureBurstParticles(particles, effectColor);

        Destroy(burst, effectLifeTime);
    }

    Color GetEffectColor(GameObject target)
    {
        if (target.layer == LayerMask.NameToLayer("R"))
            return Color.red;

        if (target.layer == LayerMask.NameToLayer("B"))
            return Color.cyan;

        Renderer targetRenderer = target.GetComponentInChildren<Renderer>();
        if (targetRenderer != null && targetRenderer.material != null)
        {
            if (targetRenderer.material.HasProperty("_EmissionColor"))
                return targetRenderer.material.GetColor("_EmissionColor");

            if (targetRenderer.material.HasProperty("_BaseColor"))
                return targetRenderer.material.GetColor("_BaseColor");

            return targetRenderer.material.color;
        }

        return Color.white;
    }

    void ConfigureBurstParticles(ParticleSystem particles, Color color)
    {
        var main = particles.main;
        main.duration = 0.18f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.14f);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 36)
        });

        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.12f;

        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(Color.white, 0.25f),
                new GradientColorKey(color, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        var trails = particles.trails;
        trails.enabled = true;
        trails.lifetime = 0.18f;
        trails.ratio = 0.45f;

        particles.Play();
    }

    void ApplyParticleColor(GameObject effect, Color color)
    {
        foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>())
        {
            var main = particles.main;
            main.startColor = color;
        }
    }
}
