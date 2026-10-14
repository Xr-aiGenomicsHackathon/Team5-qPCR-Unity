using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.qPCR
{
    public sealed class ResultsLessonController : MonoBehaviour
    {
        [SerializeField] private QpcrLessonController lesson;
        [SerializeField] private QpcrMachineAudio machineAudio;
        [SerializeField] private TMP_Text interpretationText;
        [SerializeField] private Button validControlsButton;
        [SerializeField] private Button invalidControlsButton;

        public void Configure(QpcrLessonController lessonController, QpcrMachineAudio audio,
            TMP_Text interpretation, Button validButton, Button invalidButton)
        {
            lesson = lessonController;
            machineAudio = audio;
            interpretationText = interpretation;
            validControlsButton = validButton;
            invalidControlsButton = invalidButton;
        }

        private void OnEnable()
        {
            validControlsButton?.onClick.AddListener(ConfirmValidControls);
            invalidControlsButton?.onClick.AddListener(RejectValidControls);
            if (lesson != null)
                lesson.StateChanged += OnStateChanged;
        }

        private void Start() => ResetResults();

        private void OnDisable()
        {
            validControlsButton?.onClick.RemoveListener(ConfirmValidControls);
            invalidControlsButton?.onClick.RemoveListener(RejectValidControls);
            if (lesson != null)
                lesson.StateChanged -= OnStateChanged;
        }

        public void ConfirmValidControls()
        {
            if (lesson == null || lesson.CurrentState != QpcrLessonState.InterpretResults)
                return;
            if (interpretationText != null)
            {
                interpretationText.text = "Correct. The positive control amplified and the NTC remained flat. The controls passed.";
                interpretationText.color = new Color(0.16f, 0.72f, 0.38f, 1f);
            }
            machineAudio?.PlayButton();
            lesson.TryComplete(QpcrLessonState.InterpretResults);
        }

        public void RejectValidControls()
        {
            if (lesson == null || lesson.CurrentState != QpcrLessonState.InterpretResults)
                return;
            const string message = "Look again: the positive-control curve rises, while the no-template control remains flat.";
            if (interpretationText != null)
            {
                interpretationText.text = message;
                interpretationText.color = new Color(0.94f, 0.18f, 0.22f, 1f);
            }
            lesson.ReportRejectedAction(message);
            machineAudio?.PlayError();
        }

        public void ResetResults()
        {
            if (interpretationText != null)
            {
                interpretationText.text = "Positive control: amplified\nNo-template control (NTC): flat\nChoose the correct interpretation.";
                interpretationText.color = new Color(0.15f, 0.2f, 0.28f, 1f);
            }
        }

        private void OnStateChanged(QpcrLessonState state)
        {
            if (state == QpcrLessonState.NotStarted)
                ResetResults();
        }
    }
}
