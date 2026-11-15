using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Consumes melee input, commits a facing-direction stab, and applies direct 2D melee damage.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public sealed class PlayerMeleeAttackController : MonoBehaviour
{
    private const string StandardSlashTrigger = "MeleeSlash";
    private const string MeleeAttackActiveParameter = "MeleeAttackActive";
    private const string WeaponActionsLayerName = "WeaponActions";
    private const float KnifeDrawNormalizedTime = 0.20f;
    private const float KnifeResheathNormalizedTime = 0.90f;
    private const float MinimumAttackRange = 0.05f;
    private const float MinimumAttackCooldown = 0.01f;
    private const float MinimumAttackDuration = 0.1f;
    private const float MinimumImpactNormalizedTime = 0.05f;
    private const float MaximumImpactNormalizedTime = 0.9f;
    private const float MinimumFacingDirectionMagnitude = 0.01f;
    private const float MinimumHitboxHeight = 0.3f;
    private const float AttackOriginVerticalOffsetScale = 0.2f;
    private const float AttackTimeoutGrace = 0.15f;
    private const float DefaultAttackDuration = 0.6f;
    private const float DefaultImpactNormalizedTime = 0.48f;
    private const float AttackHeightScale = 0.6f;

    [Header("Committed attack timing")]
    [SerializeField, Min(MinimumAttackDuration)] private float committedAttackDuration = DefaultAttackDuration;
    [SerializeField, Range(MinimumImpactNormalizedTime, MaximumImpactNormalizedTime)] private float impactNormalizedTime = DefaultImpactNormalizedTime;

    private Player player;
    private PlayerInputReader inputReader;
    private Animator animator;
    private WeaponFireController weaponFireController;
    private PlayerReloadController reloadController;
    private PlayerMeleeWeaponController meleeWeaponController;
    private WeaponHoldController weaponHoldController;
    private HeldWeapon weaponAtAttackStart;

    private readonly HashSet<IDamageable2D> damagedTargets = new HashSet<IDamageable2D>();
    private Vector2 attackDirection;
    private float activeDamage;
    private float activeAttackRange;
    private float activeAttackCooldown;
    private float impactTime;
    private float knifeDrawTime;
    private float knifeResheathTime;
    private float attackEndTime;
    private float nextAttackTime;
    private bool isAttackCommitted;
    private bool hasAppliedDamage;
    private bool hasDrawnKnife;
    private bool hasResheathedKnife;
    private int suppressedWeaponActionsLayerIndex = -1;
    private float weaponActionsLayerWeightBeforeAttack;
    private string activeSlashTrigger = StandardSlashTrigger;

    /// <summary>Returns whether a committed melee slash is currently controlling the player.</summary>
    public bool IsAttackCommitted => isAttackCommitted;

    /// <summary>Returns the world-space facing direction selected for the current stab.</summary>
    public Vector2 AttackDirection => attackDirection;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        CompleteAttack();
    }

    private void Update()
    {
        if (isAttackCommitted)
        {
            if (ShouldCancelAttack())
            {
                CancelMeleeAttack();
                return;
            }

            SuppressWeaponActionsLayer();
            if (Time.time >= knifeResheathTime)
            {
                SheatheMeleeKnife();
            }
            else if (Time.time >= knifeDrawTime)
            {
                DrawMeleeKnife();
            }

            if (!hasAppliedDamage && Time.time >= impactTime)
            {
                ApplyMeleeDamage();
            }

            if (IsAttackAnimationFinished() || Time.time >= attackEndTime)
            {
                CompleteAttack();
            }

            return;
        }

        if (inputReader == null || !inputReader.MeleeAttackPressedThisFrame)
        {
            return;
        }

        MeleeAttackInputSource inputSource = inputReader.MeleeAttackSourceThisFrame;
        if (inputSource != MeleeAttackInputSource.None)
        {
            TryBeginAttack(inputSource);
        }
    }

    private void LateUpdate()
    {
        if (isAttackCommitted)
        {
            SuppressWeaponActionsLayer();
        }
    }

    /// <summary>Draws the hand knife at the authored draw frame; ignored after resheathing or outside an active attack.</summary>
    public void DrawMeleeKnife()
    {
        if (!isAttackCommitted || hasDrawnKnife || hasResheathedKnife || Time.time >= knifeResheathTime)
        {
            return;
        }

        if (ShouldCancelAttack())
        {
            CancelMeleeAttack();
            return;
        }

        hasDrawnKnife = true;
        meleeWeaponController?.SetAttackVisualActive(true);
    }

    /// <summary>Hides the hand knife at the authored resheath frame and prevents later draw events or fallbacks from showing it again.</summary>
    public void SheatheMeleeKnife()
    {
        if (!isAttackCommitted)
        {
            return;
        }

        hasResheathedKnife = true;
        meleeWeaponController?.SetAttackVisualActive(false);
    }

    /// <summary>Cancels the committed stab, restores the gun and weapon animation layer, and hides the hand knife without applying pending damage.</summary>
    public void CancelMeleeAttack()
    {
        CompleteAttack();
    }

    /// <summary>Animation event positioned at the stab's impact frame.</summary>
    public void AnimationEvent_ApplyMeleeDamage()
    {
        ApplyMeleeDamage();
    }

    /// <summary>Animation event called after the committed recovery portion of the stab.</summary>
    public void AnimationEvent_MeleeAttackFinished()
    {
        CompleteAttack();
    }

    /// <summary>Begins a facing-direction stab from Q or the existing gamepad melee button.</summary>
    public bool TryBeginAttack(MeleeAttackInputSource inputSource)
    {
        ResolveReferences();
        if (!isActiveAndEnabled || isAttackCommitted || inputSource == MeleeAttackInputSource.None ||
            player == null || !player.isActiveAndEnabled || inputReader == null || !inputReader.isActiveAndEnabled ||
            meleeWeaponController == null || !meleeWeaponController.isActiveAndEnabled ||
            weaponHoldController != null && !weaponHoldController.isActiveAndEnabled ||
            inputReader.IsMeleeAttackCommitted ||
            (GameManager.Instance != null && GameManager.Instance.IsGameplayInputLocked) ||
            player.IsOnLadder ||
            reloadController != null && reloadController.IsReloading ||
            player.stateMachine != null && player.stateMachine.CurrentState == player.quickStepState ||
            Time.time < nextAttackTime)
        {
            return false;
        }

        meleeWeaponController.GetActiveStats(out activeDamage, out activeAttackRange, out activeAttackCooldown);
        activeAttackRange = Mathf.Max(MinimumAttackRange, activeAttackRange);
        activeAttackCooldown = Mathf.Max(MinimumAttackCooldown, activeAttackCooldown);
        activeSlashTrigger = StandardSlashTrigger;
        if (meleeWeaponController.TryGetActiveDefinition(out MeleeWeaponDefinition activeDefinition) &&
            !string.IsNullOrWhiteSpace(activeDefinition.AttackAnimationTrigger))
        {
            activeSlashTrigger = activeDefinition.AttackAnimationTrigger;
        }

        attackDirection = new Vector2(player.facingDir, 0f);
        if (Mathf.Abs(attackDirection.x) > MinimumFacingDirectionMagnitude)
        {
            player.SetFacingDirection(attackDirection.x > 0f ? 1 : -1);
        }

        float duration = Mathf.Max(MinimumAttackDuration, committedAttackDuration);
        impactTime = Time.time + duration * Mathf.Clamp01(impactNormalizedTime);
        knifeDrawTime = Time.time + duration * KnifeDrawNormalizedTime;
        knifeResheathTime = Time.time + duration * KnifeResheathNormalizedTime;
        attackEndTime = Time.time + duration + AttackTimeoutGrace;
        nextAttackTime = Time.time + activeAttackCooldown;
        hasAppliedDamage = false;
        hasDrawnKnife = false;
        hasResheathedKnife = false;
        weaponAtAttackStart = weaponHoldController != null ? weaponHoldController.CurrentWeapon : null;
        damagedTargets.Clear();
        isAttackCommitted = true;

        try
        {
            inputReader.SetMeleeAttackCommitted(true);
            inputReader.ConsumeJumpBuffer();
            weaponFireController?.NotifyMeleeAttackStarted();
            meleeWeaponController.SetAttackVisualActive(false);
            weaponHoldController?.SetMeleeMode(true);
            SuppressWeaponActionsLayer();

            if (animator != null)
            {
                animator.ResetTrigger(activeSlashTrigger);
                animator.SetBool(MeleeAttackActiveParameter, true);
                animator.SetTrigger(activeSlashTrigger);
            }

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            CompleteAttack();
            return false;
        }
    }

    private bool IsAttackAnimationFinished()
    {
        if (animator == null)
        {
            return false;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsName("Base Layer.MeleeSlash") && stateInfo.normalizedTime >= 1f;
    }

    private void ApplyMeleeDamage()
    {
        if (!isAttackCommitted || hasAppliedDamage)
        {
            return;
        }

        if (ShouldCancelAttack())
        {
            CancelMeleeAttack();
            return;
        }

        hasAppliedDamage = true;
        Vector2 attackOrigin = GetAttackOrigin();
        Vector2 boxSize = new Vector2(
            activeAttackRange,
            Mathf.Max(MinimumHitboxHeight, activeAttackRange * AttackHeightScale));
        Vector2 boxCenter = attackOrigin + attackDirection * (activeAttackRange * 0.5f);
        float boxAngle = Mathf.Atan2(attackDirection.y, attackDirection.x) * Mathf.Rad2Deg;
        Collider2D[] hitColliders = Physics2D.OverlapBoxAll(boxCenter, boxSize, boxAngle);

        for (int colliderIndex = 0; colliderIndex < hitColliders.Length; colliderIndex++)
        {
            Collider2D hitCollider = hitColliders[colliderIndex];
            if (hitCollider == null || hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            IDamageable2D damageable = FindDamageable(hitCollider);
            if (damageable == null || !damagedTargets.Add(damageable))
            {
                continue;
            }

            Vector2 hitPoint = hitCollider.ClosestPoint(boxCenter + attackDirection * (activeAttackRange * 0.5f));
            damageable.ReceiveDamage(activeDamage, hitPoint, attackDirection);
        }
    }

    private Vector2 GetAttackOrigin()
    {
        if (player != null && player.BodyCollider != null)
        {
            Bounds bounds = player.BodyCollider.bounds;
            return new Vector2(bounds.center.x, bounds.center.y + bounds.extents.y * AttackOriginVerticalOffsetScale);
        }

        return transform.position;
    }

    private static IDamageable2D FindDamageable(Collider2D hitCollider)
    {
        MonoBehaviour[] components = hitCollider.GetComponentsInParent<MonoBehaviour>(true);
        for (int index = 0; index < components.Length; index++)
        {
            if (components[index] is IDamageable2D damageable)
            {
                return damageable;
            }
        }

        return null;
    }

    private bool ShouldCancelAttack()
    {
        return player == null || !player.isActiveAndEnabled || player.IsOnLadder ||
            inputReader == null || !inputReader.isActiveAndEnabled || !inputReader.IsMeleeAttackCommitted ||
            meleeWeaponController == null || !meleeWeaponController.isActiveAndEnabled ||
            weaponHoldController != null && (!weaponHoldController.isActiveAndEnabled ||
                weaponHoldController.CurrentWeapon != weaponAtAttackStart) ||
            reloadController != null && reloadController.IsReloading ||
            player.stateMachine != null && player.stateMachine.CurrentState == player.quickStepState ||
            GameManager.Instance != null && GameManager.Instance.IsGameplayInputLocked;
    }

    private void SuppressWeaponActionsLayer()
    {
        if (animator == null)
        {
            return;
        }

        if (suppressedWeaponActionsLayerIndex < 0)
        {
            int layerIndex = animator.GetLayerIndex(WeaponActionsLayerName);
            if (layerIndex < 0)
            {
                return;
            }

            suppressedWeaponActionsLayerIndex = layerIndex;
            weaponActionsLayerWeightBeforeAttack = animator.GetLayerWeight(layerIndex);
        }

        animator.SetLayerWeight(suppressedWeaponActionsLayerIndex, 0f);
    }

    private void CompleteAttack()
    {
        bool wasCommitted = isAttackCommitted;
        isAttackCommitted = false;
        if (animator != null)
        {
            if (wasCommitted)
            {
                animator.ResetTrigger(activeSlashTrigger);
                animator.SetBool(MeleeAttackActiveParameter, false);
            }

            if (suppressedWeaponActionsLayerIndex >= 0 && suppressedWeaponActionsLayerIndex < animator.layerCount)
            {
                animator.SetLayerWeight(suppressedWeaponActionsLayerIndex, weaponActionsLayerWeightBeforeAttack);
            }
        }

        suppressedWeaponActionsLayerIndex = -1;
        if (wasCommitted)
        {
            inputReader?.SetMeleeAttackCommitted(false);
            weaponHoldController?.SetMeleeMode(false);
        }

        meleeWeaponController?.SetAttackVisualActive(false);
        weaponAtAttackStart = null;
        hasAppliedDamage = false;
        hasDrawnKnife = false;
        hasResheathedKnife = true;
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            player = GetComponent<Player>();
        }

        if (inputReader == null)
        {
            inputReader = GetComponent<PlayerInputReader>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (weaponFireController == null)
        {
            weaponFireController = GetComponent<WeaponFireController>();
        }

        if (reloadController == null)
        {
            reloadController = GetComponentInChildren<PlayerReloadController>(true);
        }

        if (meleeWeaponController == null)
        {
            meleeWeaponController = GetComponent<PlayerMeleeWeaponController>();
        }

        if (weaponHoldController == null)
        {
            weaponHoldController = GetComponentInChildren<WeaponHoldController>(true);
        }
    }
}
