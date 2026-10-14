using System.Collections;
using TMPro;
using UnityEngine;

namespace Team5.qPCR
{
    public sealed class LabSessionCoordinator : MonoBehaviour
    {
        [SerializeField] private PPELessonController ppe;
        [SerializeField] private QpcrLessonController qpcr;
        [SerializeField] private GameObject ppeCard;
        [SerializeField] private GameObject qpcrCard;
        [SerializeField] private GameObject recapPanel;
        [SerializeField] private TMP_Text recapText;
        [SerializeField] private UnityEngine.UI.Button continueButton;
        [SerializeField] private Transform coatWornIndicator;
        [SerializeField] private Transform origin;
        [SerializeField] private Vector3 ppeSpawn = new Vector3(5.2f, 0, -3.65f);
        [SerializeField] private float spawnYaw;
        [SerializeField] private TrainingSceneFade transition;
        private void Awake() { qpcr?.ConfigurePpePrerequisite(ppe); }
        private void OnEnable()
        {
            if (ppe != null) { ppe.Completed += OnPpeComplete; ppe.StepChanged += OnPpeStep; }
            if (qpcr != null) qpcr.LessonCompleted += OnLessonComplete;
        }
        private IEnumerator Start()
        {
            yield return null;
            TrainingSession.PpeComplete = false;
            if (origin != null) { origin.position = ppeSpawn; origin.rotation = Quaternion.Euler(0, spawnYaw, 0); }
            if (recapPanel != null) recapPanel.SetActive(false);
            if (qpcrCard != null) qpcrCard.SetActive(false);
            if (ppeCard != null) ppeCard.SetActive(true);
            if (continueButton != null) continueButton.gameObject.SetActive(false);
            if (coatWornIndicator != null) coatWornIndicator.gameObject.SetActive(false);
            ppe?.BeginLesson();
        }
        private void OnDisable()
        {
            if (ppe != null) { ppe.Completed -= OnPpeComplete; ppe.StepChanged -= OnPpeStep; }
            if (qpcr != null) qpcr.LessonCompleted -= OnLessonComplete;
        }
        private void OnPpeStep(PPELessonStep state)
        {
            if (coatWornIndicator != null) coatWornIndicator.gameObject.SetActive(state != PPELessonStep.WearLabCoat);
        }
        private void OnPpeComplete()
        {
            TrainingSession.PpeComplete = true;
            if (continueButton != null) continueButton.gameObject.SetActive(true);
        }
        public void ContinueToQpcr()
        {
            if (ppe == null || !ppe.IsComplete) return;
            if (ppeCard != null) ppeCard.SetActive(false);
            if (qpcrCard != null) qpcrCard.SetActive(true);
            // Learners navigate to the station; there is no automatic camera movement.
        }
        private void OnLessonComplete()
        {
            if (recapPanel != null) recapPanel.SetActive(true);
            if (recapText != null)
            {
                recapText.richText = false;
                recapText.text = "Well done, " + TrainingSession.Nickname + ".\n\nYou checked the plate ID, A1 corner, seal and wells; loaded and secured the plate; entered and reviewed an assay-specific protocol; watched fluorescence change; and interpreted the controls, cycle threshold and melt curve.\n\nThe curves are simulated teaching data. Always follow an approved experimental protocol in a real lab.";
            }
        }
        public void RestartQpcr()
        {
            if (recapPanel != null) recapPanel.SetActive(false);
            FindFirstObjectByType<QpcrLessonUI>(FindObjectsInactive.Include)?.ResetLesson();
        }
        public void ReturnToLobby()
        {
            FindFirstObjectByType<NarrationController>()?.StopReading();
            transition?.Load("LobbyScene");
        }
    }
}
