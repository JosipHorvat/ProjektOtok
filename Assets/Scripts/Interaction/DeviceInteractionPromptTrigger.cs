using UnityEngine;

/// <summary>Shows an independent device-aware interaction prompt while the player is inside a 2D trigger.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class DeviceInteractionPromptTrigger : MonoBehaviour
{
    [SerializeField] private DeviceInteractionPromptUI promptUI;
    [SerializeField] private bool hideWhenPlayerLeaves = true;

    private Player playerInTrigger;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        if (promptUI == null)
        {
            Debug.LogWarning($"DeviceInteractionPromptTrigger '{name}': assign a DeviceInteractionPromptUI reference.", this);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Player detectedPlayer = other.GetComponentInParent<Player>();
        if (detectedPlayer == null)
        {
            return;
        }

        playerInTrigger = detectedPlayer;
        promptUI?.Show();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        Player detectedPlayer = other.GetComponentInParent<Player>();
        if (detectedPlayer != null && playerInTrigger == null)
        {
            playerInTrigger = detectedPlayer;
            promptUI?.Show();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<Player>() != playerInTrigger)
        {
            return;
        }

        playerInTrigger = null;
        if (hideWhenPlayerLeaves)
        {
            promptUI?.Hide();
        }
    }
}
