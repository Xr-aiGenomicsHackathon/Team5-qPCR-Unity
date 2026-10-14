using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.qPCR
{
    public sealed class QpcrLessonUI : MonoBehaviour
    {
        [SerializeField] private QpcrLessonController lesson;
        [SerializeField] private PlateInspectionController inspection;
        [SerializeField] private NarrationController narration;
        [SerializeField] private TMP_Text stepText;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text targetText;
        [SerializeField] private TMP_Text instructionText;
        [SerializeField] private TMP_Text explanationText;
        [SerializeField] private TMP_Text actionHintText;
        [SerializeField] private TMP_Text feedbackText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button confirmObservationButton;
        [SerializeField] private TMP_Text confirmObservationLabel;
        [SerializeField] private Button resetButton;

        private Coroutine feedbackRoutine;

        public void Configure(QpcrLessonController lessonController, PlateInspectionController inspectionController,
            NarrationController narrationController, TMP_Text step, TMP_Text title, TMP_Text target,
            TMP_Text instruction, TMP_Text explanation, TMP_Text actionHint, TMP_Text feedback,
            TMP_Text progress, Button start, Button confirmObservation, TMP_Text confirmLabel, Button reset)
        {
            lesson = lessonController;
            inspection = inspectionController;
            narration = narrationController;
            stepText = step;
            titleText = title;
            targetText = target;
            instructionText = instruction;
            explanationText = explanation;
            actionHintText = actionHint;
            feedbackText = feedback;
            progressText = progress;
            startButton = start;
            confirmObservationButton = confirmObservation;
            confirmObservationLabel = confirmLabel;
            resetButton = reset;
        }

        private void OnEnable()
        {
            if (lesson != null)
            {
                lesson.StateChanged += OnStateChanged;
                lesson.FeedbackRequested += ShowFeedback;
            }
            if (inspection != null)
                inspection.ProgressChanged += ShowInspectionProgress;
            startButton?.onClick.AddListener(BeginLesson);
            confirmObservationButton?.onClick.AddListener(ConfirmVisualInspection);
            resetButton?.onClick.AddListener(ResetLesson);
        }

        private void OnDisable()
        {
            if (lesson != null)
            {
                lesson.StateChanged -= OnStateChanged;
                lesson.FeedbackRequested -= ShowFeedback;
            }
            if (inspection != null)
                inspection.ProgressChanged -= ShowInspectionProgress;
            startButton?.onClick.RemoveListener(BeginLesson);
            confirmObservationButton?.onClick.RemoveListener(ConfirmVisualInspection);
            resetButton?.onClick.RemoveListener(ResetLesson);
        }

        private void Start() => ResetDisplay();

        public void BeginLesson()
        {
            lesson?.BeginLesson();
            if (startButton != null)
                startButton.gameObject.SetActive(false);
        }

        public void ResetLesson()
        {
            lesson?.ResetLesson();
            inspection?.ResetInspection();
            narration?.ResetNarration();
            ResetDisplay();
        }

        public void ConfirmVisualInspection()
        {
            if (inspection == null || !inspection.ConfirmCurrentVisualCheck())
                return;
            SetConfirmButton(lesson == null ? QpcrLessonState.NotStarted : lesson.CurrentState);
        }

        public void ShowStep(string title, string target, string instruction)
        {
            if (titleText != null) titleText.text = title;
            if (targetText != null) targetText.text = $"Current target: {target}";
            if (instructionText != null) instructionText.text = instruction;
        }

        public void ShowFeedback(string message, bool isError)
        {
            if (feedbackRoutine != null)
                StopCoroutine(feedbackRoutine);
            feedbackRoutine = StartCoroutine(ShowFeedbackRoutine(message, isError));
        }

        public void ShowInspectionProgress(int completed, int total)
        {
            if (progressText != null)
                progressText.text = $"Plate inspection: {completed} / {total}";
        }

        public void ResetDisplay()
        {
            var presentation = QpcrLessonController.GetPresentation(QpcrLessonState.NotStarted);
            ShowStep(presentation.Title, presentation.Target, presentation.Instruction);
            if (explanationText != null) explanationText.text = presentation.Explanation;
            if (stepText != null) stepText.text = "qPCR guided lesson";
            if (actionHintText != null) actionHintText.text = QpcrLessonController.GetInputHint(QpcrLessonState.NotStarted);
            if (feedbackText != null) feedbackText.text = string.Empty;
            ShowInspectionProgress(0, 4);
            if (startButton != null) startButton.gameObject.SetActive(true);
            SetConfirmButton(QpcrLessonState.NotStarted);
        }

        private void OnStateChanged(QpcrLessonState state)
        {
            var presentation = QpcrLessonController.GetPresentation(state);
            ShowStep(presentation.Title, presentation.Target, presentation.Instruction);
            if (explanationText != null) explanationText.text = presentation.Explanation;
            if (stepText != null)
            {
                var step = QpcrLessonController.GetStepNumber(state);
                stepText.text = step <= 0 ? "qPCR guided lesson" : $"Step {step} of 15";
            }
            if (actionHintText != null)
                actionHintText.text = QpcrLessonController.GetInputHint(state);
            SetConfirmButton(state);
            if (state is not (QpcrLessonState.InspectPlateId or QpcrLessonState.InspectA1Marker
                or QpcrLessonState.InspectOpticalSeal or QpcrLessonState.InspectBubbles))
            {
                if (progressText != null) progressText.text = "qPCR workflow in progress";
            }

            narration?.Play(state.ToString(), presentation.Title,
                presentation.Instruction + "\n\n" + presentation.Explanation);
        }

        private void SetConfirmButton(QpcrLessonState state)
        {
            var visible = state is QpcrLessonState.InspectPlateId or QpcrLessonState.InspectA1Marker
                or QpcrLessonState.InspectOpticalSeal or QpcrLessonState.InspectBubbles;
            if (confirmObservationButton != null)
                confirmObservationButton.gameObject.SetActive(visible);
            if (confirmObservationLabel == null)
                return;
            confirmObservationLabel.text = state switch
            {
                QpcrLessonState.InspectPlateId => "Confirm Plate ID",
                QpcrLessonState.InspectA1Marker => "Confirm A1 Orientation",
                QpcrLessonState.InspectOpticalSeal => "Confirm Seal Is Secure",
                QpcrLessonState.InspectBubbles => "Confirm No Bubbles",
                _ => "Confirm"
            };
        }

        private IEnumerator ShowFeedbackRoutine(string message, bool isError)
        {
            if (feedbackText != null)
            {
                feedbackText.text = message;
                feedbackText.color = isError
                    ? new Color(0.94f, 0.18f, 0.22f, 1f)
                    : new Color(0.12f, 0.42f, 0.92f, 1f);
            }
            yield return new WaitForSecondsRealtime(isError ? 4f : 2.5f);
            if (feedbackText != null)
                feedbackText.text = string.Empty;
            feedbackRoutine = null;
        }
    }
}
