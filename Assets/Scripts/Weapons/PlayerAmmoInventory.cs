using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Stores the player's reserve ammunition by configured ammunition type.</summary>
[DisallowMultipleComponent]
public sealed class PlayerAmmoInventory : MonoBehaviour
{
    [Serializable]
    private struct StartingReserveEntry
    {
        public WeaponDefinition.AmmunitionType ammunitionType;
        [Min(0)] public int rounds;
    }

    [Header("Starting reserve ammunition")]
    [Tooltip("Reserve rounds available when this player instance starts. Entries with the same ammunition type are combined.")]
    [SerializeField] private StartingReserveEntry[] startingReserves = Array.Empty<StartingReserveEntry>();

    private readonly Dictionary<WeaponDefinition.AmmunitionType, int> reserveRounds =
        new Dictionary<WeaponDefinition.AmmunitionType, int>();
    private bool hasInitialized;

    private void Awake()
    {
        InitializeReserves();
    }

    /// <summary>Returns the current reserve count for an ammunition type, or zero for an invalid type.</summary>
    public int GetReserveRounds(WeaponDefinition.AmmunitionType ammunitionType)
    {
        InitializeReserves();
        return IsValidAmmunitionType(ammunitionType) && reserveRounds.TryGetValue(ammunitionType, out int rounds)
            ? rounds
            : 0;
    }

    /// <summary>Adds positive rounds to a valid reserve pool; returns false for invalid input or integer overflow.</summary>
    public bool TryAddRounds(WeaponDefinition.AmmunitionType ammunitionType, int roundsToAdd)
    {
        InitializeReserves();
        if (!IsValidAmmunitionType(ammunitionType) || roundsToAdd <= 0)
        {
            return false;
        }

        int currentRounds = GetReserveRounds(ammunitionType);
        if (roundsToAdd > int.MaxValue - currentRounds)
        {
            return false;
        }

        reserveRounds[ammunitionType] = currentRounds + roundsToAdd;
        return true;
    }

    /// <summary>Consumes positive rounds only when the matching reserve pool contains the full requested amount.</summary>
    public bool TryConsumeRounds(WeaponDefinition.AmmunitionType ammunitionType, int roundsToConsume)
    {
        InitializeReserves();
        if (!IsValidAmmunitionType(ammunitionType) || roundsToConsume <= 0)
        {
            return false;
        }

        int currentRounds = GetReserveRounds(ammunitionType);
        if (currentRounds < roundsToConsume)
        {
            return false;
        }

        reserveRounds[ammunitionType] = currentRounds - roundsToConsume;
        return true;
    }

    /// <summary>Atomically returns an old magazine and withdraws up to the requested rounds from its ammo pool.</summary>
    public bool TryExchangeRounds(
        WeaponDefinition.AmmunitionType ammunitionType,
        int roundsReturned,
        int maximumRoundsToWithdraw,
        out int roundsWithdrawn)
    {
        InitializeReserves();
        roundsWithdrawn = 0;
        if (!IsValidAmmunitionType(ammunitionType) || roundsReturned < 0 || maximumRoundsToWithdraw <= 0)
        {
            return false;
        }

        int currentRounds = GetReserveRounds(ammunitionType);
        long availableRounds = (long)currentRounds + roundsReturned;
        roundsWithdrawn = (int)Math.Min(maximumRoundsToWithdraw, availableRounds);
        long remainingRounds = availableRounds - roundsWithdrawn;
        if (remainingRounds < 0 || remainingRounds > int.MaxValue)
        {
            roundsWithdrawn = 0;
            return false;
        }

        reserveRounds[ammunitionType] = (int)remainingRounds;
        return true;
    }

    private void InitializeReserves()
    {
        if (hasInitialized)
        {
            return;
        }

        hasInitialized = true;
        reserveRounds.Clear();
        if (startingReserves == null)
        {
            return;
        }

        for (int index = 0; index < startingReserves.Length; index++)
        {
            StartingReserveEntry entry = startingReserves[index];
            if (!IsValidAmmunitionType(entry.ammunitionType) || entry.rounds <= 0)
            {
                continue;
            }

            int currentRounds = GetReserveRounds(entry.ammunitionType);
            int roundsToAdd = Mathf.Min(entry.rounds, int.MaxValue - currentRounds);
            reserveRounds[entry.ammunitionType] = currentRounds + roundsToAdd;
        }
    }

    private static bool IsValidAmmunitionType(WeaponDefinition.AmmunitionType ammunitionType)
    {
        return Enum.IsDefined(typeof(WeaponDefinition.AmmunitionType), ammunitionType) &&
               ammunitionType != WeaponDefinition.AmmunitionType.None;
    }

    private void OnValidate()
    {
        if (startingReserves == null)
        {
            startingReserves = Array.Empty<StartingReserveEntry>();
            return;
        }

        for (int index = 0; index < startingReserves.Length; index++)
        {
            StartingReserveEntry entry = startingReserves[index];
            entry.rounds = Mathf.Max(0, entry.rounds);
            startingReserves[index] = entry;
        }
    }
}
