/// <summary>Movement hooks used by a character's temporary hit reaction.</summary>
public interface IHitReactionMovement2D
{
    /// <summary>Applies a temporary movement-speed multiplier.</summary>
    void ApplyTemporaryMovementSlow(float speedMultiplier, float duration);

    /// <summary>Stops or resumes character-driven movement during a stagger.</summary>
    void SetStaggered(bool staggered);
}
