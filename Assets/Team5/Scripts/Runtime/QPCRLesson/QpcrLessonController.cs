using System;
using UnityEngine;

namespace Team5.qPCR
{
    public sealed class QpcrLessonController : MonoBehaviour
    {
        [SerializeField] private bool beginOnStart;

        public QpcrLessonState CurrentState { get; private set; } = QpcrLessonState.NotStarted;
        public string CurrentTarget => GetPresentation(CurrentState).Target;
        public bool IsLessonActive { get; private set; }

        public event Action<QpcrLessonState> StateChanged;
        public event Action<string, bool> FeedbackRequested;
        public event Action LessonCompleted;

        private void Start()
        {
            if (beginOnStart)
                BeginLesson();
        }

        public void BeginLesson()
        {
            IsLessonActive = true;
            SetState(QpcrLessonState.InspectPlateId);
            FeedbackRequested?.Invoke(
                "Look at each highlighted plate feature and use the on-screen confirmation button.", false);
        }

        public bool TryComplete(QpcrLessonState attemptedState)
        {
            if (!IsLessonActive)
            {
                ReportRejectedAction("Select Start qPCR Lesson before interacting with the instrument.");
                return false;
            }

            if (attemptedState != CurrentState)
            {
                ReportRejectedAction($"Complete the current task first: {CurrentTarget}.");
                return false;
            }

            if (CurrentState == QpcrLessonState.Complete)
                return true;

            var next = (QpcrLessonState)Mathf.Min((int)QpcrLessonState.Complete, (int)CurrentState + 1);
            SetState(next);
            if (next == QpcrLessonState.Complete)
            {
                IsLessonActive = false;
                LessonCompleted?.Invoke();
            }

            return true;
        }

        public void ReportRejectedAction(string message)
        {
            FeedbackRequested?.Invoke(string.IsNullOrWhiteSpace(message)
                ? $"Complete the current task first: {CurrentTarget}."
                : message, true);
        }

        public void ReportInformation(string message) => FeedbackRequested?.Invoke(message, false);

        public void ResetLesson()
        {
            IsLessonActive = false;
            SetState(QpcrLessonState.NotStarted);
            FeedbackRequested?.Invoke("Press Start qPCR Lesson when you are ready.", false);
        }

        public QpcrStepPresentation GetCurrentPresentation() => GetPresentation(CurrentState);

        public static int GetStepNumber(QpcrLessonState state) => state switch
        {
            QpcrLessonState.NotStarted => 0,
            QpcrLessonState.InspectPlateId => 1,
            QpcrLessonState.InspectA1Marker => 2,
            QpcrLessonState.InspectOpticalSeal => 3,
            QpcrLessonState.InspectBubbles => 4,
            QpcrLessonState.ReturnPlate => 5,
            QpcrLessonState.PowerOn => 6,
            QpcrLessonState.OpenDrawer => 7,
            QpcrLessonState.LoadPlate => 8,
            QpcrLessonState.CloseDrawer => 9,
            QpcrLessonState.ConfigureProtocol => 10,
            QpcrLessonState.ValidateProtocol => 11,
            QpcrLessonState.StartRun => 12,
            QpcrLessonState.ObserveAmplification => 13,
            QpcrLessonState.InterpretResults => 14,
            QpcrLessonState.Complete => 15,
            _ => 0
        };

        public static string GetInputHint(QpcrLessonState state) => state switch
        {
            QpcrLessonState.InspectPlateId or QpcrLessonState.InspectA1Marker or
                QpcrLessonState.InspectOpticalSeal or QpcrLessonState.InspectBubbles =>
                "LOOK at the blue pointer, then aim at CONFIRM and press Trigger (T).",
            QpcrLessonState.ReturnPlate =>
                "Move the plate into the blue home marker and release Grip (G).",
            QpcrLessonState.PowerOn =>
                "Move close to the green power control and press Grip/Select (G).",
            QpcrLessonState.OpenDrawer =>
                "Select the blue OPEN control with Grip/Select (G).",
            QpcrLessonState.LoadPlate =>
                "Hold Grip (G) to carry the plate. Match A1 to the socket marker, then release.",
            QpcrLessonState.CloseDrawer =>
                "Select the amber CLOSE control with Grip/Select (G).",
            QpcrLessonState.ConfigureProtocol =>
                "Aim at a field and press Trigger (T), enter its value, then confirm it.",
            QpcrLessonState.ValidateProtocol =>
                "Aim at VALIDATE PROTOCOL and press Trigger (T).",
            QpcrLessonState.StartRun =>
                "Aim at START RUN and press Trigger (T), or select the physical Start control.",
            QpcrLessonState.ObserveAmplification =>
                "Observe the temperatures, 35 cycles and fluorescence curves; no input is required.",
            QpcrLessonState.InterpretResults =>
                "Compare the positive control and NTC, then select the correct conclusion.",
            QpcrLessonState.Complete =>
                "Lesson complete. Use Reset if you want to practise again.",
            _ => "Aim at START qPCR LESSON and press Trigger (T)."
        };

        private void SetState(QpcrLessonState state)
        {
            CurrentState = state;
            StateChanged?.Invoke(state);
        }

        public static QpcrStepPresentation GetPresentation(QpcrLessonState state)
        {
            return state switch
            {
                QpcrLessonState.InspectPlateId => new QpcrStepPresentation(
                    "Confirm the prepared plate", "Plate ID label",
                    "Read the highlighted Plate ID. If it matches the received experiment record, press Confirm Plate ID.",
                    "The plate ID links this physical plate to the experiment record and prevents sample mix-ups."),
                QpcrLessonState.InspectA1Marker => new QpcrStepPresentation(
                    "Confirm plate orientation", "A1 orientation marker",
                    "Locate the highlighted A1 corner. If the reference corner is clearly identified, press Confirm A1 Orientation.",
                    "A1 is the reference corner. Correct A1 orientation keeps every sample mapped to the correct well."),
                QpcrLessonState.InspectOpticalSeal => new QpcrStepPresentation(
                    "Check the optical seal", "Transparent optical seal",
                    "Visually inspect the highlighted seal. If it is flat, secure and undamaged, press Confirm Seal.",
                    "A secure optical seal limits evaporation and contamination while allowing fluorescence measurement."),
                QpcrLessonState.InspectBubbles => new QpcrStepPresentation(
                    "Check for bubbles", "Reaction wells",
                    "Look across the highlighted wells. If no visible bubbles are present, press Confirm No Bubbles.",
                    "Bubbles can interfere with heat transfer and fluorescence readings."),
                QpcrLessonState.ReturnPlate => new QpcrStepPresentation(
                    "Return the inspected plate", "Prepared plate home marker",
                    "Move the plate back to its blue home marker and release it.",
                    "Return the plate before preparing the instrument so it remains protected and easy to locate."),
                QpcrLessonState.PowerOn => new QpcrStepPresentation(
                    "Power on the qPCR instrument", "Power button",
                    "Press the illuminated power button on the qPCR instrument.",
                    "Powering on starts the instrument electronics and activates the touchscreen."),
                QpcrLessonState.OpenDrawer => new QpcrStepPresentation(
                    "Open the instrument drawer", "Drawer Open button",
                    "Press the Drawer Open button and wait until the drawer stops moving.",
                    "The motorized drawer exposes the thermal block that holds the 96-well plate."),
                QpcrLessonState.LoadPlate => new QpcrStepPresentation(
                    "Load the prepared plate", "A1-aligned plate socket",
                    "Grab the plate, align A1 with the socket marker, place it in the holder, then release it.",
                    "Correct orientation is essential because the instrument reads wells according to their row and column positions."),
                QpcrLessonState.CloseDrawer => new QpcrStepPresentation(
                    "Close the instrument drawer", "Drawer Close button",
                    "Press Drawer Close after the plate is securely seated.",
                    "The drawer must close before the heated lid and thermal cycling can operate safely."),
                QpcrLessonState.ConfigureProtocol => new QpcrStepPresentation(
                    "Configure the qPCR protocol", "Instrument touchscreen",
                    "Select every field and enter its displayed guided value. Confirm each entry before validating the protocol.",
                    "Training protocol: 20 µL; lid 105°C; initial 95°C; 35 cycles; 95/60/72°C; acquire at 60°C; melt 65–95°C by 0.5°C; hold 4°C."),
                QpcrLessonState.ValidateProtocol => new QpcrStepPresentation(
                    "Validate the protocol", "Validate Protocol button",
                    "Review every field, then press Validate Protocol.",
                    "Validation prevents the run from starting with missing or unsafe settings."),
                QpcrLessonState.StartRun => new QpcrStepPresentation(
                    "Start the qPCR run", "Start Run button",
                    "Press Start Run on the instrument after validation passes.",
                    "The instrument now applies the programmed thermal cycles and records fluorescence."),
                QpcrLessonState.ObserveAmplification => new QpcrStepPresentation(
                    "Observe amplification", "Live amplification display",
                    "Watch the cycle number, temperature stage and fluorescence curves until the run finishes.",
                    "True amplification produces a rising fluorescence curve as product accumulates."),
                QpcrLessonState.InterpretResults => new QpcrStepPresentation(
                    "Interpret the controls", "Results analysis",
                    "Compare the curves, then choose whether the controls passed or failed.",
                    "Controls pass when the positive control amplifies and the no-template control (NTC) remains flat."),
                QpcrLessonState.Complete => new QpcrStepPresentation(
                    "qPCR lesson complete", "Completed workflow",
                    "You have inspected, loaded, configured, run and interpreted a qPCR experiment.",
                    "The displayed data are representative teaching results and are not diagnostic patient results."),
                _ => new QpcrStepPresentation(
                    "T5 XR qPCR Training", "Start qPCR Lesson",
                    "Press Start qPCR Lesson to begin with the prepared 96-well plate.",
                    "This guided lesson teaches safe plate handling, instrument setup and result interpretation.")
            };
        }
    }
}
