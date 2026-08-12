using UnityEngine;
using UnityEngine.Events;

/// <summary>Defines where the player appears after entering a scene.</summary>
public sealed class SceneTransitionDestination : MonoBehaviour
{
    [SerializeField] private SceneLocation location = SceneLocation.A;
    [SerializeField] private PlayerStateSetter playerStateSetter;

    public SceneLocation Location => location;
    public UnityEvent OnReachDestination;

    /// <summary>Places the active scene player at this entrance.</summary>
    public void PlacePlayer()
    {
        Player player = UnityEngine.Object.FindAnyObjectByType<Player>();
        if (player == null)
        {
            Debug.LogWarning($"SceneTransitionDestination '{name}': no Player found in the active scene.");
            return;
        }

        player.transform.SetPositionAndRotation(transform.position, transform.rotation);
        playerStateSetter?.ApplyArrivalState(player);
        OnReachDestination?.Invoke();
    }
}
