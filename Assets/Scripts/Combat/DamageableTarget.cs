using UnityEngine;

/// <summary>Minimal health-bearing receiver for validating weapon damage in a test scene.</summary>
[DisallowMultipleComponent]
public sealed class DamageableTarget : MonoBehaviour, IDamageable2D
{
    [SerializeField, Min(0f)]
    private float maximumHealth = 100f;

    [SerializeField, Min(0f)]
    private float currentHealth = 100f;

    /// <summary>Maximum health available to this target.</summary>
    public float MaximumHealth => maximumHealth;

    /// <summary>Current health, visible and editable in the Inspector for testing.</summary>
    public float CurrentHealth => currentHealth;

    private void OnValidate()
    {
        maximumHealth = Mathf.Max(0f, maximumHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0f, maximumHealth);
    }

    /// <inheritdoc />
    public void ReceiveDamage(float damage, Vector2 hitPoint, Vector2 hitDirection)
    {
        if (damage <= 0f || currentHealth <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);
    }
}
