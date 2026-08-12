using UnityEngine;

/// <summary>One-time red circle collectible used to verify persistent event saving.</summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(OneTimeGameEvent))]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class ExtraLifeCollectible : MonoBehaviour
{
    private bool collected;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Start()
    {
        OneTimeGameEvent oneTimeEvent = GetComponent<OneTimeGameEvent>();
        if (OneTimeGameEventManager.Instance != null && OneTimeGameEventManager.Instance.IsEventCompleted(oneTimeEvent.uID))
        {
            gameObject.SetActive(false);
            return;
        }

        Debug.Log($"[ExtraLife] Current total: {GetExtraLifeCount()}");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;
        Player player = other.GetComponentInParent<Player>();
        if (player == null) return;

        collected = true;
        GameData data = SaveManager.Instance != null ? SaveManager.Instance.ActiveGameData : null;
        if (data == null)
        {
            Debug.LogWarning("[ExtraLife] Cannot collect because no active save data exists.");
            collected = false;
            return;
        }

        data.extraLifeCount++;
        GetComponent<OneTimeGameEvent>().MarkCompleted();
        Debug.Log($"[ExtraLife] Collected. Total extra lives: {data.extraLifeCount}");
        gameObject.SetActive(false);
    }

    private int GetExtraLifeCount()
    {
        return SaveManager.Instance != null && SaveManager.Instance.ActiveGameData != null
            ? SaveManager.Instance.ActiveGameData.extraLifeCount
            : 0;
    }
}
