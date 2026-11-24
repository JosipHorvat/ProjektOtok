using UnityEngine;

/// <summary>Receives weapon-specific impact used to build a character's stagger response.</summary>
public interface IHitReactionReceiver2D
{
    /// <summary>Applies weapon-specific stagger impact for a successful hit in the supplied body region.</summary>
    void ReceiveHitReaction(float staggerImpact, Vector2 hitPoint, Vector2 hitDirection, CharacterHitRegion hitRegion);
}
