using UnityEngine;

namespace Team5.qPCR
{
    // Trigger colliders on a non-physical, label-only layer. Never an XR interactable.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class LabLabelProxy : MonoBehaviour
    {
        [SerializeField] private LabLabelTarget target;
        public LabLabelTarget Target => target;
        public void Configure(LabLabelTarget value) => target = value;
    }
}
