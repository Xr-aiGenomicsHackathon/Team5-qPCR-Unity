using TMPro;
using UnityEngine;

namespace Team5.qPCR
{
    /// <summary>Read-only feedback beside the active outlet; never advances the lesson.</summary>
    public sealed class PPEOutletStatus : MonoBehaviour
    {
        [SerializeField] private PPELessonController lesson;
        [SerializeField] private Transform waterZone;
        [SerializeField] private Transform dryerZone;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text hands;
        [SerializeField] private TMP_Text progress;
        [SerializeField] private TMP_Text sourceProgress;
        public bool IsVisible => panel != null && panel.activeSelf;
        private void LateUpdate()
        {
            if (panel == null) return;
            var show = lesson != null && lesson.IsLessonActive &&
                (lesson.CurrentStep == PPELessonStep.WetHands || lesson.CurrentStep == PPELessonStep.RinseHands || lesson.CurrentStep == PPELessonStep.DryHands);
            panel.SetActive(show);
            if (!show) return;
            var drying = lesson.CurrentStep == PPELessonStep.DryHands;
            var anchor = drying ? dryerZone : waterZone;
            if (anchor != null) panel.transform.position = anchor.position + Vector3.up * .41f;
            if (Camera.main != null)
            {
                var direction = panel.transform.position - Camera.main.transform.position;
                if (direction.sqrMagnitude > .001f) panel.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
            if (hands != null) hands.text = "Left: " + ((drying ? lesson.LeftInDryer : lesson.LeftUnderWater) ? "detected" : "waiting") +
                "    Right: " + ((drying ? lesson.RightInDryer : lesson.RightUnderWater) ? "detected" : "waiting");
            if (progress != null) progress.text = sourceProgress != null ? sourceProgress.text : "Keep both hands beneath the outlet.";
        }
        private void OnDisable() { if (panel != null) panel.SetActive(false); }
    }
}
