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
