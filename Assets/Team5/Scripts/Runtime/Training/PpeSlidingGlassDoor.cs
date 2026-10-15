using UnityEngine;

namespace Team5.qPCR
{
    /// <summary>
    /// Opens only the moving door panel when the ordered PPE lesson completes.
    /// The door stays open until the scene is reloaded; it never closes on a learner.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PpeSlidingGlassDoor : MonoBehaviour
    {
        [Header("Existing PPE lesson")]
        [SerializeField] private PPELessonController ppe;

        [Header("Direction uses this panel's local axes")]
        [SerializeField] private Vector3 localSlideDirection = Vector3.left;

        [Tooltip("Travel in world metres, independent of the imported model's scale.")]
        [SerializeField, Min(0.01f)] private float slideDistance = 1.1f;
        [SerializeField, Min(0.1f)] private float openingSeconds = 1.2f;

        [Header("Optional sliding sound")]
        [SerializeField] private AudioSource doorAudio;
        [SerializeField] private AudioClip slideSound;

        private Rigidbody doorBody;
        private Vector3 openPosition;
        private Vector3 movementStart;
        private float elapsed;
        private bool opening;
        private bool isOpen;

        public bool IsOpen => isOpen;
        public bool IsOpening => opening;

        private void Awake()
        {
            doorBody = GetComponent<Rigidbody>();
            doorBody.isKinematic = true;
            doorBody.useGravity = false;

            var direction = localSlideDirection.sqrMagnitude > 0.0001f
                ? localSlideDirection.normalized
                : Vector3.left;

            // TransformDirection rotates the direction without scaling the distance.
            openPosition = doorBody.position +
                transform.TransformDirection(direction) * Mathf.Max(0.01f, slideDistance);

            if (ppe == null)
                Debug.LogWarning("Sliding glass door: assign the PPE Lesson Controller.", this);
        }

        private void OnEnable()
        {
            if (ppe == null) return;
            ppe.Completed += OpenAfterPpe;
            if (ppe.IsComplete) OpenAfterPpe();
        }

        private void OnDisable()
        {
            if (ppe != null) ppe.Completed -= OpenAfterPpe;
        }

        public void OpenAfterPpe()
        {
            // Direct calls and repeated events cannot bypass PPE or move the door twice.
            if (ppe == null || !ppe.IsComplete || opening || isOpen) return;

            movementStart = doorBody.position;
            elapsed = 0f;
            opening = true;

            if (doorAudio != null && slideSound != null)
                doorAudio.PlayOneShot(slideSound);
        }

        private void FixedUpdate()
        {
            if (!opening) return;

            elapsed += Time.fixedDeltaTime;
            var progress = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, openingSeconds));
            var eased = Mathf.SmoothStep(0f, 1f, progress);
            doorBody.MovePosition(Vector3.Lerp(movementStart, openPosition, eased));

            if (progress >= 1f)
            {
                opening = false;
                isOpen = true;
            }
        }
    }
}
