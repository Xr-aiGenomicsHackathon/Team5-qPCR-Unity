using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Team5.qPCR
{
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public sealed class QpcrPlateHandling : MonoBehaviour
    {
        [SerializeField] private QpcrLessonController lesson;
        [SerializeField] private Transform home;
        [SerializeField] private QpcrPlateSocket socket;
        [SerializeField] private Renderer homeGuide;
        private XRGrabInteractable grab;
        private Rigidbody body;
        private Coroutine release;
        public bool IsCompromised { get; private set; }

        public void Configure(QpcrLessonController coordinator, Transform homeAnchor,
            QpcrPlateSocket plateSocket, Renderer guide)
        { lesson = coordinator; home = homeAnchor; socket = plateSocket; homeGuide = guide; }

        private void Awake() { grab = GetComponent<XRGrabInteractable>(); body = GetComponent<Rigidbody>(); }
        private void OnEnable()
        {
            if (grab == null) Awake();
            grab.selectExited.AddListener(OnRelease);
            if (lesson != null) lesson.StateChanged += OnStateChanged;
        }
        private void OnDisable()
        {
            grab.selectExited.RemoveListener(OnRelease);
            if (lesson != null) lesson.StateChanged -= OnStateChanged;
        }
        private void OnRelease(SelectExitEventArgs args)
        {
            if (!isActiveAndEnabled || args.isCanceled) return;
            if (release != null) StopCoroutine(release);
            release = StartCoroutine(AfterXriDetach());
        }
        private IEnumerator AfterXriDetach()
        {
            // XRI restores the original Rigidbody state and parent during detach.
            // Resolve snap/gravity after that restoration, not inside selectExited.
            yield return null;
            ResolveReleasedPose();
            release = null;
        }
        public void ResolveReleasedPose()
        {
            if (grab.isSelected || (socket != null && socket.IsPlateSeated)) return;
            if (!IsCompromised && socket != null && socket.TrySeatReleasedPlate()) return;
            if (!IsCompromised && IsWithinHomeZone())
            {
                SnapHome();
                if (lesson != null && lesson.CurrentState == QpcrLessonState.ReturnPlate)
                    lesson.TryComplete(QpcrLessonState.ReturnPlate);
                else lesson?.ReportInformation("Plate returned to its clean handoff position.");
                return;
            }
            transform.SetParent(null, true);
            body.isKinematic = false;
            body.useGravity = true;
            body.linearDamping = .1f;
            body.angularDamping = .8f;
            body.WakeUp();
        }
        public bool IsWithinHomeZone()
        {
            if (home == null) return false;
            var local = home.InverseTransformPoint(transform.position);
            return Mathf.Abs(local.x) <= .11f && Mathf.Abs(local.z) <= .085f &&
                local.y >= -.04f && local.y <= .12f;
        }
        public void SnapHome()
        {
            if (home == null) return;
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.isKinematic = true;
            transform.SetParent(home, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            body.position = home.position;
            body.rotation = home.rotation;
        }
        private void OnCollisionEnter(Collision collision)
        {
            if (collision.collider.name != "Epoxy_Floor" || IsCompromised) return;
            IsCompromised = true;
            lesson?.ReportRejectedAction("Plate dropped on the floor. Treat it as potentially damaged/contaminated. Press Reset for a fresh training plate; do not load this one.");
        }
        private void Update()
        {
            if (homeGuide != null)
                homeGuide.enabled = lesson != null && lesson.CurrentState == QpcrLessonState.ReturnPlate;
            // Only recover a plate that fell outside the level. A plate on the
            // actual floor remains visible so the learner sees the consequence.
            if (!grab.isSelected && transform.position.y < -.3f && home != null)
            {
                IsCompromised = true;
                SnapHome();
                lesson?.ReportRejectedAction("The dropped plate was recovered. Press Reset to start with a fresh training plate.");
            }
        }
        private void OnStateChanged(QpcrLessonState state)
        {
            if (state != QpcrLessonState.NotStarted) return;
            if (release != null) StopCoroutine(release);
            release = null;
            IsCompromised = false;
            SnapHome();
        }
    }
}
