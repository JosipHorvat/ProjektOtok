using UnityEngine;

/// <summary>Static compatibility and modifier data for removable weapon attachments.</summary>
[CreateAssetMenu(fileName = "WeaponAttachmentDefinition", menuName = "Items/Weapon Attachment Definition")]
public sealed class WeaponAttachmentDefinition : ItemDefinition
{
    [Header("Attachment")]
    [Tooltip("Attachment slot used for compatibility checks.")]
    [SerializeField] private string attachmentSlotId = string.Empty;
    [Tooltip("Weapon tags required for this attachment to be installed.")]
    [SerializeField] private string[] compatibleWeaponTags = System.Array.Empty<string>();
    [Tooltip("Optional stable modifier identifier; the weapon handler interprets its value.")]
    [SerializeField] private string modifierId = string.Empty;
    [Tooltip("Configured modifier magnitude. Runtime installed state is saved on the weapon item instance.")]
    [SerializeField] private float modifierValue;

    /// <summary>Gets the equipment category.</summary>
    public override ItemCategory Category => ItemCategory.Attachment;

    /// <summary>Gets the attachment slot identifier.</summary>
    public string AttachmentSlotId => attachmentSlotId;

    /// <summary>Gets weapon tags required for compatibility.</summary>
    public string[] CompatibleWeaponTags => compatibleWeaponTags;

    /// <summary>Gets the modifier identifier.</summary>
    public string ModifierId => modifierId;

    /// <summary>Gets the configured modifier magnitude.</summary>
    public float ModifierValue => modifierValue;

    /// <summary>Ensures attachment compatibility data is initialized.</summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        if (compatibleWeaponTags == null)
        {
            compatibleWeaponTags = System.Array.Empty<string>();
        }
    }
}
