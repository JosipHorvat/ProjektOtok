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

    /// <summary>Draws the hand knife at normalized time 0.20 of the stab clip.</summary>
    public void DrawMeleeKnife()
    {
        ResolveAttackController();
        attackController?.DrawMeleeKnife();
    }

    /// <summary>Resheathes the knife at normalized time 0.90 of the stab clip.</summary>
    public void SheatheMeleeKnife()
    {
        ResolveAttackController();
        attackController?.SheatheMeleeKnife();
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
