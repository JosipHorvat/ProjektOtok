using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Interactable save point that stores the player's current position.</summary>
[RequireComponent(typeof(Collider2D))]
public sealed class CheckpointSavePoint : MonoBehaviour, IInteractable
{
    [SerializeField] private string checkpointId;
    [SerializeField] private bool requireInteraction = true;

    private Collider2D checkpointCollider;
    private Player playerInRange;

    /// <summary>Stable unique identifier used to restore this checkpoint after loading.</summary>
    public string CheckpointId => checkpointId;

    private void Awake()
    {
        checkpointCollider = GetComponent<Collider2D>();
        checkpointCollider.isTrigger = true;
        EnsureCheckpointId();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureCheckpointId();
    }
#endif

    private void Update()
    {
        if (!requireInteraction || playerInRange == null || playerInRange.InputReader == null)
        {
            return;
        }

        if (playerInRange.InputReader.InteractPressedThisFrame)
        {
            Interact();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Player detectedPlayer = other.GetComponentInParent<Player>();
        if (detectedPlayer != null)
        {
            playerInRange = detectedPlayer;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<Player>() == playerInRange)
        {
            playerInRange = null;
        }
    }

    /// <summary>Saves this checkpoint using the player in the trigger, or the scene player as fallback.</summary>
    public void Interact()
    {
        if (SaveManager.Instance == null)
        {
            Debug.LogWarning($"CheckpointSavePoint '{name}': SaveManager is not available. Start through Bootstrap.unity.");
            return;
        }

        Player player = playerInRange ?? UnityEngine.Object.FindAnyObjectByType<Player>();
        if (player == null)
        {
            Debug.LogWarning($"CheckpointSavePoint '{name}': no Player found.");
            return;
        }

        SaveManager.Instance.SaveCheckpoint(
            CheckpointId,
            SceneManager.GetActiveScene().name,
            player.transform.position);

        Debug.Log($"CheckpointSavePoint '{name}': game saved.");
    }

    /// <summary>Sets the ID used by a checkpoint created by the save system.</summary>
    public void ConfigureRuntime(string id)
    {
        checkpointId = string.IsNullOrWhiteSpace(id) ? CreateCheckpointId() : id;
    }

    /// <summary>Moves the player to this checkpoint.</summary>
    public void PlacePlayer(Player player)
    {
        if (player != null)
        {
            player.transform.SetPositionAndRotation(transform.position, transform.rotation);
        }
    }

    private void EnsureCheckpointId()
    {
        if (string.IsNullOrWhiteSpace(checkpointId))
        {
            checkpointId = CreateCheckpointId();
        }
    }

    private string CreateCheckpointId()
    {
        return Guid.NewGuid().ToString("N");
    }
}
