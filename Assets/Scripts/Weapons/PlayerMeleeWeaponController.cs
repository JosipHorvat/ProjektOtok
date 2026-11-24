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
    private const string PlayerAnimatorPath = "PlayerAnimator";
    private const string OffhandKnifePath = "root/pelvis/abdomen/chest/shoulder_r/arm_r/forearm_r/hand_r/CombatKnife_OffHand";
    private const string SheathedKnifePath = "root/pelvis/hip/CombatKnife_Sheath";
    private const float BackupDamage = 5f;
    private const float BackupAttackRange = 1f;
    private const float BackupAttackCooldown = 0.8f;
    private const float BackupStaggerImpact = 12f;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer offhandKnifeRenderer;
    [SerializeField] private SpriteRenderer sheathedKnifeRenderer;

    private PlayerItemInventory playerItemInventory;
    private PlayerMeleeAttackController attackController;
    private Transform playerAnimatorTransform;
    private bool attackVisualActive;
    private bool readyVisualActive;

    /// <summary>Shows the knife guard independently of commitment; an active Q stab retains its original draw schedule.</summary>
    public void SetReadyVisualActive(bool active)
    {
        readyVisualActive = active;
        RefreshVisuals();
    }

    /// <summary>Gets the currently carried Combat Knife definition, or null when the strong knife is stored or absent.</summary>
    public MeleeWeaponDefinition CarriedCombatKnifeDefinition => FindCarriedCombatKnifeDefinition();

    /// <summary>Gets the visual mode selected by the current committed attack state.</summary>
    public MeleeVisualMode VisualMode { get; private set; }

    /// <summary>Gets whether the built-in backup knife supplies the active melee statistics.</summary>
    public bool IsUsingBackupKnife => CarriedCombatKnifeDefinition == null;

    private void Awake()
    {
        attackVisualActive = false;
        ResolveReferences();
        RefreshVisuals();
    }

    private void OnEnable()
    {
        attackVisualActive = false;
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

        attackController?.CancelMeleeAttack();
        readyVisualActive = false;
        attackVisualActive = false;
        RefreshVisuals();
    }

    private void LateUpdate()
    {
        RefreshVisuals();
    }

    /// <summary>Returns the active carried knife definition, or false when backup stats are active.</summary>
    public bool TryGetActiveDefinition(out MeleeWeaponDefinition definition)
    {
        definition = FindCarriedCombatKnifeDefinition();
        return definition != null;
    }

    /// <summary>Gets the damage, reach, cooldown, and poise impact from the carried knife or built-in backup.</summary>
    public void GetActiveStats(out float damage, out float attackRange, out float attackCooldown, out float staggerImpact)
    {
        MeleeWeaponDefinition definition = FindCarriedCombatKnifeDefinition();
        if (definition != null)
        {
            damage = definition.Damage;
            attackRange = definition.AttackRange;
            attackCooldown = definition.AttackCooldown;
            staggerImpact = definition.StaggerImpact;
            return;
        }

        damage = BackupDamage;
        attackRange = BackupAttackRange;
        attackCooldown = BackupAttackCooldown;
        staggerImpact = BackupStaggerImpact;
    }

    /// <summary>Shows the hand knife only during the committed attack's draw-to-resheath interval; otherwise shows the hip knife.</summary>
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

        if (attackController == null)
        {
            attackController = GetComponent<PlayerMeleeAttackController>();
        }

        if (playerAnimatorTransform == null)
        {
            playerAnimatorTransform = transform.Find(PlayerAnimatorPath);
        }

        if (playerAnimatorTransform == null)
        {
            return;
        }

        if (offhandKnifeRenderer == null)
        {
            Transform knifeTransform = playerAnimatorTransform.Find(OffhandKnifePath);
            if (knifeTransform != null)
            {
                offhandKnifeRenderer = knifeTransform.GetComponent<SpriteRenderer>();
            }
        }

        if (sheathedKnifeRenderer == null)
        {
            Transform knifeTransform = playerAnimatorTransform.Find(SheathedKnifePath);
            if (knifeTransform != null)
            {
                sheathedKnifeRenderer = knifeTransform.GetComponent<SpriteRenderer>();
            }
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
        bool committed = attackController != null && attackController.IsAttackCommitted;
        bool isKnifeDrawn = isActiveAndEnabled &&
            (committed ? attackVisualActive : readyVisualActive);
        VisualMode = isKnifeDrawn ? MeleeVisualMode.Drawn : MeleeVisualMode.Sheathed;
        if (offhandKnifeRenderer != null)
        {
            offhandKnifeRenderer.enabled = isKnifeDrawn;
        }

        if (sheathedKnifeRenderer != null)
        {
            sheathedKnifeRenderer.enabled = !isKnifeDrawn;
        }
    }
}
