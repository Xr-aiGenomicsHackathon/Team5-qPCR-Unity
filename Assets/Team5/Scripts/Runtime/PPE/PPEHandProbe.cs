using UnityEngine;

namespace Team5.qPCR
{
    /// <summary>Identifies a small trigger collider as the learner's left or right hand.</summary>
    public sealed class PPEHandProbe : MonoBehaviour
    {
        [SerializeField] private PPELessonController lesson;
        [SerializeField] private PPEHandSide side;
        [SerializeField] private Transform trackedController;
        [SerializeField] private Vector3 controllerLocalPosition = new Vector3(0, -.02f, -.06f);
        private Rigidbody body;

        public PPEHandSide Side => side;
        public Transform HandPose => trackedController != null ? trackedController : transform;

        private void OnEnable()
        {
            body = GetComponent<Rigidbody>();
            if (trackedController == null) trackedController = transform.parent;
            lesson?.RegisterHand(this);
        }

        private void FixedUpdate()
        {
            // A tracked parent's render pose is not a reliable PhysX kinematic target.
            // Drive the invisible probe explicitly without changing the tracked hand.
            if (trackedController == null || body == null || !body.isKinematic) return;
            body.MovePosition(trackedController.TransformPoint(controllerLocalPosition));
            body.MoveRotation(trackedController.rotation);
        }

        private void OnDisable()
        {
            lesson?.UnregisterHand(this);
        }

        private void Reset()
        {
            var sphere = GetComponent<SphereCollider>();
            if (sphere == null) sphere = gameObject.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 0.09f;

            var body = GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
    }
}
