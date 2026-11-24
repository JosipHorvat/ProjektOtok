using System;
using UnityEngine;

/// <summary>Publishes health feedback events for reusable character presentation systems.</summary>
public interface IDamageFeedbackSource
{
    /// <summary>Raised when positive damage is accepted.</summary>
    event Action<float, Vector2, Vector2> Damaged;

    /// <summary>Raised once when the character dies.</summary>
    event Action Died;

    /// <summary>Whether the character has already died.</summary>
    bool IsDead { get; }
}
