using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Team5.qPCR
{
    public sealed class QpcrRunController : MonoBehaviour
    {
        [SerializeField] private QpcrLessonController lesson;
        [SerializeField] private QpcrTouchscreenController touchscreen;
        [SerializeField] private QpcrMachineAudio machineAudio;
        [SerializeField] private AmplificationGraphView amplificationGraph;
        [SerializeField] private MeltCurveView meltCurve;
        [SerializeField] private TMP_Text cycleText;
        [SerializeField] private TMP_Text stageText;
        [SerializeField] private TMP_Text temperatureText;
        [SerializeField] private UnityEngine.UI.Image progressFill;
        [SerializeField] private XRSimpleInteractable physicalStartButton;
        [SerializeField, Min(5f)] private float simulatedDurationSeconds = 45f;

        public bool IsRunning { get; private set; }
        public int CurrentCycle { get; private set; }

        public void Configure(QpcrLessonController lessonController, QpcrTouchscreenController screen,
            QpcrMachineAudio audio, AmplificationGraphView graph, MeltCurveView melt,
            TMP_Text cycle, TMP_Text stage, TMP_Text temperature, UnityEngine.UI.Image progress,
            XRSimpleInteractable physicalStart)
        {
            lesson = lessonController;
            touchscreen = screen;
            machineAudio = audio;
            amplificationGraph = graph;
            meltCurve = melt;
            cycleText = cycle;
            stageText = stage;
            temperatureText = temperature;
            progressFill = progress;
            physicalStartButton = physicalStart;
        }

        private void OnEnable()
        {
            if (touchscreen != null)
                touchscreen.StartRunRequested += StartRun;
            physicalStartButton?.selectEntered.AddListener(OnPhysicalStartSelected);
            if (lesson != null)
                lesson.StateChanged += OnStateChanged;
        }

        private void Start() => ResetRun();

        private void OnDisable()
        {
            if (touchscreen != null)
                touchscreen.StartRunRequested -= StartRun;
            physicalStartButton?.selectEntered.RemoveListener(OnPhysicalStartSelected);
            if (lesson != null)
                lesson.StateChanged -= OnStateChanged;
        }

        public void StartRun()
        {
            if (IsRunning || lesson == null || lesson.CurrentState != QpcrLessonState.StartRun ||
                touchscreen == null || !touchscreen.IsProtocolValid)
            {
                lesson?.ReportRejectedAction("The plate, drawer and validated protocol must all be ready before Start Run.");
                machineAudio?.PlayError();
                return;
            }

            lesson.TryComplete(QpcrLessonState.StartRun);
            touchscreen.ShowRunPanel();
            StartCoroutine(RunRoutine());
        }

        public void ResetRun()
        {
            StopAllCoroutines();
            IsRunning = false;
            CurrentCycle = 0;
            machineAudio?.StopRunLoop();
            amplificationGraph?.SetProgress(0f);
            meltCurve?.SetVisible(false);
            if (cycleText != null) cycleText.text = "Cycle 00 / 35";
            if (stageText != null) stageText.text = "Awaiting validated protocol";
            if (temperatureText != null) temperatureText.text = "— °C";
            if (progressFill != null) progressFill.fillAmount = 0f;
        }

        private IEnumerator RunRoutine()
        {
            IsRunning = true;
            machineAudio?.StartRunLoop();
            var elapsed = 0f;
            while (elapsed < simulatedDurationSeconds)
            {
                elapsed += Time.deltaTime;
                var normalized = Mathf.Clamp01(elapsed / simulatedDurationSeconds);
                var cyclingProgress = Mathf.Clamp01((normalized - .08f) / .80f);
                var exactCycle = cyclingProgress * 35f;
                CurrentCycle = Mathf.Clamp(Mathf.FloorToInt(exactCycle) + 1, 1, 35);
                var phase = exactCycle - Mathf.Floor(exactCycle);
                string stage;
                float temperature;
                if (phase < .20f) { stage = "Denaturation · 15 s"; temperature = 95f; }
                else if (phase < .60f) { stage = "Anneal + acquire · 30 s"; temperature = 60f; }
                else { stage = "Extension · 30 s"; temperature = 72f; }

                if (normalized < .08f) { stage = "Initial hold · 120 s"; temperature = 95f; CurrentCycle = 0; }
                if (normalized >= .88f)
                {
                    var meltProgress = Mathf.Clamp01((normalized - .88f) / .12f);
                    stage = "Melt curve · 0.5°C steps";
                    temperature = 65f + Mathf.Floor(meltProgress * 60f) * .5f;
                    meltCurve?.SetProgress(meltProgress);
                }

                if (cycleText != null) cycleText.text = $"Cycle {CurrentCycle:00} / 35";
                if (stageText != null) stageText.text = stage;
                if (temperatureText != null) temperatureText.text = $"{temperature:0} °C";
                if (progressFill != null) progressFill.fillAmount = normalized;
                amplificationGraph?.SetProgress(cyclingProgress);
                yield return null;
            }

            IsRunning = false;
            machineAudio?.StopRunLoop();
            if (cycleText != null) cycleText.text = "Run complete · 35 cycles";
            if (stageText != null) stageText.text = "Melt curve analysis complete";
            if (temperatureText != null) temperatureText.text = "Post-run hold · 4 °C";
            if (progressFill != null) progressFill.fillAmount = 1f;
            amplificationGraph?.SetProgress(1f);
            meltCurve?.SetVisible(true);
            yield return new WaitForSeconds(1f);
            lesson.TryComplete(QpcrLessonState.ObserveAmplification);
            touchscreen.ShowResultsPanel();
        }

        private void OnStateChanged(QpcrLessonState state)
        {
            if (state == QpcrLessonState.NotStarted)
                ResetRun();
        }

        private void OnPhysicalStartSelected(SelectEnterEventArgs args)
        {
            var ready = lesson != null && lesson.CurrentState == QpcrLessonState.StartRun &&
                touchscreen != null && touchscreen.IsProtocolValid;
            StartRun();
            (args.interactorObject as XRBaseInputInteractor)?.SendHapticImpulse(ready ? 0.38f : 0.72f,
                ready ? 0.08f : 0.12f);
        }
    }
}
