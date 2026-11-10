using UnityEngine;

/// <summary>Shapes the visual character from vertical jump velocity without changing physics.</summary>
public sealed class PlayerSquash : MonoBehaviour
{
    private const float MinimumApexVelocity = 0.01f;
    private const float FullCircleRadians = Mathf.PI * 2f;
    private const float FinishedEpsilon = 0.0005f;

    [Header("Ascent Stretch")]
    [SerializeField, Min(0f)] private float ascentStretchY = 0.16f;
    [SerializeField, Min(0f)] private float ascentCompressX = 0.08f;
    [SerializeField, Min(0.01f)] private float fullAscentStretchSpeed = 10f;
    [SerializeField, Min(0.01f)] private float ascentResponse = 9f;

    [Header("Apex Squash")]
    [SerializeField, Min(0f)] private float apexSquashY = 0.09f;
    [SerializeField, Min(0f)] private float apexWidenX = 0.07f;
    [SerializeField, Min(MinimumApexVelocity)] private float apexVelocityBand = 4.5f;
    [SerializeField, Min(0.01f)] private float apexResponse = 14f;

    [Header("Fall Stretch")]
    [SerializeField, Min(0f)] private float fallStretchY = 0.19f;
    [SerializeField, Min(0f)] private float fallCompressX = 0.09f;
    [SerializeField, Min(0.01f)] private float fullFallStretchSpeed = 12f;
    [SerializeField, Min(0.01f)] private float fallResponse = 16f;

    [Header("Landing Impact")]
    [SerializeField, Min(0f)] private float landingSquashY = 0.28f;
    [SerializeField, Min(0f)] private float landingWidenX = 0.2f;
    [SerializeField, Range(0f, 1f)] private float minimumLandingIntensity = 0.5f;
    [SerializeField, Min(0f)] private float lightImpactSpeed = 3f;
    [SerializeField, Min(0.01f)] private float heavyImpactSpeed = 16f;
    [SerializeField, Min(0.01f)] private float landingDuration = 0.42f;
    [SerializeField, Min(0.1f)] private float landingFrequency = 3.1f;
    [SerializeField, Min(0f)] private float landingDamping = 8f;

    [Header("Visual Pivot")]
    [SerializeField] private bool pinScaleToFeet = true;

    private Vector3 baseLocalScale;
    private Vector3 baseLocalPosition;
    private Rigidbody2D playerRigidbody;
    private float feetOffsetY;
    private float landingIntensity = 1f;
    private float landingElapsed;
    private bool isAirborne;
    private bool isLanding;

    private void Awake()
    {
        baseLocalScale = transform.localScale;
        baseLocalPosition = transform.localPosition;
        playerRigidbody = GetComponentInParent<Rigidbody2D>();
        feetOffsetY = pinScaleToFeet ? CalculateFeetOffsetY() : 0f;
    }

    /// <summary>Begins velocity-driven airborne scaling; jump height remains controlled by the movement system.</summary>
    public void TriggerJumpTakeoff()
    {
        isLanding = false;
        isAirborne = true;
    }

    /// <summary>Plays a brief impact squash whose intensity follows the downward landing speed.</summary>
    /// <param name="impactSpeed">Downward speed in units per second at the moment of landing.</param>
    public void TriggerSquash(float impactSpeed)
    {
        float maximumImpact = Mathf.Max(heavyImpactSpeed, lightImpactSpeed + 0.01f);
        float impact = Mathf.InverseLerp(lightImpactSpeed, maximumImpact, impactSpeed);
        landingIntensity = Mathf.Lerp(minimumLandingIntensity, 1f, impact);
        isAirborne = false;
        isLanding = true;
        landingElapsed = 0f;
        ApplyLandingFrame();
    }

    /// <summary>Stops airborne squash and stretch while ladder poses control the character.</summary>
    public void SetLadderClimbing(bool isClimbing)
    {
        if (!isClimbing)
        {
            return;
        }

        isAirborne = false;
        isLanding = false;
        ApplyScale(Vector2.one);
    }


    private void LateUpdate()
    {
        if (isLanding)
        {
            landingElapsed += Time.deltaTime;
            ApplyLandingFrame();
            return;
        }

        if (!isAirborne)
        {
            ApplyScale(Vector2.one);
            return;
        }

        if (playerRigidbody == null)
        {
            isAirborne = false;
            ApplyScale(Vector2.one);
            return;
        }

        ApplyVelocityDrivenScale(playerRigidbody.linearVelocity.y);
    }

    private void ApplyVelocityDrivenScale(float verticalVelocity)
    {
        Vector2 targetScale;
        float response;
        float apexBand = Mathf.Max(apexVelocityBand, MinimumApexVelocity);

        if (verticalVelocity >= apexBand)
        {
            float stretch = Mathf.InverseLerp(apexBand, Mathf.Max(fullAscentStretchSpeed, apexBand + 0.01f), verticalVelocity);
            targetScale = new Vector2(
                1f - ascentCompressX * stretch,
                1f + ascentStretchY * stretch);
            response = ascentResponse;
        }
        else if (verticalVelocity > -apexBand)
        {
            float apexIntensity = 1f - Mathf.Abs(verticalVelocity) / apexBand;
            targetScale = new Vector2(
                1f + apexWidenX * apexIntensity,
                1f - apexSquashY * apexIntensity);
            response = apexResponse;
        }
        else
        {
            float fallStretch = Mathf.InverseLerp(apexBand, Mathf.Max(fullFallStretchSpeed, apexBand + 0.01f), -verticalVelocity);
            targetScale = new Vector2(
                1f - fallCompressX * fallStretch,
                1f + fallStretchY * fallStretch);
            response = fallResponse;
        }

        float blend = 1f - Mathf.Exp(-response * Time.deltaTime);
        ApplyScale(Vector2.Lerp(CurrentRelativeScale, targetScale, blend));
    }

    private Vector2 CurrentRelativeScale => new Vector2(
        baseLocalScale.x != 0f ? transform.localScale.x / baseLocalScale.x : 1f,
        baseLocalScale.y != 0f ? transform.localScale.y / baseLocalScale.y : 1f);

    private void ApplyLandingFrame()
    {
        float duration = Mathf.Max(landingDuration, 0.01f);
        float envelope = Mathf.Exp(-landingDamping * landingElapsed);

        if (landingElapsed >= duration || envelope < FinishedEpsilon)
        {
            isLanding = false;
            ApplyScale(Vector2.one);
            return;
        }

        // At impact the spring is fully compressed, then briefly overshoots and settles.
        float spring = envelope * Mathf.Cos(FullCircleRadians * landingFrequency * landingElapsed) * landingIntensity;
        ApplyScale(new Vector2(
            1f + landingWidenX * spring,
            1f - landingSquashY * spring));
    }

    private void ApplyScale(Vector2 scale)
    {
        transform.localScale = new Vector3(
            baseLocalScale.x * scale.x,
            baseLocalScale.y * scale.y,
            baseLocalScale.z);

        if (!pinScaleToFeet)
        {
            return;
        }

        transform.localPosition = new Vector3(
            baseLocalPosition.x,
            baseLocalPosition.y + feetOffsetY * (1f - scale.y),
            baseLocalPosition.z);
    }

    private float CalculateFeetOffsetY()
    {
        Collider2D bodyCollider = GetComponentInParent<Collider2D>();
        Transform parent = transform.parent;

        if (bodyCollider == null || parent == null)
        {
            return 0f;
        }

        Vector3 feetWorld = new Vector3(transform.position.x, bodyCollider.bounds.min.y, transform.position.z);
        float feetLocalY = parent.InverseTransformPoint(feetWorld).y;
        return feetLocalY - baseLocalPosition.y;
    }

    private void OnDisable()
    {
        isAirborne = false;
        isLanding = false;
        transform.localScale = baseLocalScale;
        transform.localPosition = baseLocalPosition;
    }
}
