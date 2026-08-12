using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays a temporary visual indicator after a game save completes.</summary>
public sealed class SaveIndicator : MonoBehaviour
{
    [SerializeField, Min(0f)] private float visibleDuration = 0.75f;
    [SerializeField] private float rotationSpeed = -240f;

    private Image image;

    private void Awake()
    {
        image = GetComponent<Image>();
        if (image == null)
        {
            Destroy(gameObject);
            return;
        }

        image.raycastTarget = false;
    }

    private void Start()
    {
        StartCoroutine(DestroyAfterDisplay());
    }

    private void Update()
    {
        transform.Rotate(0f, 0f, rotationSpeed * Time.unscaledDeltaTime);
    }

    private IEnumerator DestroyAfterDisplay()
    {
        yield return new WaitForSecondsRealtime(visibleDuration);
        Destroy(gameObject);
    }
}
