using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.qPCR
{
    public sealed class QpcrTouchscreenController : MonoBehaviour
    {
        private static readonly Color Neutral = new Color(0.91f, 0.95f, 0.98f, 1f);
        private static readonly Color Selected = new Color(0.67f, 0.82f, 1f, 1f);
        private static readonly Color Correct = new Color(0.70f, 0.94f, 0.78f, 1f);
        private static readonly Color Error = new Color(1f, 0.70f, 0.72f, 1f);

        [SerializeField] private QpcrLessonController lesson;
        [SerializeField] private QpcrProtocolValidator validator;
        [SerializeField] private QpcrMachineAudio machineAudio;
        [SerializeField] private GameObject welcomePanel;
        [SerializeField] private GameObject protocolPanel;
        [SerializeField] private GameObject runPanel;
        [SerializeField] private GameObject resultsPanel;
        [SerializeField] private QpcrProtocolFieldView[] fields = Array.Empty<QpcrProtocolFieldView>();
        [SerializeField] private TMP_Text activeInputText;
        [SerializeField] private TMP_Text validationText;
        [SerializeField] private Button validateButton;
        [SerializeField] private Button startRunButton;

        private readonly Dictionary<QpcrProtocolParameter, float> values = new Dictionary<QpcrProtocolParameter, float>();
        private int selectedIndex = -1;
        private string inputBuffer = string.Empty;

        public bool IsProtocolValid { get; private set; }
        public IReadOnlyDictionary<QpcrProtocolParameter, float> Values => values;

        public event Action StartRunRequested;

        public void Configure(QpcrLessonController lessonController, QpcrProtocolValidator protocolValidator,
            QpcrMachineAudio audio, GameObject welcome, GameObject protocol, GameObject run, GameObject results,
            QpcrProtocolFieldView[] fieldViews, TMP_Text input, TMP_Text feedback, Button validate, Button startRun)
        {
            lesson = lessonController;
            validator = protocolValidator;
            machineAudio = audio;
            welcomePanel = welcome;
            protocolPanel = protocol;
            runPanel = run;
            resultsPanel = results;
            fields = fieldViews ?? Array.Empty<QpcrProtocolFieldView>();
            activeInputText = input;
            validationText = feedback;
            validateButton = validate;
            startRunButton = startRun;
        }

        private void OnEnable()
        {
            validateButton?.onClick.AddListener(ValidateProtocol);
            startRunButton?.onClick.AddListener(RequestStartRun);
            if (lesson != null)
                lesson.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            validateButton?.onClick.RemoveListener(ValidateProtocol);
            startRunButton?.onClick.RemoveListener(RequestStartRun);
            if (lesson != null)
                lesson.StateChanged -= OnStateChanged;
        }

        public void SelectField(int index)
        {
            if (lesson == null || lesson.CurrentState != QpcrLessonState.ConfigureProtocol)
            {
                lesson?.ReportRejectedAction("Load the plate and close the drawer before configuring the protocol.");
                machineAudio?.PlayError();
                return;
            }
            if (index < 0 || index >= fields.Length || fields[index] == null)
                return;

            selectedIndex = index;
            inputBuffer = values.TryGetValue(fields[index].Parameter, out var existing)
                ? existing.ToString("0.##", CultureInfo.InvariantCulture)
                : string.Empty;
            RefreshFieldColours();
            RefreshInputText();
            var parameter = fields[index].Parameter;
            SetValidation(
                $"Guided value: {QpcrProtocolValidator.ExpectedValue(parameter):0.##} " +
                $"{QpcrProtocolValidator.UnitFor(parameter)}. Enter it, then press Confirm value.", true);
            machineAudio?.PlayButton();
        }

        public void AppendCharacter(string character)
        {
            if (selectedIndex < 0 || selectedIndex >= fields.Length)
            {
                SetValidation("Select a protocol field first.", false);
                machineAudio?.PlayError();
                return;
            }
            if (character == "." && inputBuffer.Contains("."))
                return;
            if (inputBuffer.Length >= 6)
                return;
            inputBuffer += character;
            RefreshInputText();
            machineAudio?.PlayButton();
        }

        public void Backspace()
        {
            if (inputBuffer.Length > 0)
                inputBuffer = inputBuffer.Substring(0, inputBuffer.Length - 1);
            RefreshInputText();
            machineAudio?.PlayButton();
        }

        public void ConfirmCurrentValue()
        {
            if (selectedIndex < 0 || selectedIndex >= fields.Length)
            {
                SetValidation("Select a field before confirming a value.", false);
                return;
            }
            if (!float.TryParse(inputBuffer, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                SetValidation("Enter a valid number, then press Confirm.", false);
                machineAudio?.PlayError();
                return;
            }

            var field = fields[selectedIndex];
            values[field.Parameter] = parsed;
            if (field.Value != null)
                field.Value.text = $"{parsed:0.##} {QpcrProtocolValidator.UnitFor(field.Parameter)}";

            var correct = validator != null && validator.IsCorrect(field.Parameter, parsed);
            field.Background.color = correct ? Correct : Error;
            SetValidation(correct
                ? $"{QpcrProtocolValidator.LabelFor(field.Parameter)} is correct."
                : $"Check {QpcrProtocolValidator.LabelFor(field.Parameter)}. The entered value is not the approved training value.", correct);
            machineAudio?.PlayButton();
            selectedIndex = -1;
            inputBuffer = string.Empty;
            RefreshInputText();
        }

        public void ValidateProtocol()
        {
            if (lesson == null || lesson.CurrentState is not (QpcrLessonState.ConfigureProtocol or QpcrLessonState.ValidateProtocol))
            {
                lesson?.ReportRejectedAction($"Complete the current task first: {lesson?.CurrentTarget}.");
                machineAudio?.PlayError();
                return;
            }

            var invalid = QpcrProtocolParameter.ReactionVolume;
            if (validator == null || !validator.ValidateAll(values, out invalid))
            {
                IsProtocolValid = false;
                var label = QpcrProtocolValidator.LabelFor(invalid);
                SetValidation($"Protocol blocked: correct {label} before starting the run.", false);
                Highlight(invalid, Error);
                lesson.ReportRejectedAction($"Correct the red protocol field: {label}.");
                machineAudio?.PlayError();
                return;
            }

            IsProtocolValid = true;
            SetValidation("Protocol valid. All 12 required values match the approved training protocol.", true);
            machineAudio?.PlayButton();
            if (lesson.CurrentState == QpcrLessonState.ConfigureProtocol)
                lesson.TryComplete(QpcrLessonState.ConfigureProtocol);
            if (lesson.CurrentState == QpcrLessonState.ValidateProtocol)
                lesson.TryComplete(QpcrLessonState.ValidateProtocol);
            if (startRunButton != null)
                startRunButton.interactable = true;
        }

        public void RequestStartRun()
        {
            if (!IsProtocolValid || lesson == null || lesson.CurrentState != QpcrLessonState.StartRun)
            {
                lesson?.ReportRejectedAction(IsProtocolValid
                    ? $"Complete the current task first: {lesson.CurrentTarget}."
                    : "Validate every protocol field before starting the run.");
                machineAudio?.PlayError();
                return;
            }
            StartRunRequested?.Invoke();
        }

        public void ResetTouchscreen()
        {
            values.Clear();
            selectedIndex = -1;
            inputBuffer = string.Empty;
            IsProtocolValid = false;
            foreach (var field in fields)
            {
                if (field == null) continue;
                if (field.Label != null) field.Label.text = QpcrProtocolValidator.LabelFor(field.Parameter);
                if (field.Value != null) field.Value.text = "—";
                if (field.Background != null) field.Background.color = Neutral;
            }
            if (startRunButton != null) startRunButton.interactable = false;
            SetValidation("Select a field and use the number pad.", true);
            RefreshInputText();
            ShowPanel(welcomePanel);
        }

        public void ShowRunPanel() => ShowPanel(runPanel);
        public void ShowResultsPanel() => ShowPanel(resultsPanel);

        private void OnStateChanged(QpcrLessonState state)
        {
            switch (state)
            {
                case QpcrLessonState.NotStarted:
                    ResetTouchscreen();
                    break;
                case QpcrLessonState.ConfigureProtocol:
                case QpcrLessonState.ValidateProtocol:
                case QpcrLessonState.StartRun:
                    ShowPanel(protocolPanel);
                    break;
                case QpcrLessonState.ObserveAmplification:
                    ShowPanel(runPanel);
                    break;
                case QpcrLessonState.InterpretResults:
                case QpcrLessonState.Complete:
                    ShowPanel(resultsPanel);
                    break;
            }
        }

        private void ShowPanel(GameObject active)
        {
            if (welcomePanel != null) welcomePanel.SetActive(active == welcomePanel);
            if (protocolPanel != null) protocolPanel.SetActive(active == protocolPanel);
            if (runPanel != null) runPanel.SetActive(active == runPanel);
            if (resultsPanel != null) resultsPanel.SetActive(active == resultsPanel);
        }

        private void RefreshFieldColours()
        {
            for (var index = 0; index < fields.Length; index++)
            {
                var field = fields[index];
                if (field?.Background == null) continue;
                if (index == selectedIndex) field.Background.color = Selected;
                else if (values.TryGetValue(field.Parameter, out var value))
                    field.Background.color = validator != null && validator.IsCorrect(field.Parameter, value) ? Correct : Error;
                else field.Background.color = Neutral;
            }
        }

        private void Highlight(QpcrProtocolParameter parameter, Color color)
        {
            foreach (var field in fields)
                if (field != null && field.Parameter == parameter && field.Background != null)
                    field.Background.color = color;
        }

        private void RefreshInputText()
        {
            if (activeInputText == null) return;
            activeInputText.text = selectedIndex < 0
                ? "Select a field"
                : string.IsNullOrEmpty(inputBuffer) ? "Enter value" : inputBuffer;
        }

        private void SetValidation(string message, bool positive)
        {
            if (validationText == null) return;
            validationText.text = message;
            validationText.color = positive ? new Color(0.16f, 0.72f, 0.38f, 1f) : new Color(0.94f, 0.18f, 0.22f, 1f);
        }
    }
}
