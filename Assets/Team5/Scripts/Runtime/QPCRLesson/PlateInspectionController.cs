using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Team5.qPCR
{
    public sealed class PlateInspectionController : MonoBehaviour
    {
        [SerializeField] private QpcrLessonController lesson;
        [SerializeField] private XRGrabInteractable plateGrab;
        [SerializeField] private Transform plateHomeAnchor;
        [SerializeField] private Rigidbody plateBody;
        [SerializeField] private PlateInspectionHotspot[] hotspots = Array.Empty<PlateInspectionHotspot>();
        [SerializeField, Min(0.05f)] private float maximumReach = 0.22f;
        [SerializeField, Min(0f)] private float repeatThrottleSeconds = 0.65f;
        [SerializeField, Min(0.05f)] private float returnDistance = 0.18f;

        private readonly HashSet<PlateInspectionKind> completed = new HashSet<PlateInspectionKind>();
        private float lastRejectedTime = float.NegativeInfinity;

        public PlateInspectionKind CurrentInspection => StateToKind(lesson == null ? QpcrLessonState.NotStarted : lesson.CurrentState);
        public bool IsComplete => completed.Count == 4;
        public int CompletedCount => completed.Count;
        public float MaximumReach => maximumReach;

        public event Action<int, int> ProgressChanged;
        public event Action AvailabilityChanged;

        public void Configure(QpcrLessonController lessonController, XRGrabInteractable grab,
            Transform homeAnchor, Rigidbody body, PlateInspectionHotspot[] inspectionHotspots)
        {
            lesson = lessonController;
            plateGrab = grab;
            plateHomeAnchor = homeAnchor;
            plateBody = body;
            hotspots = inspectionHotspots ?? Array.Empty<PlateInspectionHotspot>();
        }

        private void Awake()
        {
            if (plateGrab == null)
                plateGrab = GetComponent<XRGrabInteractable>();
            if (plateBody == null)
                plateBody = GetComponent<Rigidbody>();
        }

        private void Start() => ResetInspection();

        private void OnEnable()
        {
            if (lesson != null)
                lesson.StateChanged += OnLessonStateChanged;
            if (plateGrab != null)
                plateGrab.selectExited.AddListener(OnPlateReleased);
        }

        private void OnDisable()
        {
            if (lesson != null)
                lesson.StateChanged -= OnLessonStateChanged;
            if (plateGrab != null)
                plateGrab.selectExited.RemoveListener(OnPlateReleased);
        }

        public void BeginInspection()
        {
            completed.Clear();
            ProgressChanged?.Invoke(0, 4);
            RefreshHotspots();
        }

        public bool ConfirmCurrentVisualCheck()
        {
            if (lesson == null || !lesson.IsLessonActive)
            {
                lesson?.ReportRejectedAction("Press Start qPCR Lesson before confirming the plate.");
                return false;
            }

            var kind = CurrentInspection;
            if (kind == PlateInspectionKind.None)
            {
                lesson.ReportRejectedAction("There is no visual plate check to confirm at this step.");
                return false;
            }

            if (completed.Contains(kind))
            {
                lesson.ReportRejectedAction("This plate check is already complete.");
                return false;
            }

            var hotspot = FindHotspot(kind);
            completed.Add(kind);
            hotspot?.MarkComplete();
            ProgressChanged?.Invoke(completed.Count, 4);

            if (!lesson.TryComplete(KindToState(kind)))
            {
                completed.Remove(kind);
                ProgressChanged?.Invoke(completed.Count, 4);
                RefreshHotspots();
                return false;
            }

            lesson.ReportInformation($"{DisplayName(kind)} confirmed.");
            RefreshHotspots();

            // A visual inspection performed while the plate is still at its handoff station
            // does not need a separate return action. If the learner picked it up, the normal
            // ReturnPlate release check remains active.
            if (kind == PlateInspectionKind.Bubbles &&
                lesson.CurrentState == QpcrLessonState.ReturnPlate &&
                plateHomeAnchor != null &&
                (plateGrab == null || !plateGrab.isSelected) &&
                Vector3.Distance(transform.position, plateHomeAnchor.position) <= returnDistance)
            {
                SnapPlateHome();
                lesson.TryComplete(QpcrLessonState.ReturnPlate);
            }

            return true;
        }

        public InspectionAttemptResult TryInspect(PlateInspectionKind kind, IXRSelectInteractor inspectingInteractor)
        {
            if (lesson == null || !lesson.IsLessonActive || CurrentInspection == PlateInspectionKind.None)
                return Reject(InspectionAttemptOutcome.LessonNotReady, "Begin the qPCR lesson before inspecting the plate.");

            if (completed.Contains(kind))
                return Reject(InspectionAttemptOutcome.AlreadyComplete, "That checkpoint is already complete. Inspect the highlighted target.");

            if (kind != CurrentInspection)
                return Reject(InspectionAttemptOutcome.WrongCheckpoint, $"Inspect the highlighted {lesson.CurrentTarget} first.");

            if (plateGrab == null || !plateGrab.isSelected || plateGrab.interactorsSelecting.Count == 0)
                return Reject(InspectionAttemptOutcome.PlateNotHeld, "Grab and hold the plate with one hand before inspecting it.");

            var holdingInteractor = plateGrab.interactorsSelecting[0];
            if (inspectingInteractor == null || ReferenceEquals(holdingInteractor, inspectingInteractor))
                return Reject(InspectionAttemptOutcome.SameHand, "Keep one hand holding the plate and inspect with your other hand.");

            var hotspot = FindHotspot(kind);
            if (hotspot == null || Vector3.Distance(inspectingInteractor.transform.position, hotspot.transform.position) > maximumReach)
                return Reject(InspectionAttemptOutcome.TooFar, "Move your free hand closer. Inspection range is 22 centimetres.");

            completed.Add(kind);
            ProgressChanged?.Invoke(completed.Count, 4);
            hotspot.MarkComplete();
            lesson.TryComplete(KindToState(kind));
            RefreshHotspots();
            return InspectionAttemptResult.Correct($"{DisplayName(kind)} confirmed.");
        }

        public void ResetInspection()
        {
            completed.Clear();
            lastRejectedTime = float.NegativeInfinity;
            foreach (var hotspot in hotspots)
                hotspot?.ResetHotspot();
            SnapPlateHome();
            if (plateGrab != null)
                plateGrab.enabled = true;
            ProgressChanged?.Invoke(0, 4);
            RefreshHotspots();
        }

        private InspectionAttemptResult Reject(InspectionAttemptOutcome outcome, string message)
        {
            if (Time.unscaledTime - lastRejectedTime < repeatThrottleSeconds)
                return InspectionAttemptResult.Rejected(InspectionAttemptOutcome.Throttled, message);

            lastRejectedTime = Time.unscaledTime;
            lesson?.ReportRejectedAction(message);
            return InspectionAttemptResult.Rejected(outcome, message);
        }

        private void OnLessonStateChanged(QpcrLessonState state)
        {
            if (state == QpcrLessonState.InspectPlateId && completed.Count == 0)
                BeginInspection();

            if (plateGrab != null)
            {
                plateGrab.enabled = state is QpcrLessonState.InspectPlateId
                    or QpcrLessonState.InspectA1Marker
                    or QpcrLessonState.InspectOpticalSeal
                    or QpcrLessonState.InspectBubbles
                    or QpcrLessonState.ReturnPlate
                    or QpcrLessonState.LoadPlate;
            }

            RefreshHotspots();
        }

        private void OnPlateReleased(SelectExitEventArgs args)
        {
            if (lesson == null || lesson.CurrentState != QpcrLessonState.ReturnPlate || plateHomeAnchor == null)
                return;

            if (Vector3.Distance(transform.position, plateHomeAnchor.position) > returnDistance)
            {
                lesson.ReportRejectedAction("Release the plate inside the blue home marker before powering on the instrument.");
                return;
            }

            SnapPlateHome();
            lesson.TryComplete(QpcrLessonState.ReturnPlate);
        }

        private void SnapPlateHome()
        {
            if (plateHomeAnchor == null)
                return;
            transform.SetParent(plateHomeAnchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            if (plateBody != null)
            {
                if (!plateBody.isKinematic)
                {
                    plateBody.linearVelocity = Vector3.zero;
                    plateBody.angularVelocity = Vector3.zero;
                }
                plateBody.isKinematic = true;
            }
        }

        private void RefreshHotspots()
        {
            foreach (var hotspot in hotspots)
                hotspot?.RefreshAvailability(CurrentInspection, completed.Contains(hotspot.Kind));
            AvailabilityChanged?.Invoke();
        }

        private PlateInspectionHotspot FindHotspot(PlateInspectionKind kind)
        {
            foreach (var hotspot in hotspots)
                if (hotspot != null && hotspot.Kind == kind)
                    return hotspot;
            return null;
        }

        public static PlateInspectionKind StateToKind(QpcrLessonState state) => state switch
        {
            QpcrLessonState.InspectPlateId => PlateInspectionKind.PlateId,
            QpcrLessonState.InspectA1Marker => PlateInspectionKind.A1Marker,
            QpcrLessonState.InspectOpticalSeal => PlateInspectionKind.OpticalSeal,
            QpcrLessonState.InspectBubbles => PlateInspectionKind.Bubbles,
            _ => PlateInspectionKind.None
        };

        public static QpcrLessonState KindToState(PlateInspectionKind kind) => kind switch
        {
            PlateInspectionKind.PlateId => QpcrLessonState.InspectPlateId,
            PlateInspectionKind.A1Marker => QpcrLessonState.InspectA1Marker,
            PlateInspectionKind.OpticalSeal => QpcrLessonState.InspectOpticalSeal,
            PlateInspectionKind.Bubbles => QpcrLessonState.InspectBubbles,
            _ => QpcrLessonState.NotStarted
        };

        public static string DisplayName(PlateInspectionKind kind) => kind switch
        {
            PlateInspectionKind.PlateId => "Plate ID",
            PlateInspectionKind.A1Marker => "A1 marker",
            PlateInspectionKind.OpticalSeal => "Optical seal",
            PlateInspectionKind.Bubbles => "Bubble status",
            _ => "Plate checkpoint"
        };
    }
}
