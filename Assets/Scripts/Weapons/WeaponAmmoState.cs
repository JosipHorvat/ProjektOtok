using UnityEngine;

/// <summary>Runtime loaded-ammunition state preserved independently from weapon instances.</summary>
[System.Serializable]
public sealed class WeaponAmmoState
{
    private int loadedRounds;
    private bool isInitialized;

    /// <summary>Current rounds loaded in this weapon's magazine.</summary>
    public int LoadedRounds => loadedRounds;

    /// <summary>Initializes from the definition once, then clamps to its current magazine capacity.</summary>
    public void Initialize(WeaponDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        int capacity = Mathf.Max(0, definition.MagazineCapacity);
        if (!isInitialized)
        {
            loadedRounds = capacity;
            isInitialized = true;
        }
        else
        {
            loadedRounds = Mathf.Clamp(loadedRounds, 0, capacity);
        }
    }

    /// <summary>Returns how many reserve rounds can fit in this magazine.</summary>
    public int GetTransferCapacity(WeaponDefinition definition)
    {
        if (definition == null)
        {
            return 0;
        }

        Initialize(definition);
        if (!isInitialized)
        {
            return 0;
        }

        return Mathf.Max(0, definition.MagazineCapacity - loadedRounds);
    }

    /// <summary>Adds up to the requested number of rounds and returns the number actually loaded.</summary>
    public int AddRounds(WeaponDefinition definition, int requestedRounds)
    {
        if (requestedRounds <= 0)
        {
            return 0;
        }

        int transferCapacity = GetTransferCapacity(definition);
        int roundsAdded = Mathf.Min(requestedRounds, transferCapacity);
        loadedRounds += roundsAdded;
        return roundsAdded;
    }

    /// <summary>Replaces the loaded magazine with no more than the weapon's capacity.</summary>
    public int ReplaceMagazine(WeaponDefinition definition, int roundsToLoad)
    {
        if (definition == null)
        {
            return loadedRounds;
        }

        int capacity = Mathf.Max(0, definition.MagazineCapacity);
        loadedRounds = Mathf.Clamp(roundsToLoad, 0, capacity);
        isInitialized = true;
        return loadedRounds;
    }

    /// <summary>Spends one loaded round when available; returns whether a round was spent.</summary>
    public bool TrySpendRound(WeaponDefinition definition)
    {
        Initialize(definition);
        if (!isInitialized || loadedRounds <= 0)
        {
            return false;
        }

        loadedRounds--;
        return true;
    }
}
