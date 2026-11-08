using UnityEngine;
using UnityEngine.U2D.IK;

/// <summary>Tracks the leg IK targets and pelvis through a smooth, velocity-scaled grounded stride.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-15)]
public sealed class PlayerLocomotionIKController : MonoBehaviour
{
    private const float TwoPi = Mathf.PI * 2f;
    private const float MinimumSpeedDenominator = 0.01f;
    private const float MinimumStrideSpeed = 0.03f;

    [Header("Leg IK targets")]
    [SerializeField] private Transform leftLegTarget;
    [SerializeField] private Transform rightLegTarget;
    [SerializeField] private Transform pelvis;
    [SerializeField] private LimbSolver2D leftLegSolver;
    [SerializeField] private LimbSolver2D rightLegSolver;
    [SerializeField] private LimbSolver2D leftFootSolver;
    [SerializeField] private LimbSolver2D rightFootSolver;

    [Header("IK weights")]
    [Tooltip("When enabled, the leg IK forces the ankle effector to the leg target rotation.")]
    [SerializeField] private bool constrainAnkleRotation;
    [Tooltip("Leg solver influence. Lower values blend more of the original animated leg pose back in.")]
    [SerializeField, Range(0f, 1f)] private float legIkWeight = 1f;
    [Tooltip("Foot solver influence. Lower values blend more of the original animated foot pose back in.")]
    [SerializeField, Range(0f, 1f)] private float footIkWeight = 1f;

    [Header("Stride")]
    [Tooltip("Full stride cycles per second at maximum horizontal speed.")]
    [SerializeField, Min(0f)] private float strideCyclesPerSecond = 1.65f;
    [Tooltip("Peak-to-peak leg target travel at maximum horizontal speed, in local units.")]
    [SerializeField, Min(0f)] private float strideLengthAtMaxSpeed = 0.62f;
    [Tooltip("Maximum vertical lift of the leg target during its swing phase.")]
    [SerializeField, Min(0f)] private float stepHeight = 0.2f;
    [Tooltip("Fraction of each leg cycle spent swinging forward; the remainder is the grounded recovery step.")]
    [SerializeField, Range(0.2f, 0.8f)] private float swingFraction = 0.4f;

    [Header("Body response")]
    [Tooltip("Vertical pelvis travel synchronized with alternating leg plants.")]
    [SerializeField, Min(0f)] private float hipBounceAmplitude = 0.045f;
    [Tooltip("Smoothing time for blending into and out of the stride.")]
    [SerializeField, Min(0.01f)] private float strideBlendSmoothTime = 0.12f;

    private Player player;
    private Vector3 leftLegRestPosition;
    private Vector3 rightLegRestPosition;
    private float pelvisRestHeight;
    private float stridePhase;
    private float strideBlend;
    private float strideBlendVelocity;
    private bool hasRestPose;

    private void Awake()
    {
        if (!TryResolveRigReferences())
        {
            enabled = false;
            return;
        }

        leftLegRestPosition = leftLegTarget.localPosition;
        rightLegRestPosition = rightLegTarget.localPosition;
        pelvisRestHeight = pelvis.localPosition.y;
        ApplySolverSettings();
        hasRestPose = true;
    }

    private bool TryResolveRigReferences()
    {
        if (player == null)
        {
            player = GetComponentInParent<Player>();
        }

        if (leftLegTarget == null)
        {
            leftLegTarget = transform.Find("IKTargets/Target_LegL");
        }

        if (rightLegTarget == null)
        {
            rightLegTarget = transform.Find("IKTargets/Target_LegR");
        }

        if (pelvis == null)
        {
            pelvis = transform.Find("root/pelvis");
        }

        if (leftLegSolver == null)
        {
            Transform solverTransform = transform.Find("IKSolvers/Solver_LegL");
            leftLegSolver = solverTransform != null ? solverTransform.GetComponent<LimbSolver2D>() : null;
        }

        if (rightLegSolver == null)
        {
            Transform solverTransform = transform.Find("IKSolvers/Solver_LegR");
            rightLegSolver = solverTransform != null ? solverTransform.GetComponent<LimbSolver2D>() : null;
        }

        if (leftFootSolver == null)
        {
            Transform solverTransform = transform.Find("IKSolvers/Solver_FootL");
            leftFootSolver = solverTransform != null ? solverTransform.GetComponent<LimbSolver2D>() : null;
        }

        if (rightFootSolver == null)
        {
            Transform solverTransform = transform.Find("IKSolvers/Solver_FootR");
            rightFootSolver = solverTransform != null ? solverTransform.GetComponent<LimbSolver2D>() : null;
        }

        return player != null && leftLegTarget != null && rightLegTarget != null && pelvis != null;
    }

    private void LateUpdate()
    {
        if (!hasRestPose || !TryResolveRigReferences())
        {
            return;
        }

        float horizontalSpeed = player.rb != null
            ? Mathf.Abs(player.rb.linearVelocity.x)
            : 0f;
        float maximumSpeed = Mathf.Max(player.moveSpeed, MinimumSpeedDenominator);
        float normalizedSpeed = Mathf.Clamp01(horizontalSpeed / maximumSpeed);
        bool isGrounded = player.HasGroundContact;
        float targetBlend = isGrounded ? normalizedSpeed : 0f;
        strideBlend = Mathf.SmoothDamp(
            strideBlend,
            targetBlend,
            ref strideBlendVelocity,
            strideBlendSmoothTime);
        ApplySolverSettings();

        if (!isGrounded)
        {
            return;
        }

        if (normalizedSpeed > MinimumStrideSpeed)
        {
            float cadence = strideCyclesPerSecond * Mathf.Lerp(0.7f, 1f, normalizedSpeed);
            stridePhase = Mathf.Repeat(stridePhase + Time.deltaTime * cadence * TwoPi, TwoPi);
        }

        UpdateLegTargets();
        UpdateHipBounce();
    }

    private void UpdateLegTargets()
    {
        Vector3 centeredRest = (leftLegRestPosition + rightLegRestPosition) * 0.5f;
        Vector3 leftBase = Vector3.Lerp(leftLegRestPosition, centeredRest, strideBlend);
        Vector3 rightBase = Vector3.Lerp(rightLegRestPosition, centeredRest, strideBlend);

        float halfStride = strideLengthAtMaxSpeed * strideBlend * 0.5f;
        float legLift = stepHeight * strideBlend;

        leftLegTarget.localPosition = EvaluateStep(leftBase, stridePhase, halfStride, legLift);
        rightLegTarget.localPosition = EvaluateStep(rightBase, stridePhase + Mathf.PI, halfStride, legLift);
    }

    private void ApplySolverSettings()
    {
        if (leftLegSolver != null)
        {
            leftLegSolver.constrainRotation = constrainAnkleRotation;
            leftLegSolver.weight = legIkWeight;
        }

        if (rightLegSolver != null)
        {
            rightLegSolver.constrainRotation = constrainAnkleRotation;
            rightLegSolver.weight = legIkWeight;
        }

        if (leftFootSolver != null)
        {
            leftFootSolver.weight = footIkWeight;
        }

        if (rightFootSolver != null)
        {
            rightFootSolver.weight = footIkWeight;
        }
    }

    private Vector3 EvaluateStep(Vector3 restPosition, float phase, float halfStride, float liftHeight)
    {
        float cycleProgress = Mathf.Repeat(phase / TwoPi, 1f);
        float horizontalProgress;
        float verticalLift;

        if (cycleProgress < swingFraction)
        {
            float swingProgress = Mathf.SmoothStep(0f, 1f, cycleProgress / swingFraction);
            horizontalProgress = Mathf.Lerp(-1f, 1f, swingProgress);
            verticalLift = Mathf.Sin(swingProgress * Mathf.PI) * liftHeight;
        }
        else
        {
            float stanceProgress = Mathf.SmoothStep(
                0f,
                1f,
                (cycleProgress - swingFraction) / (1f - swingFraction));
            horizontalProgress = Mathf.Lerp(1f, -1f, stanceProgress);
            verticalLift = 0f;
        }

        return new Vector3(
            restPosition.x + horizontalProgress * halfStride,
            restPosition.y + verticalLift,
            restPosition.z);
    }

    private void UpdateHipBounce()
    {
        Vector3 pelvisPosition = pelvis.localPosition;
        float touchdownPhase = TwoPi * swingFraction;
        float bounce = -Mathf.Cos(stridePhase * 2f - touchdownPhase * 2f) * hipBounceAmplitude * strideBlend;
        pelvisPosition.y = pelvisRestHeight + bounce;
        pelvis.localPosition = pelvisPosition;
    }

    private void OnDisable()
    {
        if (!hasRestPose || !TryResolveRigReferences())
        {
            return;
        }

        leftLegTarget.localPosition = leftLegRestPosition;
        rightLegTarget.localPosition = rightLegRestPosition;

        Vector3 pelvisPosition = pelvis.localPosition;
        pelvisPosition.y = pelvisRestHeight;
        pelvis.localPosition = pelvisPosition;
    }
}
