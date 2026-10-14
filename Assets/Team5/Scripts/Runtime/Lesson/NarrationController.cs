using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.qPCR
{
    [Serializable]
    public sealed class NarrationCue
    {
        public string Key;
        [TextArea(2, 5)] public string Caption;
        public AudioClip Clip;
    }

    public sealed class NarrationController : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private TMP_Text[] captionTexts = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] captionTitles = Array.Empty<TMP_Text>();
        [SerializeField] private Slider[] volumeSliders = Array.Empty<Slider>();
        [SerializeField] private Toggle[] muteToggles = Array.Empty<Toggle>();
        [SerializeField] private Button[] replayButtons = Array.Empty<Button>();
        [SerializeField] private NarrationCue[] cues = Array.Empty<NarrationCue>();

        private NarrationCue currentCue;
        private NarrationCue instructionCue;
        private string instructionTitle;
        private string instructionCaption;

        public bool IsMuted => audioSource != null && audioSource.mute;
        public bool IsSpeaking => audioSource != null && audioSource.isPlaying && !audioSource.mute;
        public string CurrentKey => currentCue?.Key;
        public string CurrentCaption => currentCue == null ? string.Empty : currentCue.Caption;

        public void PlayInstruction(string key, string title, string caption)
        {
            instructionCue = new NarrationCue { Key = key, Caption = caption,
                Clip = cues.FirstOrDefault(c => c != null && c.Key == key)?.Clip };
            instructionTitle = title;
            instructionCaption = caption;
            Play(key, title, caption);
        }

        public void ReplayInstruction()
        {
            if (instructionCue != null) Play(instructionCue.Key, instructionTitle, instructionCaption);
        }

        public void StopReading() => audioSource?.Stop();

        public void Configure(AudioSource source, TMP_Text[] titles, TMP_Text[] captions, Slider[] volumes,
            Toggle[] mutes, Button[] replay, NarrationCue[] availableCues)
        {
            audioSource = source;
            captionTitles = titles ?? Array.Empty<TMP_Text>();
            captionTexts = captions ?? Array.Empty<TMP_Text>();
            volumeSliders = volumes ?? Array.Empty<Slider>();
            muteToggles = mutes ?? Array.Empty<Toggle>();
            replayButtons = replay ?? Array.Empty<Button>();
            cues = availableCues ?? Array.Empty<NarrationCue>();
        }

        private void OnEnable()
        {
            foreach (var slider in volumeSliders) slider?.onValueChanged.AddListener(SetVolume);
            foreach (var toggle in muteToggles) toggle?.onValueChanged.AddListener(SetMuted);
            foreach (var button in replayButtons) button?.onClick.AddListener(ReplayInstruction);
        }

        private void OnDisable()
        {
            foreach (var slider in volumeSliders) slider?.onValueChanged.RemoveListener(SetVolume);
            foreach (var toggle in muteToggles) toggle?.onValueChanged.RemoveListener(SetMuted);
            foreach (var button in replayButtons) button?.onClick.RemoveListener(ReplayInstruction);
        }

        private void Start()
        {
            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.loop = false;
                audioSource.spatialBlend = 0f;
            }

            foreach (var volumeSlider in volumeSliders)
            {
                if (volumeSlider == null) continue;
                volumeSlider.minValue = 0f;
                volumeSlider.maxValue = 1f;
                volumeSlider.SetValueWithoutNotify(audioSource == null ? 0.8f : audioSource.volume);
            }
        }

        public void Play(string key, string title, string fallbackCaption)
        {
            currentCue = cues.FirstOrDefault(item => item != null &&
                string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                ?? new NarrationCue { Key = key, Caption = fallbackCaption };
            var caption = currentCue != null && !string.IsNullOrWhiteSpace(currentCue.Caption)
                ? currentCue.Caption
                : fallbackCaption;

            SetText(captionTitles, title);
            SetText(captionTexts, caption);
            if (audioSource == null) return;

            audioSource.Stop();
            audioSource.clip = currentCue?.Clip;
            if (audioSource.clip != null && !audioSource.mute)
            {
                audioSource.Play();
            }
        }

        public void Replay()
        {
            if (audioSource == null || currentCue?.Clip == null) return;
            audioSource.Stop();
            audioSource.clip = currentCue.Clip;
            if (!audioSource.mute) audioSource.Play();
        }

        public void SetMuted(bool value)
        {
            if (audioSource == null) return;
            audioSource.mute = value;
            if (value) audioSource.Stop();
            TrainingSession.VoiceMuted = value;
        }

        public void ToggleMuted()
        {
            SetMuted(!IsMuted);
            foreach (var toggle in muteToggles) toggle?.SetIsOnWithoutNotify(IsMuted);
        }

        public void SetVolume(float value)
        {
            if (audioSource != null) audioSource.volume = Mathf.Clamp01(value);
            TrainingSession.VoiceVolume = Mathf.Clamp01(value);
            foreach (var slider in volumeSliders) slider?.SetValueWithoutNotify(Mathf.Clamp01(value));
        }

        public void ResetNarration()
        {
            currentCue = null;
            instructionCue = null;
            audioSource?.Stop();
            SetText(captionTitles, "Choose a lesson mode");
            SetText(captionTexts, "Guided Training gives spoken prompts and cues. Assessment records mistakes and requested hints.");
        }

        private static void SetText(TMP_Text[] targets, string value)
        {
            if (targets == null) return;
            foreach (var target in targets) if (target != null) target.text = value;
        }
    }
}
