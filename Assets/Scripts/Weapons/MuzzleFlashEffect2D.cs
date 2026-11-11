using UnityEngine;

/// <summary>Fades and expands a bright unlit muzzle flash or smoke sprite before removing it.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class MuzzleFlashEffect2D : MonoBehaviour
{
    private const float DefaultDuration = 0.16f;
    private const float MinimumDuration = 0.01f;
    private const float DefaultGrowthMultiplier = 2f;
    private const string UnlitShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";

    [Header("Effect tuning")]
    [Min(0.01f)] public float effectDuration = DefaultDuration;
    [Min(1f)] public float endingScaleMultiplier = DefaultGrowthMultiplier;
    public Vector2 driftVelocity;
    public string sortingLayerName = "Player";
    public int sortingOrder = 1100;

    private SpriteRenderer spriteRenderer;
    private Color initialColor;
    private Vector3 initialScale;
    private float elapsedTime;
    private Material runtimeUnlitMaterial;

    /// <summary>Overrides effect duration and starting scale when spawned by a weapon.</summary>
    public void Initialize(float requestedDuration, float scaleMultiplier)
    {
        EnsureRenderer();
        effectDuration = Mathf.Max(MinimumDuration, requestedDuration > 0f ? requestedDuration : effectDuration);
        initialScale = transform.localScale * Mathf.Max(0.1f, scaleMultiplier);
        transform.localScale = initialScale;
        initialColor = spriteRenderer.color;
        elapsedTime = 0f;
        spriteRenderer.enabled = true;
        spriteRenderer.color = initialColor;
    }

    private void Awake()
    {
        EnsureRenderer();
        initialScale = transform.localScale;
        initialColor = spriteRenderer.color;
    }

    private void EnsureRenderer()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        spriteRenderer.enabled = true;
        spriteRenderer.sortingLayerName = sortingLayerName;
        spriteRenderer.sortingOrder = sortingOrder;

        Shader unlitShader = Shader.Find(UnlitShaderName);
        if (unlitShader != null && (runtimeUnlitMaterial == null || runtimeUnlitMaterial.shader != unlitShader))
        {
            if (runtimeUnlitMaterial != null)
            {
                Destroy(runtimeUnlitMaterial);
            }

            runtimeUnlitMaterial = new Material(unlitShader);
            spriteRenderer.material = runtimeUnlitMaterial;
        }
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;
        float normalizedTime = Mathf.Clamp01(elapsedTime / Mathf.Max(MinimumDuration, effectDuration));
        Color color = initialColor;
        color.a = initialColor.a * (1f - normalizedTime);
        spriteRenderer.color = color;
        transform.position += (Vector3)(driftVelocity * Time.deltaTime);
        transform.localScale = Vector3.Lerp(initialScale, initialScale * endingScaleMultiplier, normalizedTime);
        if (normalizedTime >= 1f)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (runtimeUnlitMaterial != null)
        {
            Destroy(runtimeUnlitMaterial);
        }
    }
}
