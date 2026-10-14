using UnityEngine;

namespace Team5.qPCR
{
    /// <summary>Quiet non-verbal confirmation; independent of narration and label hovering.</summary>
    public sealed class LessonStepFeedback : MonoBehaviour
    {
        [SerializeField] private PPELessonController ppe;
        [SerializeField] private QpcrLessonController qpcr;
        [SerializeField] private AudioSource confirmation;
        private void OnEnable()
        {
            if (ppe != null) ppe.StepChanged += PpeStep;
            if (qpcr != null) qpcr.StateChanged += QpcrStep;
        }
        private void OnDisable()
        {
            if (ppe != null) ppe.StepChanged -= PpeStep;
            if (qpcr != null) qpcr.StateChanged -= QpcrStep;
            if (confirmation != null) confirmation.Stop();
        }
        private void PpeStep(PPELessonStep step) { if (step != PPELessonStep.WearLabCoat) Confirm(); }
        private void QpcrStep(QpcrLessonState step)
        { if (step != QpcrLessonState.NotStarted && step != QpcrLessonState.InspectPlateId) Confirm(); }
        private void Confirm()
        { if (confirmation != null && confirmation.clip != null) confirmation.PlayOneShot(confirmation.clip); }
    }
}
