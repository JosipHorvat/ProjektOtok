using UnityEngine;

/// <summary>Identifies the surface represented by a ground collider.</summary>
public sealed class SurfaceIdentifier : MonoBehaviour
{
    [SerializeField] private SurfaceType surfaceType = SurfaceType.Default;

    /// <summary>Gets the surface type represented by this object.</summary>
    public SurfaceType SurfaceType => surfaceType;
}
