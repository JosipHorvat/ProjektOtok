using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Independent world-space interaction icon with device switching and trigger animation.</summary>
[DisallowMultipleComponent]
public sealed class DeviceInteractionPromptUI : MonoBehaviour
{
    [Header("Device Icons")]
    [SerializeField] private Sprite keyboardIcon;
    [SerializeField] private Sprite gamepadIcon;
    [SerializeField] private SpriteRenderer promptRenderer;

    [Header("Trigger")]
    [SerializeField] private bool hideWhenPlayerLeaves = true;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float showDuration = 0.22f;
    [SerializeField, Min(0f)] private float hideDuration = 0.16f;
    [SerializeField, Range(1f, 1.3f)] private float showOvershoot = 1.08f;
    [SerializeField, Range(0.01f, 1f)] private float hiddenScale = 0.78f;

    private const float FadePortion = 0.8f;

    private Player playerInTrigger;
    private Sequence promptTween;
    private Vector3 visibleScale;
    private Color visibleColor;
    private Sprite selectedIcon;
    private bool isVisible;
    private bool showingGamepadIcon;

    /// <summary>Gets whether this prompt is currently visible or animating.</summary>
    public bool IsVisible => isVisible;

    private void Awake()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        ResolveRenderer();
        if (promptRenderer == null)
        {
            return;
        }

        visibleScale = promptRenderer.transform.localScale;
        visibleColor = promptRenderer.color;
        HideImmediately();
    }

    private void Update()
    {
        if (isVisible)
        {
            RefreshDeviceIcon();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Player detectedPlayer = other.GetComponentInParent<Player>();
        if (detectedPlayer == null)
        {
            return;
        }

        playerInTrigger = detectedPlayer;
        Show();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        Player detectedPlayer = other.GetComponentInParent<Player>();
        if (detectedPlayer != null && playerInTrigger == null)
        {
            playerInTrigger = detectedPlayer;
            Show();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<Player>() != playerInTrigger)
        {
            return;
        }

        playerInTrigger = null;
        if (hideWhenPlayerLeaves)
        {
            Hide();
        }
    }

    /// <summary>Shows the selected device icon with a small pop and fade-in.</summary>
    public void Show()
    {
        ResolveRenderer();
        if (promptRenderer == null)
        {
            return;
        }

        isVisible = true;
        RefreshDeviceIcon();
        if (selectedIcon == null)
        {
            promptRenderer.enabled = false;
            return;
        }

        promptTween?.Kill();
        promptRenderer.enabled = true;
        promptRenderer.transform.localScale = visibleScale * hiddenScale;
        promptRenderer.color = WithAlpha(0f);

        float duration = Mathf.Max(0f, showDuration);
        float fadeDuration = duration * FadePortion;
        promptTween = DOTween.Sequence().SetUpdate(true);
        promptTween.Join(promptRenderer.transform
            .DOScale(visibleScale * showOvershoot, duration)
            .SetEase(Ease.OutBack));
        promptTween.Join(promptRenderer
            .DOFade(visibleColor.a, fadeDuration)
            .SetEase(Ease.OutQuad));
        promptTween.Append(promptRenderer.transform
            .DOScale(visibleScale, duration * (1f - FadePortion))
            .SetEase(Ease.OutQuad));
    }

    /// <summary>Hides the icon with a quick scale and fade-out.</summary>
    public void Hide()
    {
        isVisible = false;
        if (promptRenderer == null || !promptRenderer.enabled)
        {
            return;
        }

        promptTween?.Kill();
        float duration = Mathf.Max(0f, hideDuration);
        promptTween = DOTween.Sequence()
            .SetUpdate(true)
            .Join(promptRenderer.transform
                .DOScale(visibleScale * hiddenScale, duration)
                .SetEase(Ease.InBack))
            .Join(promptRenderer
                .DOFade(0f, duration)
                .SetEase(Ease.InQuad))
            .OnComplete(() => promptRenderer.enabled = false);
    }

    /// <summary>Assigns the keyboard and gamepad sprites at runtime if needed.</summary>
    public void SetDeviceIcons(Sprite keyboard, Sprite gamepad)
    {
        keyboardIcon = keyboard;
        gamepadIcon = gamepad;
        if (isVisible)
        {
            RefreshDeviceIcon();
        }
    }

    private void ResolveRenderer()
    {
        if (promptRenderer == null)
        {
            promptRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (promptRenderer == null)
        {
            promptRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
    }

    private void RefreshDeviceIcon()
    {
        ResolveRenderer();
        if (promptRenderer == null)
        {
            return;
        }

        bool gamepadConnected = Gamepad.current != null;
        Sprite nextIcon = gamepadConnected ? gamepadIcon : keyboardIcon;
        if (showingGamepadIcon == gamepadConnected && selectedIcon == nextIcon)
        {
            return;
        }

        showingGamepadIcon = gamepadConnected;
        selectedIcon = nextIcon;
        promptRenderer.sprite = nextIcon;
        promptRenderer.enabled = nextIcon != null;
    }

    private void HideImmediately()
    {
        promptTween?.Kill();
        isVisible = false;
        promptRenderer.transform.localScale = visibleScale * hiddenScale;
        promptRenderer.color = WithAlpha(0f);
        promptRenderer.enabled = false;
    }

    private Color WithAlpha(float alpha)
    {
        Color color = visibleColor;
        color.a = alpha;
        return color;
    }
}
