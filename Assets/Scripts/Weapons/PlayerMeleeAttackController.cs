using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Consumes melee input, commits a facing-direction stab, and applies direct 2D melee damage.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public sealed class PlayerMeleeAttackController : MonoBehaviour
{
    private const string StandardSlashTrigger = "MeleeSlash";
    private const string AimedSlashTrigger = "KnifeAimSlash";
    private const float DefaultSlashImpactTime = 0.29f;
    private const float DefaultSlashFinishTime = 0.58f;
    private const float DefaultSweepAngle = 100f;
    private const float DefaultSweepWidth = 0.35f;
    private const int SweepSampleCount = 9;

    [Header("Aimed knife slash")]
    [Tooltip("Duration of the aimed slash clip in seconds.")]
    [SerializeField, Min(MinimumAttackDuration)] private float aimedSlashDuration = DefaultAttackDuration;
    [Tooltip("Damage fallback time in seconds; match the authored damage event.")]
    [SerializeField, Min(0f)] private float aimedSlashImpactTime = DefaultSlashImpactTime;
    [Tooltip("Commitment ends at this recovery time; match the finish event.")]
    [SerializeField, Min(0f)] private float aimedSlashFinishTime = DefaultSlashFinishTime;
    [Tooltip("Angular span of the cutting arc centered on the captured direction.")]
    [SerializeField, Range(1f, 180f)] private float aimedSlashSweepAngle = DefaultSweepAngle;
    [Tooltip("Thickness of the swept blade volume in world units; reach comes from melee stats.")]
    [SerializeField, Min(0.01f)] private float aimedSlashSweepWidth = DefaultSweepWidth;
    private bool isAimedSlash;

    /// <summary>True only during a committed directional aimed slash, not a Q stab.</summary>
    public bool IsAimedSlashCommitted => isAttackCommitted && isAimedSlash;
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
    private float activeCriticalChance;
    private float activeCriticalMultiplier = 1f;
    private float activeStaggerImpact;
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
        if (!isAttackCommitted || isAimedSlash)
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
        if (isAimedSlash && Time.time < impactTime) return;
        ApplyMeleeDamage();
    }

    /// <summary>Animation event called after the committed recovery portion of the stab.</summary>
    public void AnimationEvent_MeleeAttackFinished()
    {
        if (isAimedSlash && Time.time < attackEndTime) return;
        CompleteAttack();
    }

    /// <summary>Begins a facing-direction stab from Q or the existing gamepad melee button.</summary>
    public bool TryBeginAttack(MeleeAttackInputSource inputSource)
    {
        if (inputSource == MeleeAttackInputSource.None) return false;
        return TryBeginAttackInternal(false, Vector2.zero);
    }

    /// <summary>Commits a no-gun sweeping slash using a world direction captured before combat input is masked.</summary>
    public bool TryBeginAimedSlash(Vector2 direction)
    {
        if (float.IsNaN(direction.x) || float.IsNaN(direction.y) ||
            float.IsInfinity(direction.x) || float.IsInfinity(direction.y) ||
            direction.sqrMagnitude < MinimumFacingDirectionMagnitude) return false;
        return TryBeginAttackInternal(true, direction.normalized);
    }

    private bool TryBeginAttackInternal(bool aimedSlash, Vector2 direction)
    {
        ResolveReferences();
        if (aimedSlash && (weaponHoldController == null || weaponHoldController.CurrentWeapon != null ||
            inputReader == null || !inputReader.FocusAimHeld)) return false;
        if (!isActiveAndEnabled || isAttackCommitted ||
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

        meleeWeaponController.GetActiveStats(out activeDamage, out activeAttackRange, out activeAttackCooldown, out activeStaggerImpact);
        meleeWeaponController.GetActiveCriticalStats(out activeCriticalChance, out activeCriticalMultiplier);
        activeAttackRange = Mathf.Max(MinimumAttackRange, activeAttackRange);
        activeAttackCooldown = Mathf.Max(MinimumAttackCooldown, activeAttackCooldown);
        activeSlashTrigger = StandardSlashTrigger;
        if (meleeWeaponController.TryGetActiveDefinition(out MeleeWeaponDefinition activeDefinition) &&
            !string.IsNullOrWhiteSpace(activeDefinition.AttackAnimationTrigger))
        {
            activeSlashTrigger = activeDefinition.AttackAnimationTrigger;
        }

        isAimedSlash = aimedSlash;
        if (aimedSlash) activeSlashTrigger = AimedSlashTrigger;
        attackDirection = aimedSlash ? direction : new Vector2(player.facingDir, 0f);
        if (Mathf.Abs(attackDirection.x) > MinimumFacingDirectionMagnitude)
        {
            player.SetFacingDirection(attackDirection.x > 0f ? 1 : -1);
        }

        float duration = Mathf.Max(MinimumAttackDuration, aimedSlash ? aimedSlashDuration : committedAttackDuration);
        impactTime = Time.time + (aimedSlash ? Mathf.Clamp(aimedSlashImpactTime, 0f, duration) : duration * Mathf.Clamp01(impactNormalizedTime));
        knifeDrawTime = aimedSlash ? Time.time : Time.time + duration * KnifeDrawNormalizedTime;
        knifeResheathTime = aimedSlash ? float.PositiveInfinity : Time.time + duration * KnifeResheathNormalizedTime;
        attackEndTime = Time.time + (aimedSlash ? Mathf.Clamp(aimedSlashFinishTime, aimedSlashImpactTime, duration) : duration + AttackTimeoutGrace);
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
            meleeWeaponController.SetAttackVisualActive(aimedSlash);
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
        return stateInfo.IsName(isAimedSlash ? "Base Layer.KnifeAimSlash" : "Base Layer.MeleeSlash") && stateInfo.normalizedTime >= 1f;
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
        if (isAimedSlash)
        {
            // Integrate the authored cutting arc at impact, not a horizontal thrust box.
            float centerAngle = Mathf.Atan2(attackDirection.y, attackDirection.x) * Mathf.Rad2Deg;
            for (int sample = 0; sample < SweepSampleCount; sample++)
            {
                float angle = centerAngle + Mathf.Lerp(-aimedSlashSweepAngle * 0.5f,
                    aimedSlashSweepAngle * 0.5f, sample / (float)(SweepSampleCount - 1));
                Vector2 radial = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                Vector2 center = attackOrigin + radial * (activeAttackRange * 0.5f);
                ApplyDamageToColliders(Physics2D.OverlapBoxAll(center,
                    new Vector2(activeAttackRange, aimedSlashSweepWidth), angle), center);
            }
            return;
        }
        Vector2 boxSize = new Vector2(
            activeAttackRange,
            Mathf.Max(MinimumHitboxHeight, activeAttackRange * AttackHeightScale));
        Vector2 boxCenter = attackOrigin + attackDirection * (activeAttackRange * 0.5f);
        float boxAngle = Mathf.Atan2(attackDirection.y, attackDirection.x) * Mathf.Rad2Deg;
        Collider2D[] hitColliders = Physics2D.OverlapBoxAll(boxCenter, boxSize, boxAngle);

        ApplyDamageToColliders(hitColliders, boxCenter);
    }

    private void ApplyDamageToColliders(Collider2D[] hitColliders, Vector2 boxCenter)
    {
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
            HitRegionMarker regionMarker = HitRegionMarker.FindAtHitPoint(hitCollider, damageable, hitPoint);
            CharacterHitRegion hitRegion = regionMarker != null ? regionMarker.Region : CharacterHitRegion.Torso;
            float regionDamageMultiplier = regionMarker != null ? regionMarker.DamageMultiplier : 1f;
            EnemyHealth targetHealth = damageable as EnemyHealth;
            CombatHitData hitData = new CombatHitData(
                activeDamage,
                regionDamageMultiplier,
                activeCriticalChance,
                activeCriticalMultiplier,
                hitRegion,
                hitPoint,
                attackDirection,
                targetHealth != null ? targetHealth.CurrentHealth : 0f,
                targetHealth != null ? targetHealth.MaximumHealth : 0f);
            IHitRegionDamageReceiver2D regionDamageReceiver = FindHitRegionDamageReceiver(hitCollider);
            if (hitData.FinalDamage <= 0f)
            {
                continue;
            }

            regionDamageReceiver?.ReceiveHitRegionDamage(hitData);
            damageable.ReceiveDamage(hitData.FinalDamage, hitPoint, attackDirection);
            IHitReactionReceiver2D hitReaction = FindHitReactionReceiver(hitCollider);
            hitReaction?.ReceiveHitReaction(activeStaggerImpact, hitPoint, attackDirection, hitRegion);
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

    private static IHitReactionReceiver2D FindHitReactionReceiver(Collider2D hitCollider)
    {
        MonoBehaviour[] components = hitCollider.GetComponentsInParent<MonoBehaviour>(true);
        for (int index = 0; index < components.Length; index++)
        {
            if (components[index] is IHitReactionReceiver2D hitReaction)
            {
                return hitReaction;
            }
        }

        return null;
    }

    private static IHitRegionDamageReceiver2D FindHitRegionDamageReceiver(Collider2D hitCollider)
    {
        MonoBehaviour[] components = hitCollider.GetComponentsInParent<MonoBehaviour>(true);
        for (int index = 0; index < components.Length; index++)
        {
            if (components[index] is IHitRegionDamageReceiver2D regionDamageReceiver)
            {
                return regionDamageReceiver;
            }
        }

        return null;
    }

    private static CharacterHitRegion FindHitRegion(Collider2D hitCollider)
    {
        HitRegionMarker marker = hitCollider.GetComponentInParent<HitRegionMarker>();
        return marker != null ? marker.Region : CharacterHitRegion.Torso;
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
