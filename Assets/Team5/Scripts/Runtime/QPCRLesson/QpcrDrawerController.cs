using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Team5.qPCR
{
    public sealed class QpcrDrawerController : MonoBehaviour
    {
        [SerializeField] private QpcrLessonController lesson;
        [SerializeField] private QpcrPowerController power;
        [SerializeField] private QpcrPlateSocket plateSocket;
        [SerializeField] private Transform drawer;
        [SerializeField] private XRSimpleInteractable openInteractable;
        [SerializeField] private XRSimpleInteractable closeInteractable;
        [SerializeField] private QpcrMachineAudio machineAudio;
        [SerializeField] private Vector3 closedLocalPosition = new Vector3(0f, 0.11f, -0.21f);
        [SerializeField] private Vector3 openLocalPosition = new Vector3(0f, 0.11f, -0.43f);
        [SerializeField, Min(0.1f)] private float travelDuration = 0.65f;

        private Coroutine movement;

        public bool IsOpen { get; private set; }
        public bool IsMoving => movement != null;

        public void Configure(QpcrLessonController lessonController, QpcrPowerController powerController,
            Transform drawerTransform, XRSimpleInteractable openButton, XRSimpleInteractable closeButton,
            QpcrMachineAudio audio)
        {
            lesson = lessonController;
            power = powerController;
            drawer = drawerTransform;
            openInteractable = openButton;
            closeInteractable = closeButton;
            machineAudio = audio;
        }

        public void SetPlateSocket(QpcrPlateSocket socket) => plateSocket = socket;

        private void OnEnable()
        {
            openInteractable?.selectEntered.AddListener(OnOpenSelected);
            closeInteractable?.selectEntered.AddListener(OnCloseSelected);
            if (lesson != null)
                lesson.StateChanged += OnStateChanged;
        }

        private void Start() => ResetDrawer();

        private void OnDisable()
        {
            openInteractable?.selectEntered.RemoveListener(OnOpenSelected);
            closeInteractable?.selectEntered.RemoveListener(OnCloseSelected);
            if (lesson != null)
                lesson.StateChanged -= OnStateChanged;
        }

        public void ResetDrawer()
        {
            StopAllCoroutines();
            movement = null;
            IsOpen = false;
            if (drawer != null)
                drawer.localPosition = closedLocalPosition;
        }

        private void OnOpenSelected(SelectEnterEventArgs args)
        {
            if (lesson == null || lesson.CurrentState != QpcrLessonState.OpenDrawer || power == null || !power.IsPowered || IsMoving || IsOpen)
            {
                Reject(args, "Power on the instrument and complete the current task before opening the drawer.");
                return;
            }

            movement = StartCoroutine(MoveDrawer(openLocalPosition, true, () => lesson.TryComplete(QpcrLessonState.OpenDrawer)));
            (args.interactorObject as XRBaseInputInteractor)?.SendHapticImpulse(0.35f, 0.07f);
        }

        private void OnCloseSelected(SelectEnterEventArgs args)
        {
            if (lesson == null || lesson.CurrentState != QpcrLessonState.CloseDrawer || !IsOpen || IsMoving)
            {
                Reject(args, $"Complete the current task first: {lesson?.CurrentTarget}.");
                return;
            }
            if (plateSocket == null || !plateSocket.IsPlateSeated)
            {
                Reject(args, "The drawer cannot close until the plate is correctly aligned and seated.");
                return;
            }

            movement = StartCoroutine(MoveDrawer(closedLocalPosition, false, () => lesson.TryComplete(QpcrLessonState.CloseDrawer)));
            (args.interactorObject as XRBaseInputInteractor)?.SendHapticImpulse(0.35f, 0.07f);
        }

        private IEnumerator MoveDrawer(Vector3 destination, bool open, System.Action completed)
        {
            machineAudio?.PlayDrawer();
            var start = drawer == null ? destination : drawer.localPosition;
            var elapsed = 0f;
            while (drawer != null && elapsed < travelDuration)
            {
                elapsed += Time.deltaTime;
                drawer.localPosition = Vector3.Lerp(start, destination, Mathf.SmoothStep(0f, 1f, elapsed / travelDuration));
                yield return null;
            }
            if (drawer != null)
                drawer.localPosition = destination;
            IsOpen = open;
            movement = null;
            completed?.Invoke();
        }

        private void Reject(SelectEnterEventArgs args, string message)
        {
            lesson?.ReportRejectedAction(message);
            machineAudio?.PlayError();
            (args.interactorObject as XRBaseInputInteractor)?.SendHapticImpulse(0.72f, 0.12f);
        }

        private void OnStateChanged(QpcrLessonState state)
        {
            if (state == QpcrLessonState.NotStarted)
                ResetDrawer();
        }
    }
}
