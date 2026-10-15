using UnityEngine;

namespace Team5.qPCR
{
    /// <summary>A fixed, fail-closed barrier; unlike the glass leaf, it never slides.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class DoorwayAccessGate : MonoBehaviour
    {
        [SerializeField] private PpeSlidingGlassDoor door;
        [SerializeField] private BoxCollider blocker;

        public bool IsPassageOpen => isActiveAndEnabled && door != null && blocker != null && door.IsOpen;

        private void Awake()
        {
            if (blocker == null) blocker = GetComponent<BoxCollider>();
            RefreshBlocker();
        }

        private void Start()
        {
            if (door == null || blocker == null)
                Debug.LogError("Doorway access: assign the sliding door and fixed BoxCollider. Passage remains blocked.", this);
        }

        private void LateUpdate() => RefreshBlocker();
        private void OnDisable()
        {
            if (blocker != null) blocker.enabled = true;
        }

        private void RefreshBlocker()
        {
            if (blocker == null) return;
            if (blocker.isTrigger) blocker.isTrigger = false;
            var shouldBlock = !IsPassageOpen;
            if (blocker.enabled != shouldBlock) blocker.enabled = shouldBlock;
        }

        /// <summary>
        /// Test the body-to-destination route, not the curved controller ray.
        /// Project onto the doorway's horizontal plane so aiming over the glass
        /// cannot bypass the lock. Expand the opening by the learner's body radius.
        /// </summary>
        public bool AllowsTeleport(Vector3 from, Vector3 destination, float bodyRadius)
        {
            if (door == null || blocker == null) return false;
            if (IsPassageOpen) return true;
            var start = blocker.transform.InverseTransformPoint(from);
            var end = blocker.transform.InverseTransformPoint(destination);
            start.y = end.y = blocker.center.y;
            var scale = blocker.transform.lossyScale;
            var radiusX = Mathf.Max(0f, bodyRadius) / Mathf.Max(.0001f, Mathf.Abs(scale.x));
            var radiusZ = Mathf.Max(0f, bodyRadius) / Mathf.Max(.0001f, Mathf.Abs(scale.z));
            var bounds = new Bounds(blocker.center, blocker.size + new Vector3(radiusX * 2f, 0f, radiusZ * 2f));
            if (bounds.Contains(start) || bounds.Contains(end)) return false;
            var delta = end - start;
            var distance = delta.magnitude;
            if (distance < .0001f) return true;
            return !bounds.IntersectRay(new Ray(start, delta / distance), out var hitDistance) || hitDistance > distance;
        }
    }
}
