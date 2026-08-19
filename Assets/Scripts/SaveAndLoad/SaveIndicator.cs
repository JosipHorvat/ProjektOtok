using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays a polished temporary save notification after a game save completes.</summary>
public sealed class SaveIndicator : MonoBehaviour
{
    [Header("Display")]
    [SerializeField, Min(0f)] private float showDuration = 0.2f;
    [SerializeField, Min(0f)] private float holdDuration = 0.7f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;
    [SerializeField, Min(0f)] private float riseDistance = 16f;
    [SerializeField, Range(0.01f, 1f)] private float startScale = 0.82f;
    [SerializeField, Range(1f, 1.2f)] private float showOvershoot = 1.06f;

    private Image image;
    private RectTransform rectTransform;
    private Sequence displayTween;
    private Vector2 startPosition;
    private Vector3 baseScale;
    private Color baseColor;

    private void Awake()
    {
        image = GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();
        if (image == null || rectTransform == null)
        {
            Destroy(gameObject);
            return;
        }

        image.raycastTarget = false;
        startPosition = rectTransform.anchoredPosition;
        baseScale = transform.localScale;
        baseColor = image.color;
    }

    private void Start()
    {
        PlayDisplayAnimation();
    }

    private void OnDestroy()
    {
        displayTween?.Kill();
    }

    private void PlayDisplayAnimation()
    {
        displayTween?.Kill();

        float showTime = Mathf.Max(0f, showDuration);
        float fadeTime = Mathf.Max(0f, fadeOutDuration);
        Vector2 exitPosition = startPosition + Vector2.up * riseDistance;

        rectTransform.anchoredPosition = startPosition;
        transform.localScale = baseScale * startScale;
        image.color = WithAlpha(0f);

        displayTween = DOTween.Sequence().SetUpdate(true);
        displayTween.Append(transform
            .DOScale(baseScale * showOvershoot, showTime)
            .SetEase(Ease.OutBack));
        displayTween.Join(image
            .DOFade(baseColor.a, showTime * 0.8f)
            .SetEase(Ease.OutQuad));
        displayTween.Append(transform
            .DOScale(baseScale, showTime * 0.35f)
            .SetEase(Ease.OutQuad));
        displayTween.AppendInterval(Mathf.Max(0f, holdDuration));
        displayTween.Append(rectTransform
            .DOAnchorPos(exitPosition, fadeTime)
            .SetEase(Ease.InQuad));
        displayTween.Join(image
            .DOFade(0f, fadeTime)
            .SetEase(Ease.InQuad));
        displayTween.OnComplete(() => Destroy(gameObject));
    }

    private Color WithAlpha(float alpha)
    {
        Color color = baseColor;
        color.a = alpha;
        return color;
    }
}
