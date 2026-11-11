using UnityEngine;

/// <summary>Coordinates weapon reload requests with animation-synchronized ammunition events.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class PlayerReloadController : MonoBehaviour
{
    private const float MinimumReloadDuration = 0.05f;
    private const float AuthoredReloadClipLengthSeconds = 1f;

    private PlayerInputReader inputReader;
    private Player player;
    private PlayerAmmoInventory ammoInventory;
    private WeaponHoldController weaponHoldController;
    private WeaponFireController weaponFireController;
    private Animator animator;

    private HeldWeapon reloadingWeapon;
    private WeaponDefinition reloadingDefinition;
    private WeaponAmmoState reloadingAmmoState;
    private WeaponDefinition.ReloadStyle activeReloadStyle;
    private int activeSpeedParameterHash;
    private int activeCancelTriggerHash;
    private int activeReloadTriggerHash;
    private float nextRoundInsertionTime;
    private bool isReloading;
    private bool hasTransferredAmmo;

    /// <summary>Returns true while a reload is active and firing must be blocked.</summary>
    public bool IsReloading => isReloading;

    private void Awake()
    {
        player = GetComponentInParent<Player>();
        inputReader = GetComponentInParent<PlayerInputReader>();
        ammoInventory = GetComponentInParent<PlayerAmmoInventory>();
        weaponHoldController = GetComponentInChildren<WeaponHoldController>();
        weaponFireController = GetComponentInParent<WeaponFireController>();
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void Update()
    {
        if (isReloading)
        {
            if (ShouldCancelReload())
            {
                CancelReload();
            }

            return;
        }

        if (inputReader == null || !inputReader.ReloadPressedThisFrame)
        {
            return;
        }

        TryBeginReload();
    }

    private void OnDisable()
    {
        CancelReload();
    }

    /// <summary>Animation event at the magazine insertion point of a magazine-swap reload.</summary>
    public void AnimationEvent_MagazineInserted()
    {
        if (!IsActiveReloadSnapshot(WeaponDefinition.ReloadStyle.MagazineSwap) || hasTransferredAmmo)
        {
            return;
        }

        int roundsReturned = reloadingAmmoState.LoadedRounds;
        int magazineCapacity = Mathf.Max(0, reloadingDefinition.MagazineCapacity);
        if (!ammoInventory.TryExchangeRounds(
                reloadingDefinition.AmmoType,
                roundsReturned,
                magazineCapacity,
                out int roundsWithdrawn))
        {
            return;
        }

        int loadedAfterSwap = reloadingAmmoState.ReplaceMagazine(reloadingDefinition, roundsWithdrawn);
        hasTransferredAmmo = true;
        Debug.Log($"[Reload] Magazine swap: returned {roundsReturned}, loaded {loadedAfterSwap}, reserve {ammoInventory.GetReserveRounds(reloadingDefinition.AmmoType)}.", this);
    }

    /// <summary>Animation event for one round insertion in a round-by-round reload.</summary>
    public void AnimationEvent_RoundInserted()
    {
        if (!IsActiveReloadSnapshot(WeaponDefinition.ReloadStyle.RoundByRound) ||
            Time.time < nextRoundInsertionTime)
        {
            return;
        }

        if (reloadingAmmoState.GetTransferCapacity(reloadingDefinition) <= 0 ||
            ammoInventory.GetReserveRounds(reloadingDefinition.AmmoType) <= 0)
        {
            FinishReload();
            return;
        }

        if (!ammoInventory.TryConsumeRounds(reloadingDefinition.AmmoType, 1))
        {
            FinishReload();
            return;
        }

        int roundsAdded = reloadingAmmoState.AddRounds(reloadingDefinition, 1);
        if (roundsAdded != 1)
        {
            ammoInventory.TryAddRounds(reloadingDefinition.AmmoType, 1);
            FinishReload();
            return;
        }

        hasTransferredAmmo = true;
        nextRoundInsertionTime = Time.time + Mathf.Max(0.05f, reloadingDefinition.RoundInsertInterval * 0.75f);
        Debug.Log($"[Reload] Inserted one round: loaded {reloadingAmmoState.LoadedRounds}, reserve {ammoInventory.GetReserveRounds(reloadingDefinition.AmmoType)}.", this);

        if (reloadingAmmoState.GetTransferCapacity(reloadingDefinition) <= 0 ||
            ammoInventory.GetReserveRounds(reloadingDefinition.AmmoType) <= 0)
        {
            FinishReload();
        }
    }

    /// <summary>Animation event at the end of a magazine-swap reload clip.</summary>
    public void AnimationEvent_ReloadFinished()
    {
        if (isReloading && activeReloadStyle == WeaponDefinition.ReloadStyle.MagazineSwap &&
            IsActiveReloadSnapshot(WeaponDefinition.ReloadStyle.MagazineSwap))
        {
            FinishReload();
        }
    }

    private bool TryBeginReload()
    {
        if (IsGameplayInputBlocked() || player == null || player.IsOnLadder || IsQuickStepping() ||
            weaponHoldController == null || ammoInventory == null || animator == null)
        {
            return false;
        }

        HeldWeapon weapon = weaponHoldController.CurrentWeapon;
        WeaponDefinition definition = weapon != null ? weapon.Definition : null;
        WeaponAmmoState ammoState = weaponHoldController.CurrentAmmoState;
        if (definition == null || ammoState == null ||
            ammoState.GetTransferCapacity(definition) <= 0 ||
            ammoInventory.GetReserveRounds(definition.AmmoType) <= 0)
        {
            return false;
        }

        reloadingWeapon = weapon;
        reloadingDefinition = definition;
        reloadingAmmoState = ammoState;
        activeReloadStyle = definition.ReloadingStyle;
        hasTransferredAmmo = false;
        nextRoundInsertionTime = 0f;
        isReloading = true;
        weaponFireController?.NotifyReloadStarted();

        activeSpeedParameterHash = string.IsNullOrWhiteSpace(definition.ReloadSpeedParameter)
            ? 0
            : Animator.StringToHash(definition.ReloadSpeedParameter);
        activeCancelTriggerHash = string.IsNullOrWhiteSpace(definition.ReloadCancelTrigger)
            ? 0
            : Animator.StringToHash(definition.ReloadCancelTrigger);

        float targetReloadDuration = activeReloadStyle == WeaponDefinition.ReloadStyle.MagazineSwap
            ? definition.ReloadDuration
            : definition.RoundInsertInterval;
        float reloadStateSpeed = AuthoredReloadClipLengthSeconds / Mathf.Max(MinimumReloadDuration, targetReloadDuration);
        if (activeSpeedParameterHash != 0)
        {
            animator.SetFloat(activeSpeedParameterHash, reloadStateSpeed);
        }

        string reloadTrigger = activeReloadStyle == WeaponDefinition.ReloadStyle.MagazineSwap
            ? definition.MagazineReloadTrigger
            : definition.RoundByRoundReloadTrigger;
        activeReloadTriggerHash = string.IsNullOrWhiteSpace(reloadTrigger)
            ? 0
            : Animator.StringToHash(reloadTrigger);

        if (activeCancelTriggerHash != 0)
        {
            animator.ResetTrigger(activeCancelTriggerHash);
        }

        if (activeReloadTriggerHash != 0)
        {
            animator.ResetTrigger(activeReloadTriggerHash);
            animator.SetTrigger(activeReloadTriggerHash);
        }

        return true;
    }

    private bool IsActiveReloadSnapshot(WeaponDefinition.ReloadStyle expectedStyle)
    {
        return isReloading &&
               activeReloadStyle == expectedStyle &&
               reloadingWeapon != null &&
               reloadingDefinition != null &&
               reloadingAmmoState != null &&
               weaponHoldController != null &&
               weaponHoldController.CurrentWeapon == reloadingWeapon &&
               weaponHoldController.CurrentAmmoState == reloadingAmmoState;
    }

    private bool IsQuickStepping()
    {
        return (inputReader != null && inputReader.QuickStepPressedThisFrame) ||
               (player != null && player.stateMachine != null &&
                player.stateMachine.CurrentState == player.quickStepState);
    }

    private bool ShouldCancelReload()
    {
        return IsGameplayInputBlocked() ||
               player == null ||
               player.IsOnLadder ||
               IsQuickStepping() ||
               !IsActiveReloadSnapshot(activeReloadStyle);
    }

    private bool IsGameplayInputBlocked()
    {
        return GameManager.Instance != null && GameManager.Instance.IsGameplayInputLocked;
    }

    private void FinishReload()
    {
        if (!isReloading)
        {
            return;
        }

        isReloading = false;
        SignalAnimatorReloadExit();
        ClearReloadSnapshot();
    }

    /// <summary>Cancels the active reload while retaining all ammo already transferred.</summary>
    public void CancelReload()
    {
        if (!isReloading)
        {
            return;
        }

        isReloading = false;
        SignalAnimatorReloadExit();
        ClearReloadSnapshot();
    }

    private void SignalAnimatorReloadExit()
    {
        if (animator == null)
        {
            return;
        }

        if (activeReloadTriggerHash != 0)
        {
            animator.ResetTrigger(activeReloadTriggerHash);
        }

        if (activeCancelTriggerHash != 0)
        {
            animator.ResetTrigger(activeCancelTriggerHash);
            animator.SetTrigger(activeCancelTriggerHash);
        }

        if (activeSpeedParameterHash != 0)
        {
            animator.SetFloat(activeSpeedParameterHash, 1f);
        }
    }

    private void ClearReloadSnapshot()
    {
        reloadingWeapon = null;
        reloadingDefinition = null;
        reloadingAmmoState = null;
        activeSpeedParameterHash = 0;
        activeCancelTriggerHash = 0;
        activeReloadTriggerHash = 0;
        hasTransferredAmmo = false;
    }
}
