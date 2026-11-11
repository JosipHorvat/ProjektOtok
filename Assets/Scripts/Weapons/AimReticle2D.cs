using UnityEngine;

/// <summary>Moves the aim reticle and visualizes the active weapon's angular spread.</summary>
[DisallowMultipleComponent]
public sealed class AimReticle2D : MonoBehaviour
{
    private const float GamepadAimDistance = 12f;
    private const float MinimumAimInputSqrMagnitude = 0.04f;
    private const float MinimumConeDistance = 0.01f;
    private const float MaximumDisplayedSpreadDegrees = 80f;

    public PlayerInputReader inputReader;

    public Camera aimCamera;

    public Transform followOrigin;

    public WeaponHoldController weaponHoldController;

    public WeaponFireController weaponFireController;

    [Header("Reticle size")]
    [SerializeField, Min(0f)]
    private float aimedReticleScaleMultiplier = 1.5f;

    [SerializeField, Min(0f)]
    private float hipFireReticleScaleMultiplier = 2.25f;

    [Header("Spread cone")]
    [SerializeField, Min(0.001f)]
    private float spreadConeLineWidth = 0.025f;

    [SerializeField]
    private Color aimedSpreadConeColor = new Color(0.25f, 0.9f, 1f, 0.75f);

    [SerializeField]
    private Color hipFireSpreadConeColor = new Color(1f, 0.72f, 0.2f, 0.7f);

    [SerializeField]
    private int spreadConeSortingOrder = 20;

    private Vector3 baseLocalScale;
    private LineRenderer spreadConeRenderer;
    private Material spreadConeMaterial;

    private void Awake()
    {
        baseLocalScale = transform.localScale;

        if (inputReader == null)
        {
            inputReader = FindAnyObjectByType<PlayerInputReader>();
        }

        if (aimCamera == null)
        {
            aimCamera = Camera.main;
        }

        if (weaponHoldController == null)
        {
            weaponHoldController = FindAnyObjectByType<WeaponHoldController>();
        }

        if (weaponFireController == null)
        {
            weaponFireController = FindAnyObjectByType<WeaponFireController>();
        }

        CreateSpreadConeRenderer();
    }

    private void LateUpdate()
    {
        if (inputReader == null || aimCamera == null)
        {
            return;
        }

        Vector2 aimInput = inputReader.AimInput;
        Vector3 worldAimPoint = transform.position;
        Transform origin = followOrigin != null ? followOrigin : transform;

        bool isGamepadAim = inputReader.AimUsesGamepad;
        if (isGamepadAim)
        {
            if (aimInput.sqrMagnitude >= MinimumAimInputSqrMagnitude)
            {
                Vector3 worldDirection = aimCamera.transform.right * aimInput.x + aimCamera.transform.up * aimInput.y;
                worldAimPoint = origin.position + worldDirection.normalized * GamepadAimDistance;
            }
        }
        else
        {
            float cameraDepth = Vector3.Dot(origin.position - aimCamera.transform.position, aimCamera.transform.forward);
            if (cameraDepth > 0f)
            {
                worldAimPoint = aimCamera.ScreenToWorldPoint(new Vector3(aimInput.x, aimInput.y, cameraDepth));
                worldAimPoint.z = origin.position.z;
            }
        }

        transform.position = worldAimPoint;

        bool isAiming = inputReader.FocusAimHeld;
        float scaleMultiplier = isAiming ? aimedReticleScaleMultiplier : hipFireReticleScaleMultiplier;
        WeaponDefinition equippedDefinition = weaponHoldController != null && weaponHoldController.CurrentWeapon != null
            ? weaponHoldController.CurrentWeapon.Definition
            : null;
        if (isAiming && equippedDefinition != null && weaponFireController != null)
        {
            float baseSpread = equippedDefinition.AimedSpread;
            float maximumSpread = Mathf.Max(baseSpread, equippedDefinition.MaximumAimedSpread);
            float spreadRange = maximumSpread - baseSpread;
            float spreadProgress = spreadRange > 0f
                ? Mathf.Clamp01((weaponFireController.CurrentSpreadDegrees - baseSpread) / spreadRange)
                : 0f;
            scaleMultiplier = Mathf.Lerp(aimedReticleScaleMultiplier, hipFireReticleScaleMultiplier, spreadProgress);
        }

        transform.localScale = baseLocalScale * scaleMultiplier;
        UpdateSpreadCone(worldAimPoint, isAiming);
    }

    private void CreateSpreadConeRenderer()
    {
        spreadConeRenderer = GetComponent<LineRenderer>();
        if (spreadConeRenderer == null)
        {
            spreadConeRenderer = gameObject.AddComponent<LineRenderer>();
        }

        spreadConeRenderer.useWorldSpace = true;
        spreadConeRenderer.loop = false;
        spreadConeRenderer.positionCount = 4;
        spreadConeRenderer.widthMultiplier = spreadConeLineWidth;
        spreadConeRenderer.numCapVertices = 1;
        spreadConeRenderer.numCornerVertices = 0;
        spreadConeRenderer.sortingLayerName = "Player";
        spreadConeRenderer.sortingOrder = spreadConeSortingOrder;
        spreadConeRenderer.enabled = false;

        Shader coneShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (coneShader == null)
        {
            coneShader = Shader.Find("Sprites/Default");
        }

        if (coneShader != null)
        {
            spreadConeMaterial = new Material(coneShader)
            {
                name = "Runtime Aim Spread Cone"
            };
            spreadConeRenderer.material = spreadConeMaterial;
        }
    }

    private void UpdateSpreadCone(Vector3 aimPoint, bool isAiming)
    {
        if (spreadConeRenderer == null || weaponHoldController == null)
        {
            return;
        }

        HeldWeapon currentWeapon = weaponHoldController.CurrentWeapon;
        if (currentWeapon == null)
        {
            spreadConeRenderer.enabled = false;
            return;
        }

        Transform shotOrigin = currentWeapon.BulletEjectPoint;
        if (shotOrigin == null)
        {
            spreadConeRenderer.enabled = false;
            return;
        }

        WeaponDefinition definition = currentWeapon.Definition;
        float spreadDegrees = weaponFireController != null
            ? weaponFireController.CurrentSpreadDegrees
            : definition != null
                ? (isAiming ? definition.AimedSpread : definition.HipFireSpread)
                : 0f;

        if (spreadDegrees <= 0.01f)
        {
            spreadConeRenderer.enabled = false;
            return;
        }

        Vector3 coneOrigin = shotOrigin.position;
        coneOrigin.z = aimPoint.z;
        Vector2 toAimPoint = aimPoint - coneOrigin;
        float coneLength = toAimPoint.magnitude;
        if (coneLength < MinimumConeDistance)
        {
            spreadConeRenderer.enabled = false;
            return;
        }

        Vector2 centerDirection = toAimPoint / coneLength;
        if (isAiming)
        {
            Vector2 recoilAdjustedDirection = weaponHoldController.AimDirection;
            if (recoilAdjustedDirection.sqrMagnitude > 0.0001f)
            {
                centerDirection = recoilAdjustedDirection.normalized;
            }
        }

        Vector2 lateralDirection = new Vector2(-centerDirection.y, centerDirection.x);
        float displayedSpread = Mathf.Min(spreadDegrees, MaximumDisplayedSpreadDegrees);
        float halfWidth = Mathf.Tan(displayedSpread * Mathf.Deg2Rad) * coneLength;
        Vector3 coneTarget = coneOrigin + (Vector3)(centerDirection * coneLength);
        Vector3 leftEdge = coneTarget + (Vector3)(lateralDirection * halfWidth);
        Vector3 rightEdge = coneTarget - (Vector3)(lateralDirection * halfWidth);
        leftEdge.z = coneOrigin.z;
        rightEdge.z = coneOrigin.z;

        spreadConeRenderer.startColor = isAiming ? aimedSpreadConeColor : hipFireSpreadConeColor;
        spreadConeRenderer.endColor = spreadConeRenderer.startColor;
        spreadConeRenderer.widthMultiplier = spreadConeLineWidth;
        spreadConeRenderer.SetPosition(0, coneOrigin);
        spreadConeRenderer.SetPosition(1, leftEdge);
        spreadConeRenderer.SetPosition(2, rightEdge);
        spreadConeRenderer.SetPosition(3, coneOrigin);
        spreadConeRenderer.enabled = true;
    }

    private void OnDestroy()
    {
        if (spreadConeMaterial != null)
        {
            Destroy(spreadConeMaterial);
        }
    }
}
