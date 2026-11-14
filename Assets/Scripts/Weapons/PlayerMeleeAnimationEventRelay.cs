using UnityEngine;

/// <summary>Forwards animation events from the Animator GameObject to the player's melee attack controller.</summary>
[DisallowMultipleComponent]
public sealed class PlayerMeleeAnimationEventRelay : MonoBehaviour
{
    private PlayerMeleeAttackController attackController;

    private void Awake()
    {
        ResolveAttackController();
    }

    /// <summary>Applies the committed melee hit on the authored strike frame.</summary>
    public void AnimationEvent_ApplyMeleeDamage()
    {
        ResolveAttackController();
        attackController?.AnimationEvent_ApplyMeleeDamage();
    }

    /// <summary>Ends the committed melee attack after its authored recovery frame.</summary>
    public void AnimationEvent_MeleeAttackFinished()
    {
        ResolveAttackController();
        attackController?.AnimationEvent_MeleeAttackFinished();
    }

    private void ResolveAttackController()
    {
        if (attackController == null)
        {
            attackController = GetComponentInParent<PlayerMeleeAttackController>();
        }
    }
}
