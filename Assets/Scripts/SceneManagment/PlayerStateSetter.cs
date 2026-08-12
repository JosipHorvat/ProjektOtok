using UnityEngine;

/// <summary>Applies optional arrival-state changes to the player.</summary>
public sealed class PlayerStateSetter : MonoBehaviour
{
    [SerializeField] private bool flipLeft;

    /// <summary>Applies this entrance's arrival settings to the arriving player.</summary>
    public void ApplyArrivalState(Player player)
    {
        if (player == null) return;
        if (flipLeft) player.Flip();
    }
}
