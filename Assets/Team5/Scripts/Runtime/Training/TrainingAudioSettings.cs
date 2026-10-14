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
        [Tooltip("Turn off before Play when testing with a real headset in the Editor. Builds always disable simulation.")]
        [SerializeField] private bool useSimulatorInEditor = true;
        private void OnEnable()
        {
            volume?.onValueChanged.AddListener(SetVolume);
            mute?.onValueChanged.AddListener(SetMute);
            simulator?.onValueChanged.AddListener(SetSimulator);
        }
        private void Start()
        {
            if (!Application.isEditor || !useSimulatorInEditor) TrainingSession.SimulatorEnabled = false;
            volume?.SetValueWithoutNotify(TrainingSession.VoiceVolume);
            mute?.SetIsOnWithoutNotify(TrainingSession.VoiceMuted);
            simulator?.SetIsOnWithoutNotify(TrainingSession.SimulatorEnabled);
            if (simulator != null) simulator.gameObject.SetActive(Application.isEditor);
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
            value = value && Application.isEditor;
            TrainingSession.SimulatorEnabled = value;
            if (simulatorObjects != null) foreach (var item in simulatorObjects) if (item != null) item.SetActive(value);
            // The XRI loader may place its simulator in DontDestroyOnLoad rather than this scene.
            foreach (var item in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (item != null && (item.GetType().Name == "XRInteractionSimulator" || item.GetType().Name == "XRDeviceSimulator"))
                    item.gameObject.SetActive(value && Application.isEditor);
        }
    }
}
