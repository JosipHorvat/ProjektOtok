using System;

/// <summary>Compatibility adapter exposing weapon-ammo operations over one owned ItemInstance.</summary>
[Serializable]
public sealed class WeaponAmmoState
{
    [NonSerialized] private ItemInstance weaponItemInstance;

    /// <summary>Creates an adapter bound to the specified owned weapon item instance.</summary>
    public WeaponAmmoState(ItemInstance ownedWeaponItem)
    {
        Bind(ownedWeaponItem);
    }

    /// <summary>Creates an unbound adapter for serializers and legacy callers.</summary>
    public WeaponAmmoState()
    {
    }

    /// <summary>Gets the rounds stored on the bound weapon item instance.</summary>
    public int LoadedRounds => weaponItemInstance != null ? weaponItemInstance.LoadedRounds : 0;

    /// <summary>Gets the owned weapon item represented by this adapter.</summary>
    public ItemInstance WeaponItemInstance => weaponItemInstance;

    /// <summary>Binds this adapter to an existing inventory-owned weapon instance.</summary>
    public bool Bind(ItemInstance ownedWeaponItem)
    {
        if (ownedWeaponItem == null || !(ownedWeaponItem.Definition is WeaponDefinition))
        {
            weaponItemInstance = null;
            return false;
        }

        weaponItemInstance = ownedWeaponItem;
        return true;
    }

    /// <summary>Clamps loaded rounds on the owned item to its current weapon definition.</summary>
    public void Initialize(WeaponDefinition definition)
    {
        if (weaponItemInstance == null || definition == null || weaponItemInstance.Definition != definition)
        {
            return;
        }

        weaponItemInstance.TrySetLoadedRounds(weaponItemInstance.LoadedRounds);
    }

    /// <summary>Returns the number of reserve rounds that fit in the bound magazine.</summary>
    public int GetTransferCapacity(WeaponDefinition definition)
    {
        return weaponItemInstance != null ? weaponItemInstance.GetLoadedTransferCapacity(definition) : 0;
    }

    /// <summary>Adds up to the requested number of rounds to the bound item instance.</summary>
    public int AddRounds(WeaponDefinition definition, int requestedRounds)
    {
        if (weaponItemInstance == null || weaponItemInstance.Definition != definition)
        {
            return 0;
        }

        return weaponItemInstance.AddLoadedRounds(requestedRounds);
    }

    /// <summary>Replaces the loaded magazine on the bound item instance.</summary>
    public int ReplaceMagazine(WeaponDefinition definition, int roundsToLoad)
    {
        if (weaponItemInstance == null || weaponItemInstance.Definition != definition ||
            !weaponItemInstance.TryReplaceMagazine(roundsToLoad))
        {
            return LoadedRounds;
        }

        return weaponItemInstance.LoadedRounds;
    }

    /// <summary>Spends one loaded round from the bound weapon item instance.</summary>
    public bool TrySpendRound(WeaponDefinition definition)
    {
        return weaponItemInstance != null && weaponItemInstance.Definition == definition &&
               weaponItemInstance.TrySpendLoadedRound();
    }
}
