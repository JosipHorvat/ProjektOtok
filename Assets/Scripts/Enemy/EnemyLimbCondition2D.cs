using System;
using UnityEngine;

/// <summary>Records enemy limb loss once and notifies interested gameplay systems.</summary>
[DisallowMultipleComponent]
public sealed class EnemyLimbCondition2D : MonoBehaviour
{
    public enum Limb
    {
        Head,
        LeftArm,
        RightArm,
        LeftLeg,
        RightLeg
    }

    private bool hasLostHead;
    private bool hasLostLeftArm;
    private bool hasLostRightArm;
    private bool hasLostLeftLeg;
    private bool hasLostRightLeg;

    /// <summary>Raised once after a limb is first recorded as lost.</summary>
    public event Action<EnemyLimbCondition2D, Limb> LimbLost;

    /// <summary>Whether the head has been recorded as lost.</summary>
    public bool HasLostHead => hasLostHead;

    /// <summary>Whether the left arm has been recorded as lost.</summary>
    public bool HasLostLeftArm => hasLostLeftArm;

    /// <summary>Whether the right arm has been recorded as lost.</summary>
    public bool HasLostRightArm => hasLostRightArm;

    /// <summary>Whether the left leg has been recorded as lost.</summary>
    public bool HasLostLeftLeg => hasLostLeftLeg;

    /// <summary>Whether the right leg has been recorded as lost.</summary>
    public bool HasLostRightLeg => hasLostRightLeg;

    /// <summary>Number of arms that remain attached.</summary>
    public int RemainingArmCount => (hasLostLeftArm ? 0 : 1) + (hasLostRightArm ? 0 : 1);

    /// <summary>Number of legs that remain attached.</summary>
    public int RemainingLegCount => (hasLostLeftLeg ? 0 : 1) + (hasLostRightLeg ? 0 : 1);

    /// <summary>Records a limb loss once and raises <see cref="LimbLost"/> for its first occurrence.</summary>
    /// <param name="limb">The limb that was successfully detached.</param>
    /// <returns>True if this call recorded the loss; false if it was already recorded.</returns>
    public bool RecordLimbLoss(Limb limb)
    {
        switch (limb)
        {
            case Limb.Head:
                if (hasLostHead)
                {
                    return false;
                }

                hasLostHead = true;
                break;
            case Limb.LeftArm:
                if (hasLostLeftArm)
                {
                    return false;
                }

                hasLostLeftArm = true;
                break;
            case Limb.RightArm:
                if (hasLostRightArm)
                {
                    return false;
                }

                hasLostRightArm = true;
                break;
            case Limb.LeftLeg:
                if (hasLostLeftLeg)
                {
                    return false;
                }

                hasLostLeftLeg = true;
                break;
            case Limb.RightLeg:
                if (hasLostRightLeg)
                {
                    return false;
                }

                hasLostRightLeg = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(limb), limb, "Unknown enemy limb.");
        }

        LimbLost?.Invoke(this, limb);
        return true;
    }
}
