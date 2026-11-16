using UnityEngine;
using UnityEngine.U2D.IK;

/// <summary>Configures leg IK and adds terrain-relative offsets to animated targets before the 2D IK manager solves.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-15)]
public sealed class PlayerLocomotionIKController : MonoBehaviour
{
    private const string LeftLegSolverPath = "IKSolvers/Solver_LegL";
    private const string RightLegSolverPath = "IKSolvers/Solver_LegR";
    private const string LeftFootSolverPath = "IKSolvers/Solver_FootL";
    private const string RightFootSolverPath = "IKSolvers/Solver_FootR";

    [Header("Leg and foot IK solvers")]
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

    private const float MinimumLength = 0.0001f;
    private const int ProbeCapacity = 16;

    [Header("Terrain foot placement")]
    [Tooltip("Adds terrain-relative corrections to the existing animated leg and toe targets.")]
    [SerializeField] private bool enableFootPlacement = true;
    [Tooltip("Height above each animated toe from which to cast down, in world units.")]
    [SerializeField, Min(0f)] private float footProbeLift = 0.6f;
    [Tooltip("Extra distance below each animated toe to search for walkable terrain.")]
    [SerializeField, Min(0f)] private float footProbeDepth = 0.9f;
    [Tooltip("Maximum world-space vertical correction to either foot; never changes the pelvis.")]
    [SerializeField, Min(0f)] private float maximumFootOffset = 0.35f;
    [Tooltip("Reject foot samples steeper than this angle. Match the motor's walkable slope limit.")]
    [SerializeField, Range(0f, 89f)] private float maximumFootSlope = 50f;
    [Tooltip("Fraction of measured leg length available to procedural placement, avoiding straight-knee singularities.")]
    [SerializeField, Range(0.5f, 1f)] private float maximumLegReach = 0.98f;
    [Tooltip("Minimum planted ankle-to-toe target spacing as a fraction of the foot IK chain length. Values below 1 preserve a small bend to avoid a fully extended IK singularity.")]
    [SerializeField, Range(0.85f, 0.999f)] private float minimumFootTargetReach = 0.995f;
    [Tooltip("Exponential response of terrain corrections while the animated toe is near stance height.")]
    [SerializeField, Min(0f)] private float stanceResponse = 18f;
    [Tooltip("Exponential response during swing and when blending back to animation in air or on probe loss.")]
    [SerializeField, Min(0f)] private float releaseResponse = 12f;
    [Tooltip("Ignore tiny support-offset changes during stance to keep collider seam noise from shaking the toes.")]
    [SerializeField, Min(0f)] private float offsetNoiseDeadZone = 0.015f;
    [Tooltip("Hold the last correction briefly through a missed toe probe while the player remains grounded.")]
    [SerializeField, Min(0f)] private float supportLossGracePeriod = 0.04f;
    [Tooltip("Animated lift above the starting toe clearance at which terrain adaptation fades out completely.")]
    [SerializeField, Min(0.001f)] private float swingReleaseHeight = 0.25f;

    [Header("Terrain ankle rotation")]
    [Tooltip("Rotate the animated ankle-to-toe target vector with the surface. Disabled preserves authored foot rotation.")]
    [SerializeField] private bool alignFeetToGround;
    [Tooltip("Maximum additive world-space ankle tilt; respects the animated ankle-to-toe shape.")]
    [SerializeField, Range(0f, 60f)] private float maximumAnkleTilt = 25f;

    private sealed class FootPlacement
    {
        internal LimbSolver2D LegSolver;
        internal Transform Ankle;
        internal Transform Toe;
        internal float RestClearance;
        internal float ToeChainLength;
        internal float Offset;
        internal float Tilt;
        internal float SupportLossTime;
        internal Vector3 AnimatedAnklePosition;
        internal Vector3 AnimatedToePosition;
        internal Quaternion AnimatedAnkleRotation;
        internal Quaternion AnimatedToeRotation;
        internal bool HasAppliedPose;
    }

    private readonly RaycastHit2D[] probeHits = new RaycastHit2D[ProbeCapacity];
    private Player player;
    private FootPlacement leftPlacement;
    private FootPlacement rightPlacement;

    private void Awake()
    {
        player = GetComponentInParent<Player>();
        if (TryResolveSolverReferences())
        {
            ApplySolverSettings();
        }
    }

    private void Start()
    {
        // Player caches its collider in Awake, after this component's earlier Awake order.
        leftPlacement = CreatePlacement(leftLegSolver, leftFootSolver);
        rightPlacement = CreatePlacement(rightLegSolver, rightFootSolver);
    }

    private FootPlacement CreatePlacement(LimbSolver2D legSolver, LimbSolver2D footSolver)
    {
        if (player == null || legSolver == null || footSolver == null)
        {
            return null;
        }

        Transform ankle = legSolver.GetChain(0).target;
        Transform toe = footSolver.GetChain(0).target;
        if (ankle == null || toe == null || ankle == toe)
        {
            return null;
        }

        float soleHeight = player.BodyCollider != null
            ? player.BodyCollider.bounds.min.y
            : player.transform.position.y;
        float toeChainLength = GetChainLength(footSolver);
        return new FootPlacement
        {
            LegSolver = legSolver,
            Ankle = ankle,
            Toe = toe,
            RestClearance = toe.position.y - soleHeight,
            ToeChainLength = toeChainLength
        };
    }

    private static float GetChainLength(LimbSolver2D solver)
    {
        float[] lengths = solver.GetChain(0).lengths;
        if (lengths == null)
        {
            return 0f;
        }

        float totalLength = 0f;
        foreach (float length in lengths)
        {
            totalLength += length;
        }
        return totalLength;
    }

    private void Update()
    {
        // Normal-mode Animator evaluates after Update. Restore the untouched local pose first,
        // including channels absent from a clip; never subtract a world offset after a facing flip.
        RestoreAnimatedPose(leftPlacement);
        RestoreAnimatedPose(rightPlacement);
    }

    private void LateUpdate()
    {
        // Package IKManager2D.LateUpdate runs at -10; this component runs at -15.
        // Do not invoke UpdateManager here: that would solve every arm/leg twice per frame.
        bool canPlace = enableFootPlacement && player != null && player.HasGroundContact
            && !player.IsOnLadder && player.stateMachine != null
            && (player.stateMachine.CurrentState == player.idleState
                || player.stateMachine.CurrentState == player.moveState)
            && (player.InputReader == null || !player.InputReader.IsMeleeAttackCommitted);
        UpdateFoot(leftPlacement, canPlace);
        UpdateFoot(rightPlacement, canPlace);
    }

    private void OnDisable()
    {
        RestoreAnimatedPose(leftPlacement);
        RestoreAnimatedPose(rightPlacement);
        ResetPlacement(leftPlacement);
        ResetPlacement(rightPlacement);
    }

    private static void ResetPlacement(FootPlacement foot)
    {
        if (foot == null)
        {
            return;
        }

        foot.Offset = 0f;
        foot.Tilt = 0f;
        foot.SupportLossTime = 0f;
    }

    private static void RestoreAnimatedPose(FootPlacement foot)
    {
        if (foot == null || !foot.HasAppliedPose)
        {
            return;
        }

        if (foot.Ankle != null)
        {
            foot.Ankle.localPosition = foot.AnimatedAnklePosition;
            foot.Ankle.localRotation = foot.AnimatedAnkleRotation;
        }
        if (foot.Toe != null)
        {
            foot.Toe.localPosition = foot.AnimatedToePosition;
            foot.Toe.localRotation = foot.AnimatedToeRotation;
        }
        foot.HasAppliedPose = false;
    }

    private void UpdateFoot(FootPlacement foot, bool canPlace)
    {
        if (foot == null || foot.Ankle == null || foot.Toe == null)
        {
            return;
        }

        foot.AnimatedAnklePosition = foot.Ankle.localPosition;
        foot.AnimatedToePosition = foot.Toe.localPosition;
        foot.AnimatedAnkleRotation = foot.Ankle.localRotation;
        foot.AnimatedToeRotation = foot.Toe.localRotation;

        Vector3 anklePosition = foot.Ankle.position;
        Vector3 toePosition = foot.Toe.position;
        float desiredOffset = 0f;
        float desiredTilt = 0f;
        float stanceWeight = 0f;
        RaycastHit2D hit = default;
        bool hasSupportSample = canPlace && TrySampleGround(toePosition, out hit);
        if (hasSupportSample)
        {
            foot.SupportLossTime = 0f;
            float supportHeight = player.CurrentGroundInfo.Point.y;
            float animatedLift = Mathf.Max(0f, toePosition.y - supportHeight - foot.RestClearance);
            stanceWeight = 1f - Mathf.SmoothStep(0f, 1f,
                animatedLift / Mathf.Max(swingReleaseHeight, MinimumLength));
            // Correct only terrain relief relative to body support. Retain authored clearance,
            // pelvis bob and leg swing instead of pinning both toes to the ground every frame.
            desiredOffset = Mathf.Clamp(hit.point.y - supportHeight,
                -maximumFootOffset, maximumFootOffset) * stanceWeight;
            if (stanceWeight > 0.5f && Mathf.Abs(desiredOffset - foot.Offset) <= offsetNoiseDeadZone)
            {
                // Ignore small support noise without suppressing meaningful slope changes.
                desiredOffset = foot.Offset;
            }
            if (alignFeetToGround)
            {
                float surfaceAngle = Mathf.Atan2(-hit.normal.x, hit.normal.y) * Mathf.Rad2Deg;
                desiredTilt = Mathf.Clamp(surfaceAngle, -maximumAnkleTilt, maximumAnkleTilt) * stanceWeight;
            }
        }
        else if (canPlace)
        {
            foot.SupportLossTime += Time.deltaTime;
            if (foot.SupportLossTime <= supportLossGracePeriod)
            {
                // Bridge only brief raycast gaps while still grounded; state/ground exits release immediately.
                desiredOffset = foot.Offset;
                desiredTilt = foot.Tilt;
            }
        }
        else
        {
            foot.SupportLossTime = 0f;
        }

        // A short ankle-to-toe target span makes the foot solver fold its middle joint.
        // Restore near-full chain reach only during stance, preserving authored swing poses.
        Vector3 ankleToToe = toePosition - anklePosition;
        float targetDistance = ankleToToe.magnitude;
        float minimumTargetDistance = foot.ToeChainLength * minimumFootTargetReach;
        if (stanceWeight > 0f && targetDistance > MinimumLength
            && targetDistance < minimumTargetDistance)
        {
            float correctedDistance = Mathf.Lerp(targetDistance, minimumTargetDistance, stanceWeight);
            toePosition = anklePosition + ankleToToe * (correctedDistance / targetDistance);
        }

        float response = Mathf.Lerp(releaseResponse, stanceResponse, stanceWeight);
        float blend = 1f - Mathf.Exp(-Mathf.Max(0f, response) * Time.deltaTime);
        foot.Offset = Mathf.Lerp(foot.Offset, desiredOffset, blend);
        foot.Tilt = Mathf.LerpAngle(foot.Tilt, desiredTilt, blend);
        float offset = ClampOffsetToReach(foot, anklePosition, foot.Offset);
        Quaternion tilt = Quaternion.Euler(0f, 0f, foot.Tilt);
        Vector3 placedAnkle = anklePosition + Vector3.up * offset;
        foot.Ankle.position = placedAnkle;
        foot.Toe.position = placedAnkle + tilt * (toePosition - anklePosition);
        foot.Ankle.rotation = tilt * foot.Ankle.rotation;
        foot.Toe.rotation = tilt * foot.Toe.rotation;
        foot.HasAppliedPose = true;
    }

    private float ClampOffsetToReach(FootPlacement foot, Vector3 anklePosition, float offset)
    {
        IKChain2D chain = foot.LegSolver.GetChain(0);
        float[] lengths = chain.lengths;
        if (chain.rootTransform == null || lengths == null)
        {
            return 0f;
        }

        float reach = 0f;
        foreach (float length in lengths)
        {
            reach += length;
        }
        reach *= maximumLegReach;
        Vector3 relative = anklePosition - chain.rootTransform.position;
        float verticalReachSquared = reach * reach - relative.x * relative.x - relative.z * relative.z;
        if (verticalReachSquared <= MinimumLength)
        {
            return 0f;
        }

        float verticalReach = Mathf.Sqrt(verticalReachSquared);
        // Include the authored height in the interval so blending out never alters animation
        // just because an existing clip itself exceeds the conservative procedural reach.
        float lower = Mathf.Min(-verticalReach, relative.y);
        float upper = Mathf.Max(verticalReach, relative.y);
        return Mathf.Clamp(relative.y + offset, lower, upper) - relative.y;
    }

    private bool TrySampleGround(Vector3 toePosition, out RaycastHit2D groundHit)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(player.whatIsGround);
        filter.useTriggers = false;
        Vector2 origin = (Vector2)toePosition + Vector2.up * footProbeLift;
        int count = Physics2D.Raycast(origin, Vector2.down, filter, probeHits,
            footProbeLift + footProbeDepth);
        groundHit = default;
        float nearestDistance = float.PositiveInfinity;
        for (int index = 0; index < count; index++)
        {
            RaycastHit2D hit = probeHits[index];
            if (hit.collider == null || hit.distance <= MinimumLength
                || hit.collider.transform.IsChildOf(player.transform)
                || Vector2.Angle(hit.normal, Vector2.up) > maximumFootSlope
                || hit.distance >= nearestDistance)
            {
                continue;
            }

            groundHit = hit;
            nearestDistance = hit.distance;
        }
        return groundHit.collider != null;
    }

    private bool TryResolveSolverReferences()
    {
        if (leftLegSolver == null)
        {
            Transform solverTransform = transform.Find(LeftLegSolverPath);
            leftLegSolver = solverTransform != null ? solverTransform.GetComponent<LimbSolver2D>() : null;
        }

        if (rightLegSolver == null)
        {
            Transform solverTransform = transform.Find(RightLegSolverPath);
            rightLegSolver = solverTransform != null ? solverTransform.GetComponent<LimbSolver2D>() : null;
        }

        if (leftFootSolver == null)
        {
            Transform solverTransform = transform.Find(LeftFootSolverPath);
            leftFootSolver = solverTransform != null ? solverTransform.GetComponent<LimbSolver2D>() : null;
        }

        if (rightFootSolver == null)
        {
            Transform solverTransform = transform.Find(RightFootSolverPath);
            rightFootSolver = solverTransform != null ? solverTransform.GetComponent<LimbSolver2D>() : null;
        }

        return leftLegSolver != null || rightLegSolver != null ||
               leftFootSolver != null || rightFootSolver != null;
    }

    private void ApplySolverSettings()
    {
        float clampedLegWeight = Mathf.Clamp01(legIkWeight);
        float clampedFootWeight = Mathf.Clamp01(footIkWeight);

        if (leftLegSolver != null)
        {
            leftLegSolver.constrainRotation = constrainAnkleRotation;
            leftLegSolver.weight = clampedLegWeight;
        }

        if (rightLegSolver != null)
        {
            rightLegSolver.constrainRotation = constrainAnkleRotation;
            rightLegSolver.weight = clampedLegWeight;
        }

        if (leftFootSolver != null)
        {
            leftFootSolver.weight = clampedFootWeight;
        }

        if (rightFootSolver != null)
        {
            rightFootSolver.weight = clampedFootWeight;
        }
    }
}
