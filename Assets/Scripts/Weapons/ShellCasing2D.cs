using UnityEngine;

/// <summary>Gives a spawned casing a brief physical ejection before it settles on the ground.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class ShellCasing2D : MonoBehaviour
{
    private const float DefaultLifetime = 3.5f;
    private const float MinimumLifetime = 0.1f;
    private const float DefaultSpin = 360f;

    [Min(0.1f)] public float lifetime = DefaultLifetime;

    private Rigidbody2D body;
    private bool initialized;

    /// <summary>Applies a world-space ejection velocity and rotation to this casing.</summary>
    public void Initialize(Vector2 ejectionVelocity, float spinDegreesPerSecond)
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        body.linearVelocity = ejectionVelocity;
        body.angularVelocity = spinDegreesPerSecond != 0f ? spinDegreesPerSecond : DefaultSpin;
        initialized = true;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        if (!initialized)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        Destroy(gameObject, Mathf.Max(MinimumLifetime, lifetime));
    }
}
