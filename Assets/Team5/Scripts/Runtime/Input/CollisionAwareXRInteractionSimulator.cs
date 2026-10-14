using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace Team5.qPCR
{
    /// <summary>Project-owned adapter: desktop walking moves the constrained body, not tracked poses.</summary>
    public sealed class CollisionAwareXRInteractionSimulator : XRInteractionSimulator
    {
        [SerializeField] private XRBodyTransformer bodyTransformer;
        [SerializeField, Min(.1f)] private float groundSpeed = 1f;
        public float GroundSpeed => groundSpeed;
        public void Configure(XRBodyTransformer transformer) => bodyTransformer = transformer;

        protected override void ProcessPoseInput()
        {
            var movesHead = manipulatingFPS || (targetedDeviceInput & TargetedDevices.HMD) != 0;
            if (!movesHead) { base.ProcessPoseInput(); return; }
            var input = new Vector2(translateXInput.ReadValue(), translateZInput.ReadValue());
            var x = translateXSpeed; var y = translateYSpeed; var z = translateZSpeed;
            try
            {
                translateXSpeed = 0; translateYSpeed = 0; translateZSpeed = 0;
                base.ProcessPoseInput(); // Preserves reset, mouse-look and controller rotations.
            }
            finally { translateXSpeed = x; translateYSpeed = y; translateZSpeed = z; }
            QueueGroundNavigation(input, Time.deltaTime);
        }

        public void QueueGroundNavigation(Vector2 input, float seconds)
        {
            if (bodyTransformer == null || !bodyTransformer.isActiveAndEnabled || cameraTransform == null || seconds <= 0) return;
            var controller = bodyTransformer.xrOrigin?.Origin.GetComponent<CharacterController>();
            if (controller == null || !controller.enabled) return; // Never fall back to wall-bypassing Transform movement.
            input = Vector2.ClampMagnitude(input, 1f);
            var forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .001f) forward = Vector3.ProjectOnPlane(bodyTransformer.transform.forward, Vector3.up).normalized;
            var right = Vector3.Cross(Vector3.up, forward);
            var motion = (right * input.x + forward * input.y) * (groundSpeed * Mathf.Min(seconds, .1f));
            if (motion.sqrMagnitude > 0)
                bodyTransformer.QueueTransformation(new XROriginMovement { motion = motion, forceUnconstrained = false });
        }
    }
}
