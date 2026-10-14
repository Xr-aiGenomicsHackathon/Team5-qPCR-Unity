using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace Team5.qPCR
{
    public sealed class LobbyController : MonoBehaviour
    {
        [SerializeField] private TMP_Text nicknameText;
        [SerializeField] private TMP_Text feedbackText;
        [SerializeField] private GameObject entryPanel;
        [FormerlySerializedAs("practicePanel")]
        [SerializeField] private GameObject welcomePanel;
        [SerializeField] private UnityEngine.UI.Button enterButton;
        [SerializeField] private NarrationController narration;
        [SerializeField] private TrainingSceneFade transition;
        private string nickname = string.Empty;
        private bool nicknameConfirmed;
        public bool CanEnterLaboratory => nicknameConfirmed && IsValidNickname(nickname) &&
            transition != null && !transition.IsLoading;
        private static bool IsValidNickname(string value) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 24;
        private void OnEnable() { if (transition != null) transition.LoadFailed += OnLoadFailed; }
        private void OnDisable() { if (transition != null) transition.LoadFailed -= OnLoadFailed; }
        private void Start()
        {
            if (entryPanel != null) entryPanel.SetActive(true);
            if (welcomePanel != null) welcomePanel.SetActive(false);
            if (enterButton != null) enterButton.interactable = false;
            RefreshName();
            narration?.PlayInstruction("Instruction_Lobby_Welcome", "Welcome",
                "Welcome to the T5 XR Genomics Laboratory. Enter a nickname, then select Continue.");
        }
        public void AppendCharacter(string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (nickname.Length + value.Length <= 24) nickname += value;
            RefreshName();
        }
        public void Backspace() { if (nickname.Length > 0) nickname = nickname.Substring(0, nickname.Length - 1); RefreshName(); }
        public void ClearName() { nickname = string.Empty; nicknameConfirmed = false; RefreshName(); }
        private void RefreshName()
        {
            if (nicknameText == null) return;
            nicknameText.richText = false;
            nicknameText.text = nickname.Length == 0 ? "Enter your nickname" : nickname;
        }
        public void ConfirmNickname()
        {
            if (!IsValidNickname(nickname)) { SetFeedback("Please enter a nickname of 1 to 24 characters."); return; }
            nickname = nickname.Trim(); nicknameConfirmed = true;
            if (entryPanel != null) entryPanel.SetActive(false);
            if (welcomePanel != null) welcomePanel.SetActive(true);
            if (enterButton != null) enterButton.interactable = CanEnterLaboratory;
            SetFeedback("Welcome, " + nickname + ". Enter the laboratory when ready.");
            narration?.PlayInstruction("Instruction_Lobby_Ready", "Your laboratory is ready",
                "Your laboratory is ready. Select Enter Laboratory. First, prepare safely. Then you will load and run Q P C R.");
        }
        public void EnterLaboratory()
        {
            if (!nicknameConfirmed || !IsValidNickname(nickname)) { SetFeedback("Enter your nickname and select Continue first."); return; }
            if (transition == null) { OnLoadFailed("The laboratory transition is not connected. Please check the scene setup."); return; }
            if (transition.IsLoading) return;
            if (!TrainingSession.TryBegin(nickname)) { SetFeedback("Please check your nickname."); return; }
            narration?.StopReading();
            if (enterButton != null) enterButton.interactable = false;
            SetFeedback("Entering laboratory...");
            if (!transition.TryLoad("DemoScene") && !transition.IsLoading && enterButton != null)
                enterButton.interactable = CanEnterLaboratory;
        }
        private void OnLoadFailed(string message)
        {
            SetFeedback(message);
            if (enterButton != null) enterButton.interactable = nicknameConfirmed && IsValidNickname(nickname);
        }
        private void SetFeedback(string text)
        {
            if (feedbackText == null) return;
            feedbackText.richText = false; feedbackText.text = text;
        }
    }
}
