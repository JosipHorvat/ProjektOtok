using UnityEngine;

/// <summary>Creates a sprite-only stump visual parented to a surviving enemy rig transform.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class EnemySeveredPartVisuals2D : MonoBehaviour
{
    [SerializeField] private Sprite stumpSprite;
    [SerializeField] private Vector3 localOffset;
    [SerializeField] private Vector3 localRotation;
    [SerializeField] private SpriteRenderer sortingReference;

    /// <summary>Creates and configures a stump sprite under the surviving rig root.</summary>
    /// <param name="sprite">Approved stump sprite slice to display.</param>
    /// <param name="parent">Surviving rig transform that should carry the stump.</param>
    /// <param name="offset">Local position offset from the rig root.</param>
    /// <param name="rotation">Local Euler rotation from the rig root.</param>
    /// <param name="sortReference">Renderer whose sorting and rendering settings should be matched.</param>
    /// <returns>The configured stump visual, or null when the sprite or parent is missing.</returns>
    public static EnemySeveredPartVisuals2D Attach(
        Sprite sprite,
        Transform parent,
        Vector3 offset,
        Vector3 rotation,
        SpriteRenderer sortReference)
    {
        if (sprite == null || parent == null)
        {
            return null;
        }

        GameObject stumpObject = new GameObject("SeveredPartStump");
        stumpObject.transform.SetParent(parent, false);
        EnemySeveredPartVisuals2D stumpVisual = stumpObject.AddComponent<EnemySeveredPartVisuals2D>();
        stumpVisual.Configure(sprite, offset, rotation, sortReference);
        return stumpVisual;
    }

    private void Configure(Sprite sprite, Vector3 offset, Vector3 rotation, SpriteRenderer sortReference)
    {
        stumpSprite = sprite;
        localOffset = offset;
        localRotation = rotation;
        sortingReference = sortReference;

        Transform stumpTransform = transform;
        stumpTransform.localPosition = localOffset;
        stumpTransform.localRotation = Quaternion.Euler(localRotation);
        stumpTransform.localScale = Vector3.one;

        SpriteRenderer stumpRenderer = GetComponent<SpriteRenderer>();
        stumpRenderer.sprite = stumpSprite;
        if (sortingReference != null)
        {
            stumpRenderer.color = sortingReference.color;
            stumpRenderer.flipX = sortingReference.flipX;
            stumpRenderer.flipY = sortingReference.flipY;
            stumpRenderer.maskInteraction = sortingReference.maskInteraction;
            stumpRenderer.sharedMaterial = sortingReference.sharedMaterial;
            stumpRenderer.sortingLayerID = sortingReference.sortingLayerID;
            stumpRenderer.sortingOrder = sortingReference.sortingOrder;
            stumpRenderer.renderingLayerMask = sortingReference.renderingLayerMask;
        }
    }
}
