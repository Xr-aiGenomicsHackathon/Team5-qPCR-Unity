using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Team5.qPCR
{
    public enum PPELessonStep
    {
        WearLabCoat,
        TurnOnTap,
        WetHands,
        UseSoap,
        RubHands,
        RinseHands,
        TurnOffTap,
        DryHands,
        WearLeftGlove,
        WearRightGlove,
        Complete
    }

    public enum PPEHandSide
    {
        Left,
        Right
    }

    public enum PPELessonMode { FullTraining = 0, Presentation = 1 }

    [Serializable]
    public sealed class PPEAudioPrompt
    {
        public PPELessonStep step;
        public AudioClip clip;
    }

    [Serializable]
    public sealed class PPELessonStepEvent : UnityEvent<PPELessonStep>
    {
    }

    /// <summary>
    /// Owns the ordered PPE lesson. Physical objects report successful actions
    /// to this component; this component decides whether the action is allowed.
    /// </summary>
    public sealed class PPELessonController : MonoBehaviour
    {
        [SerializeField] private bool beginOnStart = true;
        [SerializeField] private PPELessonMode mode = PPELessonMode.FullTraining;
        public const string PresentationNotice = "Abbreviated PPE demonstration—not complete handwashing.";
        private static readonly PPELessonStep[] PresentationSteps = {
            PPELessonStep.WearLabCoat, PPELessonStep.TurnOnTap, PPELessonStep.WetHands,
            PPELessonStep.TurnOffTap, PPELessonStep.DryHands, PPELessonStep.WearLeftGlove,
            PPELessonStep.WearRightGlove, PPELessonStep.Complete };
        [Header("Lesson UI (optional)")]
        [SerializeField] private TMP_Text instructionText;
        [SerializeField] private TMP_Text targetText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text feedbackText;
        [SerializeField, Min(1f)] private float feedbackSeconds = 4f;

        [Header("Narration (optional)")]
        [SerializeField] private AudioSource narrationSource;
        [SerializeField] private AudioSource errorSource;
        [SerializeField] private AudioClip errorClip;
        [SerializeField] private PPEAudioPrompt[] narrationClips = Array.Empty<PPEAudioPrompt>();

        [Header("Required hold times")]
        [SerializeField, Min(0.1f)] private float wetHandsSeconds = 2f;
        [SerializeField, Min(0.1f)] private float rubHandsSeconds = 20f;
        [SerializeField, Min(0.1f)] private float rinseHandsSeconds = 3f;
        [SerializeField, Min(0.1f)] private float dryHandsSeconds = 2f;
        [SerializeField, Min(0.05f)] private float handRubDistance = 0.28f;

        [Header("Events")]
        [SerializeField] private PPELessonStepEvent onStepChanged = new PPELessonStepEvent();
        [SerializeField] private UnityEvent onPpeCompleted = new UnityEvent();

        private Transform leftHand;
        private Transform rightHand;
        private bool leftUnderWater;
        private bool rightUnderWater;
        private bool leftInDryer;
        private bool rightInDryer;
        private float stepProgress;
        private float feedbackRemaining;
        private Vector3 previousHandDifference;
        private bool hasPreviousHandPose;

        public event Action<PPELessonStep> StepChanged;
        public event Action ResetRequested;
        public event Action Completed;
        public bool IsLessonActive { get; private set; }
        public float StepProgress => stepProgress;
        public PPELessonMode Mode => mode;
        public bool LeftUnderWater => leftUnderWater;
        public bool RightUnderWater => rightUnderWater;
        public bool LeftInDryer => leftInDryer;
        public bool RightInDryer => rightInDryer;
        public int ActiveStepCount => mode == PPELessonMode.Presentation ? PresentationSteps.Length : 11;
        public int ActiveStepNumber => mode == PPELessonMode.Presentation
            ? Array.IndexOf(PresentationSteps, CurrentStep) + 1 : (int)CurrentStep + 1;
        public string CurrentInstruction => InstructionFor(CurrentStep, mode);
        public string InstructionCueKey => "Instruction_Ppe_" + (mode == PPELessonMode.Presentation ? "Presentation_" : "") + CurrentStep;
        public string WhyCueKey => "Why_Ppe_" + (mode == PPELessonMode.Presentation ? "Presentation_" : "") + CurrentStep;
        public void ConfigureMode(PPELessonMode value) { mode = value; }
        public static bool IsScrubMotion(Vector3 previousDifference, Vector3 currentDifference, float seconds)
            => seconds > 0f && Vector3.Distance(previousDifference, currentDifference) / seconds >= .025f;
        public void BeginLesson() { IsLessonActive = true; ResetLesson(); }
        public void StopLesson() { IsLessonActive = false; IsWaterRunning = false; if (narrationSource != null) narrationSource.Stop(); }
        private void OnDisable() { StopLesson(); ResetRequested?.Invoke(); }

        public PPELessonStep CurrentStep { get; private set; } = PPELessonStep.WearLabCoat;
        public bool IsWaterRunning { get; private set; }
        public bool IsComplete => CurrentStep == PPELessonStep.Complete;
        public string CurrentTargetLabel => TargetFor(CurrentStep);

        private void Start()
        {
            if (beginOnStart) BeginLesson();
        }

        private void Update()
        {
            if (!IsLessonActive) return;
            var delta = Time.deltaTime;
            if (feedbackRemaining > 0f)
            {
                feedbackRemaining -= delta;
                if (feedbackRemaining <= 0f && feedbackText != null)
                    feedbackText.text = string.Empty;
            }

            switch (CurrentStep)
            {
                case PPELessonStep.WetHands:
                    UpdateTimedStep(IsWaterRunning && leftUnderWater && rightUnderWater,
                        wetHandsSeconds, mode == PPELessonMode.Presentation ? PPELessonStep.TurnOffTap : PPELessonStep.UseSoap,
                        mode == PPELessonMode.Presentation ? "Water checkpoint" : "Wetting hands", true);
                    break;
                case PPELessonStep.RubHands:
                    UpdateTimedStep(HandsAreRubbing(delta), rubHandsSeconds,
                        PPELessonStep.RinseHands, "Rubbing hands");
                    break;
                case PPELessonStep.RinseHands:
                    UpdateTimedStep(IsWaterRunning && leftUnderWater && rightUnderWater,
                        rinseHandsSeconds, PPELessonStep.TurnOffTap, "Rinsing hands");
                    break;
                case PPELessonStep.DryHands:
                    UpdateTimedStep(!IsWaterRunning && leftInDryer && rightInDryer,
                        dryHandsSeconds, PPELessonStep.WearLeftGlove, "Drying hands");
                    break;
            }
        }

        public void RegisterHand(PPEHandProbe probe)
        {
            if (probe == null) return;
            if (probe.Side == PPEHandSide.Left) leftHand = probe.HandPose;
            else rightHand = probe.HandPose;
        }

        public void UnregisterHand(PPEHandProbe probe)
        {
            if (probe == null) return;
            if (probe.Side == PPEHandSide.Left && leftHand == probe.HandPose)
            { leftHand = null; leftUnderWater = false; leftInDryer = false; }
            if (probe.Side == PPEHandSide.Right && rightHand == probe.HandPose)
            { rightHand = null; rightUnderWater = false; rightInDryer = false; }
        }

        public bool TryWearCoat()
        {
            if (!RequireStep(PPELessonStep.WearLabCoat)) return false;
            SetStep(PPELessonStep.TurnOnTap);
            return true;
        }

        public bool TrySetTap(bool turnOn)
        {
            if (turnOn)
            {
                if (!RequireStep(PPELessonStep.TurnOnTap)) return false;
                IsWaterRunning = true;
                SetStep(PPELessonStep.WetHands);
                return true;
            }

            if (!RequireStep(PPELessonStep.TurnOffTap)) return false;
            IsWaterRunning = false;
            SetStep(PPELessonStep.DryHands);
            return true;
        }

        public bool TryUseSoap()
        {
            if (!RequireStep(PPELessonStep.UseSoap)) return false;
            SetStep(PPELessonStep.RubHands);
            return true;
        }

        public bool TryWearGlove(PPEHandSide side)
        {
            var required = side == PPEHandSide.Left
                ? PPELessonStep.WearLeftGlove
                : PPELessonStep.WearRightGlove;

            if (!RequireStep(required)) return false;

            if (side == PPEHandSide.Left)
            {
                SetStep(PPELessonStep.WearRightGlove);
            }
            else
            {
                SetStep(PPELessonStep.Complete);
                IsLessonActive = false;
                onPpeCompleted?.Invoke();
                Completed?.Invoke();
            }
            return true;
        }

        public void SetHandUnderWater(PPEHandSide side, bool value)
        {
            if (side == PPEHandSide.Left) leftUnderWater = value;
            else rightUnderWater = value;
        }

        public void SetHandInDryer(PPEHandSide side, bool value)
        {
            if (side == PPEHandSide.Left) leftInDryer = value;
            else rightInDryer = value;
        }

        public void ReportRejectedAction(string message)
        {
            if (feedbackText != null) feedbackText.text = message;
            feedbackRemaining = feedbackSeconds;
            if (errorSource != null && errorClip != null)
                errorSource.PlayOneShot(errorClip);
        }

        public void ResetLesson()
        {
            IsWaterRunning = false;
            leftUnderWater = false;
            rightUnderWater = false;
            leftInDryer = false;
            rightInDryer = false;
            stepProgress = 0f;
            hasPreviousHandPose = false;
            feedbackRemaining = 0f;
            if (feedbackText != null) feedbackText.text = string.Empty;
            if (narrationSource != null)
                narrationSource.Stop();
            ResetRequested?.Invoke();
            SetStep(PPELessonStep.WearLabCoat, true);
        }

        private bool RequireStep(PPELessonStep required)
        {
            if (IsLessonActive && CurrentStep == required) return true;
            ReportRejectedAction($"Complete the current task first: {CurrentTargetLabel.ToLowerInvariant()}.");
            return false;
        }

        private void UpdateTimedStep(bool condition, float requiredSeconds,
            PPELessonStep nextStep, string progressLabel, bool continuous = false)
        {
            if (!condition)
            {
                if (continuous) stepProgress = 0f;
                if (progressText != null)
                    progressText.text = $"{progressLabel}: {stepProgress:0.0} / {requiredSeconds:0.0} seconds";
                return;
            }

            stepProgress += Time.deltaTime;
            if (progressText != null)
                progressText.text = $"{progressLabel}: {Mathf.Min(stepProgress, requiredSeconds):0.0} / {requiredSeconds:0.0} seconds";
            if (stepProgress >= requiredSeconds)
                SetStep(nextStep);
        }

        private bool AreHandsCloseTogether()
        {
            return leftHand != null && rightHand != null &&
                   Vector3.Distance(leftHand.position, rightHand.position) <= handRubDistance;
        }
        private bool HandsAreRubbing(float delta)
        {
            if (!AreHandsCloseTogether()) { hasPreviousHandPose = false; return false; }
            var difference = leftHand.position - rightHand.position;
            var moving = hasPreviousHandPose && IsScrubMotion(previousHandDifference, difference, delta);
            previousHandDifference = difference;
            hasPreviousHandPose = true;
            return moving;
        }

        private void SetStep(PPELessonStep step, bool force = false)
        {
            if (!force && CurrentStep == step) return;
            CurrentStep = step;
            stepProgress = 0f;
            hasPreviousHandPose = false;

            if (targetText != null) targetText.text = "Current target: " + TargetFor(step);
            if (instructionText != null) instructionText.text = InstructionFor(step, mode);
            if (progressText != null) progressText.text = string.Empty;
            if (feedbackText != null) feedbackText.text = string.Empty;
            feedbackRemaining = 0f;

            PlayNarration(step);
            StepChanged?.Invoke(step);
            onStepChanged?.Invoke(step);
        }

        private void PlayNarration(PPELessonStep step)
        {
            // Full-training recordings must never describe skipped presentation tasks.
            if (mode == PPELessonMode.Presentation) return;
            if (narrationSource == null || narrationClips == null) return;
            narrationSource.Stop();
            foreach (var prompt in narrationClips)
            {
                if (prompt == null || prompt.step != step || prompt.clip == null) continue;
                narrationSource.clip = prompt.clip;
                narrationSource.Play();
                break;
            }
        }

        public static string TargetFor(PPELessonStep step)
        {
            return step switch
            {
                PPELessonStep.WearLabCoat => "Lab coat",
                PPELessonStep.TurnOnTap => "Tap handle",
                PPELessonStep.WetHands => "Running water",
                PPELessonStep.UseSoap => "Soap dispenser",
                PPELessonStep.RubHands => "Both hands",
                PPELessonStep.RinseHands => "Running water",
                PPELessonStep.TurnOffTap => "Tap handle",
                PPELessonStep.DryHands => "Hand dryer",
                PPELessonStep.WearLeftGlove => "Left glove",
                PPELessonStep.WearRightGlove => "Right glove",
                _ => "PPE complete"
            };
        }

        public static string InstructionFor(PPELessonStep step)
        {
            return step switch
            {
                PPELessonStep.WearLabCoat => "Pick up the lab coat and bring it to the wear zone in front of your chest.",
                PPELessonStep.TurnOnTap => "Press the tap handle to turn on the water.",
                PPELessonStep.WetHands => "Place both hands under the running water.",
                PPELessonStep.UseSoap => "Press the soap dispenser once.",
                PPELessonStep.RubHands => "Rub your hands together for 20 seconds. Clean palms, backs, between fingers and under nails. Keep moving both hands close together.",
                PPELessonStep.RinseHands => "Place both hands under the water again to rinse them.",
                PPELessonStep.TurnOffTap => "Press the tap handle to turn off the water.",
                PPELessonStep.DryHands => "Place both hands beneath the hand dryer.",
                PPELessonStep.WearLeftGlove => "Pick up the left glove with your left hand.",
                PPELessonStep.WearRightGlove => "Pick up the right glove with your right hand.",
                _ => "PPE complete. You may continue to the qPCR plate inspection."
            };
        }

        public static string InstructionFor(PPELessonStep step, PPELessonMode mode)
        {
            if (mode != PPELessonMode.Presentation) return InstructionFor(step);
            return step switch {
                PPELessonStep.WetHands => "Keep both hands beneath the water for one continuous second. If either hand leaves, the timer restarts.",
                PPELessonStep.DryHands => "Keep both hands beneath the dryer for two seconds before putting on gloves.",
                PPELessonStep.Complete => "Preparation demonstration complete. Continue to qPCR. This short demonstration is not complete handwashing.",
                _ => InstructionFor(step)
            };
        }
    }
}
