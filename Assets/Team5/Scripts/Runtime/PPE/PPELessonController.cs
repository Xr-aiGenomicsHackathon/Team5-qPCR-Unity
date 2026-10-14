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
        [SerializeField, Min(0.1f)] private float rubHandsSeconds = 4f;
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

        public event Action<PPELessonStep> StepChanged;
        public event Action ResetRequested;

        public PPELessonStep CurrentStep { get; private set; } = PPELessonStep.WearLabCoat;
        public bool IsWaterRunning { get; private set; }
        public bool IsComplete => CurrentStep == PPELessonStep.Complete;
        public string CurrentTargetLabel => TargetFor(CurrentStep);

        private void Start()
        {
            ResetLesson();
        }

        private void Update()
        {
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
                        wetHandsSeconds, PPELessonStep.UseSoap, "Wetting hands");
                    break;
                case PPELessonStep.RubHands:
                    UpdateTimedStep(AreHandsCloseTogether(), rubHandsSeconds,
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
            if (probe.Side == PPEHandSide.Left) leftHand = probe.transform;
            else rightHand = probe.transform;
        }

        public void UnregisterHand(PPEHandProbe probe)
        {
            if (probe == null) return;
            if (probe.Side == PPEHandSide.Left && leftHand == probe.transform) leftHand = null;
            if (probe.Side == PPEHandSide.Right && rightHand == probe.transform) rightHand = null;
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
                onPpeCompleted?.Invoke();
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
            feedbackRemaining = 0f;
            if (feedbackText != null) feedbackText.text = string.Empty;
            if (narrationSource != null)
                narrationSource.Stop();
            ResetRequested?.Invoke();
            SetStep(PPELessonStep.WearLabCoat, true);
        }

        private bool RequireStep(PPELessonStep required)
        {
            if (CurrentStep == required) return true;
            ReportRejectedAction($"Complete the current task first: {CurrentTargetLabel.ToLowerInvariant()}.");
            return false;
        }

        private void UpdateTimedStep(bool condition, float requiredSeconds,
            PPELessonStep nextStep, string progressLabel)
        {
            if (!condition)
            {
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

        private void SetStep(PPELessonStep step, bool force = false)
        {
            if (!force && CurrentStep == step) return;
            CurrentStep = step;
            stepProgress = 0f;

            if (targetText != null) targetText.text = "Current target: " + TargetFor(step);
            if (instructionText != null) instructionText.text = InstructionFor(step);
            if (progressText != null) progressText.text = string.Empty;
            if (feedbackText != null) feedbackText.text = string.Empty;
            feedbackRemaining = 0f;

            PlayNarration(step);
            StepChanged?.Invoke(step);
            onStepChanged?.Invoke(step);
        }

        private void PlayNarration(PPELessonStep step)
        {
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

        private static string TargetFor(PPELessonStep step)
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

        private static string InstructionFor(PPELessonStep step)
        {
            return step switch
            {
                PPELessonStep.WearLabCoat => "Pick up the lab coat and bring it to the wear zone in front of your chest.",
                PPELessonStep.TurnOnTap => "Press the tap handle to turn on the water.",
                PPELessonStep.WetHands => "Place both hands under the running water.",
                PPELessonStep.UseSoap => "Press the soap dispenser once.",
                PPELessonStep.RubHands => "Bring both hands together and rub them.",
                PPELessonStep.RinseHands => "Place both hands under the water again to rinse them.",
                PPELessonStep.TurnOffTap => "Press the tap handle to turn off the water.",
                PPELessonStep.DryHands => "Place both hands beneath the hand dryer.",
                PPELessonStep.WearLeftGlove => "Pick up the left glove with your left hand.",
                PPELessonStep.WearRightGlove => "Pick up the right glove with your right hand.",
                _ => "PPE complete. You may continue to the qPCR plate inspection."
            };
        }
    }
}
