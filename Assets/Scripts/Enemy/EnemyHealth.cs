using System;
using UnityEngine;

/// <summary>Stores enemy health and exposes damage and death events.</summary>
[DisallowMultipleComponent]
public sealed class EnemyHealth : MonoBehaviour, IDamageable2D, IDamageFeedbackSource
{
    [Header("Health")]
    [Tooltip("Maximum health and starting health for each enemy instance.")]
    [SerializeField, Min(1f)] private float maximumHealth = 100f;

    private float currentHealth;

    /// <summary>The maximum health assigned to this enemy.</summary>
    public float MaximumHealth => maximumHealth;

    /// <summary>The enemy's current health.</summary>
    public float CurrentHealth => currentHealth;

    /// <summary>Gets the power value supplied by the most recent accepted damage source.</summary>
    public float LastDamageImpactPower { get; private set; } = 1f;

    /// <summary>Whether the enemy has already been defeated.</summary>
    public bool IsDead { get; private set; }

    /// <summary>Raised after health changes, with current and maximum health values.</summary>
    public event Action<float, float> HealthChanged;

    /// <summary>Raised when positive damage is accepted, before Died on a lethal hit.</summary>
    public event Action<float, Vector2, Vector2> Damaged;

    /// <summary>Raised once when health reaches zero.</summary>
    public event Action Died;

    private void Awake()
    {
        maximumHealth = Mathf.Max(1f, maximumHealth);
        currentHealth = maximumHealth;
        LastDamageImpactPower = 1f;
        IsDead = false;
    }

    private void OnValidate()
    {
        maximumHealth = Mathf.Max(1f, maximumHealth);
    }

    /// <summary>Applies incoming damage with a default impact power of one.</summary>
    public void ReceiveDamage(float damage, Vector2 hitPoint, Vector2 hitDirection)
    {
        ReceiveDamage(damage, hitPoint, hitDirection, 1f);
    }

    /// <summary>Applies incoming damage, recording impact power for the death response if this hit is lethal.</summary>
    public void ReceiveDamage(float damage, Vector2 hitPoint, Vector2 hitDirection, float impactPower)
    {
        if (IsDead || damage <= 0f)
        {
            return;
        }

        LastDamageImpactPower = Mathf.Max(0f, impactPower);
        currentHealth = Mathf.Max(0f, currentHealth - damage);
        HealthChanged?.Invoke(currentHealth, maximumHealth);
        Damaged?.Invoke(damage, hitPoint, hitDirection);

        if (currentHealth <= 0f)
        {
            IsDead = true;
            Died?.Invoke();
        }
    }
}
