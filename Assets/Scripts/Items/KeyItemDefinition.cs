using UnityEngine;

/// <summary>Static identity and protection data for key items, access cards, evidence, and quest items.</summary>
[CreateAssetMenu(fileName = "KeyItemDefinition", menuName = "Items/Key Item Definition")]
public sealed class KeyItemDefinition : ItemDefinition
{
    [Header("Key and quest item")]
    [Tooltip("Whether this is a quest/evidence entry rather than a physical access key or card.")]
    [SerializeField] private bool isQuestItem;
    [Tooltip("Stable objective, door, evidence, or quest identifier associated with this item.")]
    [SerializeField] private string questOrKeyId = string.Empty;
    [Tooltip("Whether the item remains in the key-item collection after successful use.")]
    [SerializeField] private bool retainAfterUse = true;

    /// <summary>Gets the key-item or quest-item category.</summary>
    public override ItemCategory Category => isQuestItem ? ItemCategory.QuestItem : ItemCategory.KeyItem;

    /// <summary>Gets the stable quest, evidence, or lock identifier.</summary>
    public string QuestOrKeyId => questOrKeyId;

    /// <summary>Gets whether the item remains after successful use.</summary>
    public bool RetainAfterUse => retainAfterUse;

    /// <summary>Key and quest items are protected from discarding.</summary>
    public override bool CanBeDiscarded => false;

    /// <summary>Key and quest items are protected from selling.</summary>
    public override bool CanBeSold => false;

    /// <summary>Validates the required stable key/quest identifier and shared data.</summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        if (string.IsNullOrWhiteSpace(questOrKeyId))
        {
            Debug.LogWarning($"Key/quest item '{name}' needs a stable quest/key identifier.", this);
        }
    }
}
