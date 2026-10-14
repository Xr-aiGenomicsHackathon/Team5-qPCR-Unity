using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Team5.qPCR
{
    public sealed class QpcrPowerController : MonoBehaviour
    {
        [SerializeField] private QpcrLessonController lesson;
        [SerializeField] private XRSimpleInteractable powerInteractable;
        [SerializeField] private Renderer powerIndicator;
        [SerializeField] private GameObject touchscreenRoot;
        [SerializeField] private QpcrMachineAudio machineAudio;

        public bool IsPowered { get; private set; }

        public void Configure(QpcrLessonController lessonController, XRSimpleInteractable interactable,
            Renderer indicator, GameObject screen, QpcrMachineAudio audio)
        {
            lesson = lessonController;
            powerInteractable = interactable;
            powerIndicator = indicator;
            touchscreenRoot = screen;
            machineAudio = audio;
        }

        private void OnEnable()
        {
            powerInteractable?.selectEntered.AddListener(OnPowerSelected);
            if (lesson != null)
                lesson.StateChanged += OnStateChanged;
        }

        private void Start() => ResetPower();

        private void OnDisable()
        {
            powerInteractable?.selectEntered.RemoveListener(OnPowerSelected);
            if (lesson != null)
                lesson.StateChanged -= OnStateChanged;
        }

        public void ResetPower()
        {
            IsPowered = false;
            if (touchscreenRoot != null)
                touchscreenRoot.SetActive(false);
            SetIndicator(new Color(0.05f, 0.09f, 0.12f, 1f));
        }

        private void OnPowerSelected(SelectEnterEventArgs args)
        {
            var accepted = lesson != null && lesson.CurrentState == QpcrLessonState.PowerOn && !IsPowered;
            if (!accepted)
            {
                lesson?.ReportRejectedAction(IsPowered
                    ? "The qPCR instrument is already powered on."
                    : $"Complete the current task first: {lesson?.CurrentTarget}.");
                machineAudio?.PlayError();
                (args.interactorObject as XRBaseInputInteractor)?.SendHapticImpulse(0.72f, 0.12f);
                return;
            }

            IsPowered = true;
            if (touchscreenRoot != null)
                touchscreenRoot.SetActive(true);
            SetIndicator(new Color(0.16f, 0.95f, 0.52f, 1f));
            machineAudio?.PlayPowerOn();
            (args.interactorObject as XRBaseInputInteractor)?.SendHapticImpulse(0.34f, 0.07f);
            lesson.TryComplete(QpcrLessonState.PowerOn);
        }

        private void OnStateChanged(QpcrLessonState state)
        {
            if (state == QpcrLessonState.NotStarted)
                ResetPower();
        }

        private void SetIndicator(Color color)
        {
            if (powerIndicator == null)
                return;
            var block = new MaterialPropertyBlock();
            powerIndicator.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            block.SetColor("_EmissionColor", color * 0.75f);
            powerIndicator.SetPropertyBlock(block);
        }
    }
}
