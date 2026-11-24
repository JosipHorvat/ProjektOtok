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

    /// <summary>Whether this enemy has already been defeated.</summary>
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
        IsDead = false;
    }

    private void OnValidate()
    {
        maximumHealth = Mathf.Max(1f, maximumHealth);
    }

    /// <summary>Applies incoming damage and raises the death event once at zero health.</summary>
    public void ReceiveDamage(float damage, Vector2 hitPoint, Vector2 hitDirection)
    {
        if (IsDead || damage <= 0f)
        {
            return;
        }

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
