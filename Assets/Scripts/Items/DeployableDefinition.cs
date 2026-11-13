using UnityEngine;

/// <summary>Static placement and behavior data for deployable items.</summary>
[CreateAssetMenu(fileName = "DeployableDefinition", menuName = "Items/Deployable Definition")]
public sealed class DeployableDefinition : ItemDefinition
{
    [Header("Deployable")]
    [Tooltip("Prefab created by a future deployable-item handler.")]
    [SerializeField] private GameObject deployedPrefab;
    [Tooltip("Maximum placement distance from the player.")]
    [Min(0f)]
    [SerializeField] private float placementRange = 2f;
    [Tooltip("Whether the deployed object may be picked up again.")]
    [SerializeField] private bool canRecover;
    [Tooltip("Optional stable identifier for the deployable behavior handler.")]
    [SerializeField] private string deploymentActionId = string.Empty;

    /// <summary>Gets the equipment category.</summary>
    public override ItemCategory Category => ItemCategory.Deployable;

    /// <summary>Gets the deployed prefab.</summary>
    public GameObject DeployedPrefab => deployedPrefab;

    /// <summary>Gets the placement range.</summary>
    public float PlacementRange => placementRange;

    /// <summary>Gets whether the deployed object is recoverable.</summary>
    public bool CanRecover => canRecover;

    /// <summary>Gets the optional deployment handler ID.</summary>
    public string DeploymentActionId => deploymentActionId;

    /// <summary>Clamps authored placement range.</summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        placementRange = Mathf.Max(0f, placementRange);
    }
}
