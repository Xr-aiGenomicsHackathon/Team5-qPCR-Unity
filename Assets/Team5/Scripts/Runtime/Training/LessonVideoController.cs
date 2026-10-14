using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Team5.qPCR
{
    [Serializable]
    public sealed class LessonVideoCue
    {
        public QpcrLessonState State = QpcrLessonState.ObserveAmplification;
        public VideoClip Clip;
        public string Title = "How qPCR works";
        [TextArea] public string Caption = "Illustration only: video timing is independent of this accelerated run.";
        public bool AutoOpen = true;
    }

    public sealed class LessonVideoController : MonoBehaviour
    {
        [Header("Drop your imported video into the Clip field below")]
        [SerializeField] private LessonVideoCue[] cues = { new LessonVideoCue() };
        [SerializeField] private QpcrLessonController lesson;
        [SerializeField] private NarrationController narration;
        [SerializeField] private VideoPlayer player;
        [SerializeField] private AudioSource videoAudio;
        [SerializeField] private RenderTexture videoTexture;
        [SerializeField] private GameObject panel;
        [SerializeField] private RawImage picture;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text caption;
        [SerializeField] private TMP_Text status;
        [SerializeField] private TMP_Text playLabel;
        [SerializeField] private TMP_Text soundLabel;
        [SerializeField] private Button watchButton;
        private LessonVideoCue currentCue;
        private Coroutine preparing;
        private int requestVersion;
        private bool preparationFailed;
        private bool userMuted = true;
        private QpcrLessonState? lastState;
        public bool IsPanelOpen => panel != null && panel.activeSelf;
        public bool IsUserMuted => userMuted;
        public bool HasVideo => currentCue?.Clip != null;

        private void Awake()
        {
            if (player != null)
            {
                player.playOnAwake = false;
                player.isLooping = false;
                player.source = VideoSource.VideoClip;
                player.renderMode = VideoRenderMode.RenderTexture;
                player.targetTexture = videoTexture;
                player.aspectRatio = VideoAspectRatio.FitInside;
                player.audioOutputMode = VideoAudioOutputMode.AudioSource;
                player.skipOnDrop = true;
            }
            if (videoAudio != null) { videoAudio.playOnAwake = false; videoAudio.spatialBlend = 0; videoAudio.mute = true; }
            if (picture != null) { picture.texture = videoTexture; picture.raycastTarget = false; picture.enabled = false; }
            if (panel != null) panel.SetActive(false);
        }
        private void OnEnable()
        {
            if (lesson != null) lesson.StateChanged += OnStateChanged;
            if (player != null) { player.errorReceived += OnError; player.loopPointReached += OnVideoFinished; }
        }
        private void Start() => OnStateChanged(lesson == null ? QpcrLessonState.NotStarted : lesson.CurrentState);
        private void OnDisable()
        {
            if (lesson != null) lesson.StateChanged -= OnStateChanged;
            if (player != null) { player.errorReceived -= OnError; player.loopPointReached -= OnVideoFinished; }
            StopPlayback();
            lastState = null;
        }
        private void Update()
        {
            if (videoAudio != null) videoAudio.mute = userMuted || (narration != null && narration.IsSpeaking);
            if (soundLabel != null) soundLabel.text = userMuted ? "Sound On" : "Sound Off";
            if (playLabel != null) playLabel.text = player != null && player.isPlaying ? "Pause" : "Play";
        }
        private void OnStateChanged(QpcrLessonState state)
        {
            // A duplicate state notification must not reopen a learner-closed video.
            if (lastState == state) return;
            lastState = state;
            if (state == QpcrLessonState.NotStarted)
            {
                Close(); currentCue = null; RefreshAvailability(); return;
            }
            var mapped = Array.Find(cues, c => c != null && c.State == state && c.Clip != null);
            if (mapped != null)
            {
                StopPlayback(); currentCue = mapped; userMuted = true;
                if (mapped.AutoOpen) Open();
            }
            else if (state == QpcrLessonState.InterpretResults || state == QpcrLessonState.Complete) Close();
            else if (state != QpcrLessonState.ObserveAmplification) { Close(); currentCue = null; }
            RefreshAvailability();
        }
        private void RefreshAvailability()
        {
            if (watchButton != null) watchButton.interactable = HasVideo;
        }
        public void Open()
        {
            if (!HasVideo || player == null || panel == null) return;
            StopPlayback();
            panel.SetActive(true);
            if (title != null) title.text = currentCue.Title;
            if (caption != null) caption.text = currentCue.Caption;
            if (status != null) status.text = "Preparing your teaching video...";
            if (picture != null) picture.enabled = false;
            preparing = StartCoroutine(PrepareVideo(++requestVersion, currentCue.Clip));
        }
        private IEnumerator PrepareVideo(int version, VideoClip clip)
        {
            preparationFailed = false;
            player.clip = clip;
            player.Prepare();
            var started = Time.realtimeSinceStartup;
            while (!player.isPrepared && !preparationFailed && version == requestVersion && IsPanelOpen)
            {
                if (Time.realtimeSinceStartup - started > 20f) { preparationFailed = true; break; }
                yield return null;
            }
            if (version != requestVersion || !IsPanelOpen || player.clip != clip) yield break;
            preparing = null;
            if (preparationFailed)
            {
                if (status != null) status.text = "This video could not play. Try Replay or replace the clip. Your qPCR run continues.";
                yield break;
            }
            if (player.audioTrackCount > 0 && videoAudio != null)
            {
                player.EnableAudioTrack(0, true);
                player.SetTargetAudioSource(0, videoAudio);
            }
            if (picture != null) picture.enabled = true;
            if (status != null) status.text = "Video starts muted. The instrument and graph continue independently.";
            player.Play();
        }
        public void TogglePlayback()
        {
            if (player == null || !IsPanelOpen || !player.isPrepared) return;
            if (player.isPlaying) player.Pause(); else player.Play();
        }
        public void Replay() { if (HasVideo) Open(); }
        public void ToggleSound() => userMuted = !userMuted;
        public void Close()
        {
            StopPlayback();
            if (panel != null) panel.SetActive(false);
        }
        private void StopPlayback()
        {
            requestVersion++;
            if (preparing != null) { StopCoroutine(preparing); preparing = null; }
            if (player != null) { player.Stop(); player.clip = null; }
            videoAudio?.Stop();
            if (picture != null) picture.enabled = false;
            if (videoTexture != null && videoTexture.IsCreated())
            {
                var previous = RenderTexture.active;
                try { RenderTexture.active = videoTexture; GL.Clear(false, true, Color.black); }
                finally { RenderTexture.active = previous; }
            }
        }
        private void OnError(VideoPlayer source, string message)
        {
            if (!IsPanelOpen) return;
            preparationFailed = true;
            if (status != null) status.text = "Video playback unavailable. Close or Replay; your experiment continues.";
        }
        private void OnVideoFinished(VideoPlayer source)
        {
            if (status != null) status.text = "Video complete. Replay it or close this panel.";
        }
    }
}
