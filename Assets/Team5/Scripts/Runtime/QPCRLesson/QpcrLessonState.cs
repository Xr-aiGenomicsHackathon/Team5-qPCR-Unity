using System;

namespace Team5.qPCR
{
    public enum QpcrLessonState
    {
        NotStarted,
        InspectPlateId,
        InspectA1Marker,
        InspectOpticalSeal,
        InspectBubbles,
        ReturnPlate,
        PowerOn,
        OpenDrawer,
        LoadPlate,
        CloseDrawer,
        ConfigureProtocol,
        ValidateProtocol,
        StartRun,
        ObserveAmplification,
        InterpretResults,
        Complete
    }

    public enum PlateInspectionKind
    {
        None,
        PlateId,
        A1Marker,
        OpticalSeal,
        Bubbles
    }

    public enum InspectionAttemptOutcome
    {
        Correct,
        LessonNotReady,
        WrongCheckpoint,
        PlateNotHeld,
        SameHand,
        TooFar,
        AlreadyComplete,
        Throttled
    }

    public readonly struct InspectionAttemptResult
    {
        public InspectionAttemptResult(InspectionAttemptOutcome outcome, string message)
        {
            Outcome = outcome;
            Message = message ?? string.Empty;
        }

        public InspectionAttemptOutcome Outcome { get; }
        public string Message { get; }
        public bool IsSuccess => Outcome == InspectionAttemptOutcome.Correct;

        public static InspectionAttemptResult Correct(string message) =>
            new InspectionAttemptResult(InspectionAttemptOutcome.Correct, message);

        public static InspectionAttemptResult Rejected(InspectionAttemptOutcome outcome, string message) =>
            new InspectionAttemptResult(outcome, message);
    }

    [Serializable]
    public readonly struct QpcrStepPresentation
    {
        public QpcrStepPresentation(string title, string target, string instruction, string explanation)
        {
            Title = title;
            Target = target;
            Instruction = instruction;
            Explanation = explanation;
        }

        public string Title { get; }
        public string Target { get; }
        public string Instruction { get; }
        public string Explanation { get; }
    }
}
