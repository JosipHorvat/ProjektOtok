using UnityEngine;
using UnityEngine.U2D.IK;

/// <summary>Configures leg and foot IK once; animation clips own the pelvis and IK targets.</summary>
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

    private void Awake()
    {
        if (TryResolveSolverReferences())
        {
            ApplySolverSettings();
        }
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
