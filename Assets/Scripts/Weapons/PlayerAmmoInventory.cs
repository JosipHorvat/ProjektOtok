using UnityEngine;

/// <summary>Compatibility facade for existing reload callers; all reserve rounds are owned by PlayerItemInventory ammo stacks.</summary>
[DisallowMultipleComponent]
public sealed class PlayerAmmoInventory : MonoBehaviour
{
    private PlayerItemInventory playerItemInventory;

    private void Awake()
    {
        playerItemInventory = GetComponentInParent<PlayerItemInventory>();
        if (playerItemInventory == null)
        {
            Debug.LogError($"{nameof(PlayerAmmoInventory)} requires {nameof(PlayerItemInventory)} on the same player hierarchy. Reserve ammo is not initialized independently.", this);
        }
    }

    /// <summary>Returns the reserve count for an ammo type from inventory-owned ammo stacks.</summary>
    public int GetReserveRounds(WeaponDefinition.AmmunitionType ammunitionType)
    {
        EnsureInventoryReference();
        return playerItemInventory != null ? playerItemInventory.GetReserveRounds(ammunitionType) : 0;
    }

    /// <summary>Adds rounds through the item inventory; succeeds only when the full request is accepted.</summary>
    public bool TryAddRounds(WeaponDefinition.AmmunitionType ammunitionType, int roundsToAdd)
    {
        EnsureInventoryReference();
        return playerItemInventory != null && playerItemInventory.TryAddRounds(ammunitionType, roundsToAdd);
    }

    /// <summary>Consumes rounds from inventory-owned ammo stacks.</summary>
    public bool TryConsumeRounds(WeaponDefinition.AmmunitionType ammunitionType, int roundsToConsume)
    {
        EnsureInventoryReference();
        return playerItemInventory != null && playerItemInventory.TryConsumeRounds(ammunitionType, roundsToConsume);
    }

    /// <summary>Atomically returns a magazine and withdraws rounds through the item inventory.</summary>
    public bool TryExchangeRounds(
        WeaponDefinition.AmmunitionType ammunitionType,
        int roundsReturned,
        int maximumRoundsToWithdraw,
        out int roundsWithdrawn)
    {
        EnsureInventoryReference();
        roundsWithdrawn = 0;
        return playerItemInventory != null && playerItemInventory.TryExchangeRounds(
            ammunitionType,
            roundsReturned,
            maximumRoundsToWithdraw,
            out roundsWithdrawn);
    }

    private void EnsureInventoryReference()
    {
        if (playerItemInventory == null)
        {
            playerItemInventory = GetComponentInParent<PlayerItemInventory>();
        }
    }
}
