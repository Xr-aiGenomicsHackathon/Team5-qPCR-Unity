using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Team5.qPCR
{
    /// <summary>Keep normal XRI teleport behaviour, with one doorway prerequisite.</summary>
    public sealed class DoorAwareTeleportationArea : TeleportationArea
    {
        [SerializeField] private DoorwayAccessGate doorway;
        [SerializeField] private CharacterController learnerBody;

        private void Start()
        {
            if (doorway == null || learnerBody == null)
                Debug.LogError("Door-aware teleport: assign the doorway gate and XR CharacterController. Teleport requests are blocked until configured.", this);
        }

        public bool CanTeleportTo(Vector3 destination)
        {
            if (doorway == null || learnerBody == null) return false;
            var from = learnerBody.transform.TransformPoint(learnerBody.center);
            var scale = learnerBody.transform.lossyScale;
            var radius = learnerBody.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            return doorway.AllowsTeleport(from, destination, radius);
        }

        protected override bool GenerateTeleportRequest(IXRInteractor interactor, RaycastHit hit, ref TeleportRequest request)
        {
            return base.GenerateTeleportRequest(interactor, hit, ref request) && CanTeleportTo(request.destinationPosition);
        }

        public override bool IsSelectableBy(IXRSelectInteractor interactor)
        {
            if (!base.IsSelectableBy(interactor)) return false;
            return interactor is XRRayInteractor ray && ray.TryGetCurrent3DRaycastHit(out var hit) && CanTeleportTo(hit.point);
        }
    }
}
