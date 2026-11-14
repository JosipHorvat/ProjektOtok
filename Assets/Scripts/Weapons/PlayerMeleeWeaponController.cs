using System;
using UnityEngine;

/// <summary>Resolves the carried Combat Knife or built-in backup stats and swaps between hip and hand knife sprites.</summary>
[DisallowMultipleComponent]
public sealed class PlayerMeleeWeaponController : MonoBehaviour
{
    public enum MeleeVisualMode
    {
        Sheathed,
        Drawn
    }

    private const string CombatKnifeStableItemId = "weapon.combat_knife";
    private const float BackupDamage = 5f;
    private const float BackupAttackRange = 1f;
    private const float BackupAttackCooldown = 0.8f;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer offhandKnifeRenderer;
    [SerializeField] private SpriteRenderer sheathedKnifeRenderer;

    private PlayerItemInventory playerItemInventory;
    private bool attackVisualActive;

    /// <summary>Gets the currently carried Combat Knife definition, or null when the strong knife is stored or absent.</summary>
    public MeleeWeaponDefinition CarriedCombatKnifeDefinition => FindCarriedCombatKnifeDefinition();

    /// <summary>Gets the visual mode selected by the current committed attack state.</summary>
    public MeleeVisualMode VisualMode { get; private set; }

    /// <summary>Gets whether the built-in backup knife supplies the active melee statistics.</summary>
    public bool IsUsingBackupKnife => CarriedCombatKnifeDefinition == null;

    private void Awake()
    {
        ResolveReferences();
        RefreshVisuals();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (playerItemInventory != null)
        {
            playerItemInventory.Changed += HandleInventoryChanged;
        }

        RefreshVisuals();
    }

    private void OnDisable()
    {
        if (playerItemInventory != null)
        {
            playerItemInventory.Changed -= HandleInventoryChanged;
        }

        attackVisualActive = false;
        RefreshVisuals();
    }

    /// <summary>Returns the active carried knife definition, or false when backup stats are active.</summary>
    public bool TryGetActiveDefinition(out MeleeWeaponDefinition definition)
    {
        definition = FindCarriedCombatKnifeDefinition();
        return definition != null;
    }

    /// <summary>Gets the damage, reach, and cooldown from the carried knife or the built-in backup.</summary>
    public void GetActiveStats(out float damage, out float attackRange, out float attackCooldown)
    {
        MeleeWeaponDefinition definition = FindCarriedCombatKnifeDefinition();
        if (definition != null)
        {
            damage = definition.Damage;
            attackRange = definition.AttackRange;
            attackCooldown = definition.AttackCooldown;
            return;
        }

        damage = BackupDamage;
        attackRange = BackupAttackRange;
        attackCooldown = BackupAttackCooldown;
    }

    /// <summary>Shows the hand knife and hides the hip knife for the committed stab.</summary>
    public void SetAttackVisualActive(bool active)
    {
        attackVisualActive = active;
        RefreshVisuals();
    }

    private void ResolveReferences()
    {
        if (playerItemInventory == null)
        {
            playerItemInventory = GetComponent<PlayerItemInventory>();
        }
    }

    private MeleeWeaponDefinition FindCarriedCombatKnifeDefinition()
    {
        ResolveReferences();
        ItemInventory inventory = playerItemInventory != null ? playerItemInventory.Inventory : null;
        if (inventory == null)
        {
            return null;
        }

        for (int index = 0; index < inventory.Items.Count; index++)
        {
            ItemInstance item = inventory.Items[index];
            if (item != null && item.Definition is MeleeWeaponDefinition meleeDefinition &&
                string.Equals(meleeDefinition.StableItemId, CombatKnifeStableItemId, StringComparison.Ordinal))
            {
                return meleeDefinition;
            }
        }

        return null;
    }

    private void HandleInventoryChanged(PlayerItemInventory changedInventory)
    {
        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        ResolveReferences();
        VisualMode = attackVisualActive ? MeleeVisualMode.Drawn : MeleeVisualMode.Sheathed;
        if (offhandKnifeRenderer != null)
        {
            offhandKnifeRenderer.enabled = attackVisualActive;
        }

        if (sheathedKnifeRenderer != null)
        {
            sheathedKnifeRenderer.enabled = !attackVisualActive;
        }
    }
}
