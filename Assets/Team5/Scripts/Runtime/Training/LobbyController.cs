using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Team5.qPCR
{
    public sealed class LobbyController : MonoBehaviour
    {
        [SerializeField] private TMP_Text nicknameText;
        [SerializeField] private TMP_Text feedbackText;
        [SerializeField] private GameObject entryPanel;
        [SerializeField] private GameObject practicePanel;
        [SerializeField] private XRGrabInteractable practiceObject;
        [SerializeField] private UnityEngine.UI.Button enterButton;
        [SerializeField] private NarrationController narration;
        [SerializeField] private TrainingSceneFade transition;
        private string nickname = string.Empty;
        private bool selectedButton;
        private bool grabbed;
        public bool PracticeComplete => selectedButton && grabbed && released;
        private bool released;
        private Vector3 practicePosition;
        private void Awake() { if (practiceObject != null) practicePosition = practiceObject.transform.position; }
        private void OnEnable()
        {
            if (practiceObject == null) return;
            practiceObject.selectEntered.AddListener(OnGrab);
            practiceObject.selectExited.AddListener(OnRelease);
        }
        private void OnDisable()
        {
            if (practiceObject == null) return;
            practiceObject.selectEntered.RemoveListener(OnGrab);
            practiceObject.selectExited.RemoveListener(OnRelease);
        }
        private void Start()
        {
            if (entryPanel != null) entryPanel.SetActive(true);
            if (practicePanel != null) practicePanel.SetActive(false);
            if (practiceObject != null) practiceObject.gameObject.SetActive(false);
            RefreshName();
            narration?.PlayInstruction("Instruction_Lobby_Welcome", "Welcome",
                "Welcome to the T5 XR Genomics Laboratory. Enter a nickname, then select Continue.");
        }
        public void AppendCharacter(string value)
        {
            if (nickname.Length + value.Length <= 24) nickname += value;
            RefreshName();
        }
        public void Backspace() { if (nickname.Length > 0) nickname = nickname.Substring(0, nickname.Length - 1); RefreshName(); }
        public void ClearName() { nickname = string.Empty; RefreshName(); }
        private void RefreshName()
        {
            if (nicknameText != null) { nicknameText.richText = false; nicknameText.text = nickname.Length == 0 ? "Enter your nickname" : nickname; }
        }
        public void ConfirmNickname()
        {
            if (string.IsNullOrWhiteSpace(nickname)) { if (feedbackText != null) feedbackText.text = "Please enter a nickname to continue."; return; }
            nickname = nickname.Trim();
            if (entryPanel != null) entryPanel.SetActive(false);
            if (practicePanel != null) practicePanel.SetActive(true);
            if (feedbackText != null) feedbackText.text = "Welcome, " + nickname + ". First, press Practice Select.";
            if (enterButton != null) enterButton.interactable = false;
            narration?.PlayInstruction("Instruction_Lobby_Practice", "Try the controls",
                "Press Practice Select. Then grip the teal practice object, lift it, and release your grip.");
        }
        public void PracticeSelect()
        {
            selectedButton = true;
            if (practiceObject != null) practiceObject.gameObject.SetActive(true);
            if (feedbackText != null) feedbackText.text = "Selection works! Grip the teal object, lift it, then release it.";
        }
        private void OnGrab(SelectEnterEventArgs args) { if (selectedButton) grabbed = true; }
        private void OnRelease(SelectExitEventArgs args)
        {
            if (args.isCanceled || !grabbed || !selectedButton) return;
            released = true;
            if (enterButton != null) enterButton.interactable = true;
            if (feedbackText != null) feedbackText.text = "Great, " + nickname + ". You can select, grab and release. Enter the laboratory when ready.";
        }
        private void Update()
        {
            if (practiceObject == null || practiceObject.isSelected || practiceObject.transform.position.y > -.3f) return;
            var body = practiceObject.GetComponent<Rigidbody>();
            body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
            practiceObject.transform.position = practicePosition; body.position = practicePosition;
        }
        public void EnterLaboratory()
        {
            if (!PracticeComplete || transition == null || transition.IsLoading || !TrainingSession.TryBegin(nickname)) return;
            narration?.StopReading();
            transition.Load("DemoScene");
        }
    }
}
