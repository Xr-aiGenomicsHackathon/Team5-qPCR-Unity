using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Team5.qPCR
{
    [RequireComponent(typeof(XRBaseInteractable))]
    public sealed class NearbySelectionFilter : MonoBehaviour, IXRSelectFilter
    {
        [SerializeField] private Transform leftController;
        [SerializeField] private Transform rightController;
        [SerializeField] private PPELessonController ppe;
        [SerializeField] private QpcrLessonController lesson;
        [SerializeField] private bool requiresPpe;
        [SerializeField] private float maximumReach = .22f;
        private XRBaseInteractable target;
        private float nextFeedback;
        public bool canProcess => isActiveAndEnabled;
        private void OnEnable() { target = GetComponent<XRBaseInteractable>(); target.selectFilters.Add(this); }
        private void OnDisable() { if (target != null) target.selectFilters.Remove(this); }
        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (requiresPpe && ppe != null && !ppe.IsComplete) return false;
            var source = interactor.transform;
            var origin = leftController != null && (source == leftController || source.IsChildOf(leftController)) ? leftController :
                rightController != null && (source == rightController || source.IsChildOf(rightController)) ? rightController : source;
            var point = origin.position;
            var distance = Vector3.Distance(point, transform.position);
            foreach (var collider in target.colliders)
                if (collider != null && collider.enabled) distance = Mathf.Min(distance, Vector3.Distance(point, collider.ClosestPoint(point)));
            if (distance <= maximumReach) return true;
            if (Time.unscaledTime >= nextFeedback)
            {
                nextFeedback = Time.unscaledTime + .65f;
                const string message = "Move closer: your hand must be within 22 cm of this object.";
                if (requiresPpe) lesson?.ReportRejectedAction(message); else ppe?.ReportRejectedAction(message);
            }
            return false;
        }
    }
}
