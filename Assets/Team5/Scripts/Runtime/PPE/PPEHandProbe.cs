using UnityEngine;

namespace Team5.qPCR
{
    /// <summary>Identifies a small trigger collider as the learner's left or right hand.</summary>
    public sealed class PPEHandProbe : MonoBehaviour
    {
        [SerializeField] private PPELessonController lesson;
        [SerializeField] private PPEHandSide side;

        public PPEHandSide Side => side;

        private void OnEnable()
        {
            lesson?.RegisterHand(this);
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
