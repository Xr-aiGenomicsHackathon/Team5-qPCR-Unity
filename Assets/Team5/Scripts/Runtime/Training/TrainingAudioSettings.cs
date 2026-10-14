using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

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
        [Tooltip("Developer-only simulator help overlay. Hidden by default; keyboard simulation still works.")]
        [SerializeField] private bool showSimulatorControlsInEditor;
        private Coroutine simulatorRefresh;
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
            if (simulator != null) simulator.gameObject.SetActive(false);
            narration?.SetVolume(TrainingSession.VoiceVolume);
            narration?.SetMuted(TrainingSession.VoiceMuted);
            SetSimulator(TrainingSession.SimulatorEnabled);
        }
        private void OnDisable()
        {
            volume?.onValueChanged.RemoveListener(SetVolume);
            mute?.onValueChanged.RemoveListener(SetMute);
            simulator?.onValueChanged.RemoveListener(SetSimulator);
            if (simulatorRefresh != null) { StopCoroutine(simulatorRefresh); simulatorRefresh = null; }
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
                if (item is XRInteractionSimulator || item is XRDeviceSimulator)
                    item.gameObject.SetActive(value && Application.isEditor);
            if (simulatorRefresh != null) StopCoroutine(simulatorRefresh);
            if (isActiveAndEnabled) simulatorRefresh = StartCoroutine(RefreshSimulatorPresentation(value));
        }
        private IEnumerator RefreshSimulatorPresentation(bool enabledForTesting)
        {
            // The persistent simulator and the new scene's modality manager initialise in different frames.
            yield return null;
            yield return null;
            foreach (var item in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (item != null && (item.GetType().Name == "XRInteractionSimulatorInputFeedbackUI" ||
                    item.GetType().Name == "XRInteractionSimulatorPlayModeMenu"))
                    item.gameObject.SetActive(enabledForTesting && showSimulatorControlsInEditor && Application.isEditor);
            if (enabledForTesting && Application.isEditor)
            {
                var trackedSimulation = InputSystem.devices.OfType<XRSimulatedController>().Any(device => device.isTracked.isPressed);
                var modality = FindFirstObjectByType<XRInputModalityManager>();
                if (trackedSimulation && modality != null && modality.enabled &&
                    ((modality.leftController != null && !modality.leftController.activeSelf) ||
                     (modality.rightController != null && !modality.rightController.activeSelf)))
                {
                    // Re-evaluate genuine tracking state; never force a controller active on a headset.
                    modality.enabled = false;
                    modality.enabled = true;
                }
            }
            simulatorRefresh = null;
        }
    }
}
