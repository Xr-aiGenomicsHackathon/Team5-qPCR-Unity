using UnityEngine;
using UnityEngine.UI;

namespace Team5.qPCR
{
    public sealed class TrainingAudioSettings : MonoBehaviour
    {
        [SerializeField] private NarrationController narration;
        [SerializeField] private Slider volume;
        [SerializeField] private Toggle mute;
        [SerializeField] private Toggle simulator;
        [SerializeField] private GameObject[] simulatorObjects;
        private void OnEnable()
        {
            volume?.onValueChanged.AddListener(SetVolume);
            mute?.onValueChanged.AddListener(SetMute);
            simulator?.onValueChanged.AddListener(SetSimulator);
        }
        private void Start()
        {
            volume?.SetValueWithoutNotify(TrainingSession.VoiceVolume);
            mute?.SetIsOnWithoutNotify(TrainingSession.VoiceMuted);
            simulator?.SetIsOnWithoutNotify(TrainingSession.SimulatorEnabled);
            narration?.SetVolume(TrainingSession.VoiceVolume);
            narration?.SetMuted(TrainingSession.VoiceMuted);
            SetSimulator(TrainingSession.SimulatorEnabled);
        }
        private void OnDisable()
        {
            volume?.onValueChanged.RemoveListener(SetVolume);
            mute?.onValueChanged.RemoveListener(SetMute);
            simulator?.onValueChanged.RemoveListener(SetSimulator);
        }
        private void SetVolume(float value) => narration?.SetVolume(value);
        private void SetMute(bool value) => narration?.SetMuted(value);
        public void SetSimulator(bool value)
        {
            TrainingSession.SimulatorEnabled = value;
            if (simulatorObjects != null) foreach (var item in simulatorObjects) if (item != null) item.SetActive(value);
        }
    }
}
