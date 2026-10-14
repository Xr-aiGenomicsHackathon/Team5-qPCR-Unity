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
        [SerializeField] private AmplificationGraphView resultsGraph;
        [SerializeField] private MeltCurveView resultsMelt;
        private int question;
        public void ConfigureGraphs(AmplificationGraphView graph, MeltCurveView melt)
        { resultsGraph = graph; resultsMelt = melt; }

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
            machineAudio?.PlayButton();
            question++;
            if (question >= 3)
            {
                interpretationText.text = "Completed: controls, Cq and melt analysis.\nKeep the amplified plate sealed. These are synthetic teaching results.";
                lesson.TryComplete(QpcrLessonState.InterpretResults);
                validControlsButton.interactable = invalidControlsButton.interactable = false;
            }
            else ShowQuestion();
        }

        public void RejectValidControls()
        {
            if (lesson == null || lesson.CurrentState != QpcrLessonState.InterpretResults)
                return;
            var message = question switch
            {
                1 => "Cq/Ct is the threshold-crossing cycle, not the final height of the curve.",
                2 => "One melt peak supports consistency, but does not prove the DNA product's identity.",
                _ => "The positive control rises; the NTC remains below threshold. These example control checks pass."
            };
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
            question = 0;
            if (validControlsButton != null) validControlsButton.interactable = true;
            if (invalidControlsButton != null) invalidControlsButton.interactable = true;
            ShowQuestion();
        }
        private void ShowQuestion()
        {
            if (interpretationText == null) return;
            interpretationText.color = Color.white;
            interpretationText.text = question switch
            {
                1 => $"2/3 · What does Cq/Ct measure?\nExample positive Cq: {AmplificationGraphView.Cq(true):0.0}; sample: {AmplificationGraphView.Cq(false):0.0}; NTC: undetermined.",
                2 => "3/3 · What does a single melt peak tell us?\nA peak supports one dominant melting transition; identity needs further evidence.",
                _ => "1/3 · Blue positive control rises; orange NTC is flat.\nDo these example controls meet their expected behaviour?"
            };
            validControlsButton.GetComponentInChildren<TMP_Text>().text = question switch
            { 1 => "Threshold-crossing cycle", 2 => "Supports consistency", _ => "Expected controls" };
            invalidControlsButton.GetComponentInChildren<TMP_Text>().text = question switch
            { 1 => "Final curve height", 2 => "Proves target identity", _ => "Controls failed" };
            if (resultsGraph != null) { resultsGraph.gameObject.SetActive(question != 2); resultsGraph.SetProgress(1f); }
            if (resultsMelt != null) { resultsMelt.gameObject.SetActive(question == 2); resultsMelt.SetVisible(true); }
        }

        private void OnStateChanged(QpcrLessonState state)
        {
            if (state == QpcrLessonState.NotStarted)
                ResetResults();
            if (state == QpcrLessonState.InterpretResults) ShowQuestion();
        }
    }
}
