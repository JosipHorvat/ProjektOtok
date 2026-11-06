using UnityEngine;

/// <summary>Rotates the Knight's weapon aim pivot toward the existing player aim input.</summary>
[DisallowMultipleComponent]
public sealed class KnightWeaponAimController : MonoBehaviour
{
    private const float MinimumAimDirectionSqrMagnitude = 0.0001f;

    [SerializeField]
    private Transform weaponAimPivot;

    [SerializeField]
    private Camera aimCamera;

    [SerializeField]
    private PlayerInputReader inputReader;

    [SerializeField, Range(-180f, 180f)]
    private float aimAngleOffset;

    private void Awake()
    {
        if (weaponAimPivot == null)
        {
            weaponAimPivot = transform.Find("WeaponAimPivot");
        }

        if (aimCamera == null)
        {
            aimCamera = Camera.main;
        }

        if (inputReader == null)
        {
            inputReader = FindAnyObjectByType<PlayerInputReader>();
        }
    }

    private void LateUpdate()
    {
        if (weaponAimPivot == null || aimCamera == null || inputReader == null)
        {
            return;
        }

        Vector2 aimScreenPosition = inputReader.AimInput;
        if (aimScreenPosition == Vector2.zero)
        {
            return;
        }

        float cameraDepth = Vector3.Dot(
            weaponAimPivot.position - aimCamera.transform.position,
            aimCamera.transform.forward);

        if (cameraDepth <= 0f)
        {
            return;
        }

        Vector3 mouseWorldPosition = aimCamera.ScreenToWorldPoint(
            new Vector3(aimScreenPosition.x, aimScreenPosition.y, cameraDepth));
        Vector2 aimDirection = mouseWorldPosition - weaponAimPivot.position;

        if (aimDirection.sqrMagnitude <= MinimumAimDirectionSqrMagnitude)
        {
            return;
        }

        if (weaponAimPivot.parent != null)
        {
            aimDirection = weaponAimPivot.parent.InverseTransformDirection(aimDirection);
        }

        float aimAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        weaponAimPivot.localRotation = Quaternion.Euler(0f, 0f, aimAngle + aimAngleOffset);
    }
}
