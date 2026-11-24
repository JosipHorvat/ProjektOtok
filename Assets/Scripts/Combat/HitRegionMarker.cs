using UnityEngine;

/// <summary>Labels a hurtbox with its body region without depending on the visual rig hierarchy.</summary>
[DisallowMultipleComponent]
public sealed class HitRegionMarker : MonoBehaviour
{
    [Tooltip("Body region represented by the collider on this GameObject. Leave the enemy root collider as Torso.")]
    [SerializeField] private CharacterHitRegion region = CharacterHitRegion.Torso;

    /// <summary>The body region assigned to this hurtbox.</summary>
    public CharacterHitRegion Region => region;
}
