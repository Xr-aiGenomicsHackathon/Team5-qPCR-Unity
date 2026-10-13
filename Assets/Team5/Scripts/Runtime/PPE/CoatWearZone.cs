using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Team5.qPCR
{
    /// <summary>
    /// Put this trigger beneath the XR Main Camera. When the grabbed coat enters it,
    /// the coat is considered worn and its loose scene model is hidden.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CoatWearZone : MonoBehaviour
    {
        [SerializeField] private PPELessonController lesson;
        [SerializeField] private Transform coatRoot;
        [Tooltip("Assign the XR Grab Interactable on LabCoat_Grab. The coat is only worn while it is actually held.")]
        [SerializeField] private XRGrabInteractable coatGrab;
        [SerializeField] private AudioSource wearAudio;

        private Transform originalParent;
        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private Vector3 originalLocalScale;
        private Rigidbody coatBody;

        private void Awake()
        {
            var trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            CacheCoatPose();
        }

        private void OnEnable()
        {
            if (lesson != null) lesson.ResetRequested += RestoreCoat;
        }

        private void OnDisable()
        {
            if (lesson != null) lesson.ResetRequested -= RestoreCoat;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (coatRoot == null || !BelongsToCoat(other)) return;
            if (coatGrab == null) coatGrab = coatRoot.GetComponent<XRGrabInteractable>();
            if (coatGrab == null || !coatGrab.isSelected) return;
            if (lesson == null || !lesson.TryWearCoat()) return;

            if (coatBody != null)
            {
                coatBody.linearVelocity = Vector3.zero;
                coatBody.angularVelocity = Vector3.zero;
            }
            wearAudio?.Play();
            coatRoot.gameObject.SetActive(false);
        }

        private bool BelongsToCoat(Collider other)
        {
            if (other == null) return false;
            var candidate = other.attachedRigidbody != null
                ? other.attachedRigidbody.transform
                : other.transform;
            return candidate == coatRoot || candidate.IsChildOf(coatRoot) || coatRoot.IsChildOf(candidate);
        }

        private void CacheCoatPose()
        {
            if (coatRoot == null) return;
            originalParent = coatRoot.parent;
            originalLocalPosition = coatRoot.localPosition;
            originalLocalRotation = coatRoot.localRotation;
            originalLocalScale = coatRoot.localScale;
            coatBody = coatRoot.GetComponent<Rigidbody>();
            if (coatGrab == null) coatGrab = coatRoot.GetComponent<XRGrabInteractable>();
        }

        public void RestoreCoat()
        {
            if (coatRoot == null) return;
            coatRoot.SetParent(originalParent, false);
            coatRoot.localPosition = originalLocalPosition;
            coatRoot.localRotation = originalLocalRotation;
            coatRoot.localScale = originalLocalScale;
            coatRoot.gameObject.SetActive(true);
            if (coatBody == null) coatBody = coatRoot.GetComponent<Rigidbody>();
            if (coatBody == null) return;
            coatBody.linearVelocity = Vector3.zero;
            coatBody.angularVelocity = Vector3.zero;
        }

        private void Reset()
        {
            var trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = Vector3.zero;
            trigger.size = new Vector3(0.60f, 0.75f, 0.35f);
        }
    }
}
