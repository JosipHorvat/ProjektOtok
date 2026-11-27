using UnityEngine;

/// <summary>Enables the authored replacement-part GameObject after its limb is lost.</summary>
[DisallowMultipleComponent]
public sealed class EnemyLimbVisuals2D : MonoBehaviour
{
    [Header("Replacement parts")]
    [Tooltip("The whole GameObject is the replacement part. Adjust its Transform during Play Mode, then copy its Transform values back to the prefab.")]
    [SerializeField] private SpriteRenderer headPart;
    [Tooltip("Move and rotate this GameObject to place the left shoulder replacement.")]
    [SerializeField] private SpriteRenderer leftArmPart;
    [Tooltip("Move and rotate this GameObject to place the right shoulder replacement.")]
    [SerializeField] private SpriteRenderer rightArmPart;
    [Tooltip("Move and rotate this GameObject to place the left hip replacement.")]
    [SerializeField] private SpriteRenderer leftLegPart;
    [Tooltip("Move and rotate this GameObject to place the right hip replacement.")]
    [SerializeField] private SpriteRenderer rightLegPart;

    private EnemyLimbCondition2D limbCondition;

    private void Awake()
    {
        limbCondition = GetComponentInParent<EnemyLimbCondition2D>();
        SetVisible(headPart, false);
        SetVisible(leftArmPart, false);
        SetVisible(rightArmPart, false);
        SetVisible(leftLegPart, false);
        SetVisible(rightLegPart, false);
    }

    private void OnEnable()
    {
        if (limbCondition == null)
        {
            limbCondition = GetComponentInParent<EnemyLimbCondition2D>();
        }

        if (limbCondition == null)
        {
            Debug.LogError($"{nameof(EnemyLimbVisuals2D)} on '{name}' requires an {nameof(EnemyLimbCondition2D)} in its parent hierarchy.", this);
            return;
        }

        limbCondition.LimbLost += HandleLimbLost;
        SetVisible(headPart, limbCondition.HasLostHead);
        SetVisible(leftArmPart, limbCondition.HasLostLeftArm);
        SetVisible(rightArmPart, limbCondition.HasLostRightArm);
        SetVisible(leftLegPart, limbCondition.HasLostLeftLeg);
        SetVisible(rightLegPart, limbCondition.HasLostRightLeg);
    }

    private void OnDisable()
    {
        if (limbCondition != null)
        {
            limbCondition.LimbLost -= HandleLimbLost;
        }
    }

    private void HandleLimbLost(EnemyLimbCondition2D condition, EnemyLimbCondition2D.Limb limb)
    {
        SetVisible(GetPart(limb), true);
    }

    private SpriteRenderer GetPart(EnemyLimbCondition2D.Limb limb)
    {
        switch (limb)
        {
            case EnemyLimbCondition2D.Limb.Head: return headPart;
            case EnemyLimbCondition2D.Limb.LeftArm: return leftArmPart;
            case EnemyLimbCondition2D.Limb.RightArm: return rightArmPart;
            case EnemyLimbCondition2D.Limb.LeftLeg: return leftLegPart;
            case EnemyLimbCondition2D.Limb.RightLeg: return rightLegPart;
            default: return null;
        }
    }

    private static void SetVisible(SpriteRenderer part, bool visible)
    {
        if (part != null)
        {
            // Never rewrite the transform: it is the authored placement control.
            part.enabled = visible && part.sprite != null;
        }
    }
}
