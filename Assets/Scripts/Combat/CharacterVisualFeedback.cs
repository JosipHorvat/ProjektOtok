using UnityEngine;

/// <summary>Displays reusable visual feedback when a character takes damage.</summary>
[DisallowMultipleComponent]
public sealed class CharacterVisualFeedback : MonoBehaviour
{
    private const float DefaultHitFlashDuration = 0.1f;

    [Header("References")]
    [Tooltip("Optional component that implements IDamageFeedbackSource. If empty, this object is searched for one.")]
    [SerializeField] private MonoBehaviour damageEventSource;
    [Tooltip("Sprite renderers to flash. When empty, all child SpriteRenderers are used.")]
    [SerializeField] private SpriteRenderer[] spriteRenderers;

    [Header("Hit Flash")]
    [Tooltip("Tint applied briefly after a hit. Each sprite keeps its original transparency.")]
    [SerializeField] private Color hitFlashColor = Color.red;
    [Tooltip("How long each hit flash lasts, in seconds.")]
    [SerializeField, Min(0.01f)] private float hitFlashDuration = DefaultHitFlashDuration;

    private IDamageFeedbackSource damageSource;
    private Color[] originalColors;
    private float flashTimeRemaining;
    private bool isDead;

    private void Awake()
    {
        CacheReferences();
        CaptureOriginalColors();
    }

    private void OnEnable()
    {
        CacheReferences();
        if (damageSource != null)
        {
            damageSource.Damaged += HandleDamage;
            damageSource.Died += HandleDeath;
            if (damageSource.IsDead)
            {
                HandleDeath();
            }
        }
    }

    private void OnDisable()
    {
        if (damageSource != null)
        {
            damageSource.Damaged -= HandleDamage;
            damageSource.Died -= HandleDeath;
        }

        RestoreOriginalColors();
        flashTimeRemaining = 0f;
    }

    private void Update()
    {
        if (isDead || flashTimeRemaining <= 0f)
        {
            return;
        }

        flashTimeRemaining -= Time.unscaledDeltaTime;
        if (flashTimeRemaining <= 0f)
        {
            RestoreOriginalColors();
        }
    }

    /// <summary>Restarts the short hit flash while the character is alive.</summary>
    public void PlayHitFlash()
    {
        if (isDead || spriteRenderers == null || originalColors == null)
        {
            return;
        }

        flashTimeRemaining = hitFlashDuration;
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            Color flashColor = hitFlashColor;
            flashColor.a = originalColors[i].a;
            spriteRenderer.color = flashColor;
        }
    }

    private void CacheReferences()
    {
        if (damageEventSource == null)
        {
            MonoBehaviour[] components = GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is IDamageFeedbackSource)
                {
                    damageEventSource = components[i];
                    break;
                }
            }
        }

        damageSource = damageEventSource as IDamageFeedbackSource;

        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        if (originalColors == null || originalColors.Length != spriteRenderers.Length)
        {
            CaptureOriginalColors();
        }
    }

    private void CaptureOriginalColors()
    {
        if (spriteRenderers == null)
        {
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        originalColors = new Color[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            originalColors[i] = spriteRenderers[i] != null ? spriteRenderers[i].color : Color.white;
        }
    }

    private void HandleDamage(float damage, Vector2 hitPoint, Vector2 hitDirection)
    {
        PlayHitFlash();
    }

    private void HandleDeath()
    {
        isDead = true;
        flashTimeRemaining = 0f;
        RestoreOriginalColors();
    }

    private void RestoreOriginalColors()
    {
        if (spriteRenderers == null || originalColors == null)
        {
            return;
        }

        int rendererCount = Mathf.Min(spriteRenderers.Length, originalColors.Length);
        for (int i = 0; i < rendererCount; i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].color = originalColors[i];
            }
        }
    }
}
