using UnityEngine;

/// <summary>Receives damage from 2D combat sources without coupling to a specific weapon system.</summary>
public interface IDamageable2D
{
    /// <summary>Applies damage and reports the hit point and hit direction.</summary>
    void ReceiveDamage(float damage, Vector2 hitPoint, Vector2 hitDirection);
}
