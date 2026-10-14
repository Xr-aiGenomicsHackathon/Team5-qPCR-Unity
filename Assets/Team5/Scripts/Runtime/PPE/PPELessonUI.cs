using TMPro;
using UnityEngine;

namespace Team5.qPCR
{
    public sealed class PPELessonUI : MonoBehaviour
    {
        [SerializeField] private PPELessonController lesson;
        [SerializeField] private NarrationController narration;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text purpose;
        [SerializeField] private TMP_Text step;
        [SerializeField] private GameObject whyPanel;
        [SerializeField] private TMP_Text whyText;
        [SerializeField] private Transform pointer;
        [SerializeField] private Transform[] targets;
        [SerializeField] private GameObject handSurfaceDiagram;
        [SerializeField] private TMP_Text detection;
        [SerializeField] private TMP_Text presentationNotice;
        private void OnEnable() { if (lesson != null) lesson.StepChanged += Present; }
        private void OnDisable() { if (lesson != null) lesson.StepChanged -= Present; }
        private void Present(PPELessonStep state)
        {
            if (title != null) title.text = state == PPELessonStep.Complete ? "Ready for qPCR" : PPELessonController.TargetFor(state);
            if (step != null) step.text = $"Lab preparation {lesson.ActiveStepNumber} / {lesson.ActiveStepCount}";
            var explanation = Explanation(state, lesson.Mode);
            if (purpose != null) purpose.text = explanation.Split(new[] { ". " }, System.StringSplitOptions.None)[0] + ".";
            if (presentationNotice != null)
            { presentationNotice.text = PPELessonController.PresentationNotice; presentationNotice.gameObject.SetActive(lesson.Mode == PPELessonMode.Presentation); }
            if (handSurfaceDiagram != null) handSurfaceDiagram.SetActive(state == PPELessonStep.RubHands);
            if (purpose != null) purpose.gameObject.SetActive(state != PPELessonStep.RubHands);
            if (whyPanel != null) whyPanel.SetActive(false);
            if (whyText != null) whyText.text = explanation;
            narration?.PlayInstruction(lesson.InstructionCueKey, "Prepare for lab work", lesson.CurrentInstruction);
        }
        private void LateUpdate()
        {
            if (detection != null && lesson != null)
            {
                var drying = lesson.CurrentStep == PPELessonStep.DryHands;
                detection.gameObject.SetActive(drying || lesson.CurrentStep == PPELessonStep.WetHands || lesson.CurrentStep == PPELessonStep.RinseHands);
                detection.text = "Left: " + ((drying ? lesson.LeftInDryer : lesson.LeftUnderWater) ? "DETECTED" : "move under outlet") +
                    "     Right: " + ((drying ? lesson.RightInDryer : lesson.RightUnderWater) ? "DETECTED" : "move under outlet");
            }
            var index = lesson == null ? -1 : (int)lesson.CurrentStep;
            var target = targets != null && index >= 0 && index < targets.Length ? targets[index] : null;
            if (pointer == null) return;
            pointer.gameObject.SetActive(target != null && lesson.IsLessonActive);
            if (target != null) pointer.position = target.position + Vector3.up * (.13f + .012f * Mathf.Sin(Time.time * 2f));
        }
        public void ToggleWhy()
        {
            if (whyPanel == null) return;
            whyPanel.SetActive(!whyPanel.activeSelf);
            if (!whyPanel.activeSelf && narration != null && narration.CurrentKey == lesson.WhyCueKey) narration.StopReading();
        }
        public void ReadAloud()
        {
            if (whyPanel != null && whyPanel.activeSelf)
                narration?.Play(lesson.WhyCueKey, "Why this step?", Explanation(lesson.CurrentStep, lesson.Mode));
        }
        public static string Explanation(PPELessonStep state, PPELessonMode mode)
        {
            if (mode != PPELessonMode.Presentation) return Explanation(state);
            if (state == PPELessonStep.WetHands || state == PPELessonStep.TurnOnTap)
                return "This one-second checkpoint demonstrates placing both hands beneath running water. It does not clean or disinfect real hands. Real handwashing requires soap, at least twenty seconds of scrubbing, rinsing and drying, following your laboratory's approved procedure.";
            if (state == PPELessonStep.Complete)
                return "You demonstrated the coat, water, drying and glove interactions. Soap and scrubbing were skipped for this presentation; this must not be treated as complete handwashing or a substitute for laboratory safety training.";
            return Explanation(state);
        }
        public static string Explanation(PPELessonStep state) => state switch
        {
            PPELessonStep.WearLabCoat => "A lab coat helps protect clothing and skin from splashes. This chest gesture represents putting it on; it does not reproduce every real dressing movement.",
            PPELessonStep.TurnOnTap or PPELessonStep.WetHands => "Wet bare hands with clean running water before applying soap. Gloves go on only after washing and drying.",
            PPELessonStep.UseSoap => "Soap and rubbing help loosen dirt and microorganisms so they can be rinsed away.",
            PPELessonStep.RubHands => "Scrub for at least 20 seconds. Clean palms, backs of hands, between fingers and under nails. The simulation detects relative hand movement, not complete real-world washing technique.",
            PPELessonStep.RinseHands => "Rinse away soap and loosened contamination with running water.",
            PPELessonStep.TurnOffTap => "Stop the water when finished. In a real lab avoid recontaminating clean hands and follow the local tap and hygiene procedure.",
            PPELessonStep.DryHands => "Dry hands before putting on gloves. This dryer gesture represents drying; follow the drying method specified by your laboratory.",
            PPELessonStep.WearLeftGlove or PPELessonStep.WearRightGlove => "Wear clean, appropriately sized gloves to help prevent contact and contamination. Gloves do not replace hand hygiene; change damaged or contaminated gloves.",
            _ => "Your coat, handwashing and gloves are complete. These are training checks, not a substitute for your laboratory's approved safety procedures."
        };
    }
}
