using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Video;

namespace Team5.qPCR.PlayModeTests
{
    [Category("VideoDoor")]
    public sealed class QpcrVideoIntegrationTests
    {
        private LessonVideoController video;
        private VideoPlayer player;
        private QpcrLessonController lesson;
        private NarrationController narration;
        private bool previousMute;
        private float previousVolume;
        private static T Field<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private void State(QpcrLessonState state) => typeof(QpcrLessonController)
            .GetMethod("SetState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(lesson, new object[] { state });

        [UnitySetUp]
        public IEnumerator LoadLab()
        {
            previousMute = TrainingSession.VoiceMuted;
            previousVolume = TrainingSession.VoiceVolume;
            // The user removed the simulator but an editor-only sample help menu
            // remains. Disable that orphan in this test instance before Start;
            // do not change saved scenes or create any simulated input devices.
            SceneManager.sceneLoaded += DisableOrphanedSimulatorHelp;
            try { yield return SceneManager.LoadSceneAsync("DemoScene"); }
            finally { SceneManager.sceneLoaded -= DisableOrphanedSimulatorHelp; }
            yield return null;
            yield return null;
            video = UnityEngine.Object.FindFirstObjectByType<LessonVideoController>();
            player = Field<VideoPlayer>(video, "player");
            lesson = Field<QpcrLessonController>(video, "lesson");
            narration = Field<NarrationController>(video, "narration");
            narration.SetMuted(true);
        }

        private static void DisableOrphanedSimulatorHelp(Scene scene, LoadSceneMode mode)
        {
            foreach (var component in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)))
                if (component != null && component.GetType().Name == "XRInteractionSimulatorPlayModeMenu") component.enabled = false;
        }

        [TearDown]
        public void Cleanup()
        {
            if (video != null) video.Close();
            if (narration != null) { narration.StopReading(); narration.SetMuted(previousMute); narration.SetVolume(previousVolume); }
            TrainingSession.VoiceMuted = previousMute;
            TrainingSession.VoiceVolume = previousVolume;
        }

        private IEnumerator WaitForVideo()
        {
            var deadline = Time.realtimeSinceStartup + 15f;
            while (!player.isPlaying && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(player.isPrepared, "The imported MP4 must prepare in the Editor.");
            Assert.IsTrue(player.isPlaying, "The assigned cue must start playback.");
        }

        [UnityTest]
        public IEnumerator RealRunKeepsDrawingWhenVideoPausedOrClosedAndAllowsResultsReplay()
        {
            Assert.IsFalse(video.IsPanelOpen);
            var screen = UnityEngine.Object.FindFirstObjectByType<QpcrTouchscreenController>(FindObjectsInactive.Include);
            var run = UnityEngine.Object.FindFirstObjectByType<QpcrRunController>();
            var graph = Field<AmplificationGraphView>(run, "amplificationGraph");
            lesson.ConfigurePpePrerequisite(null); // Test setup only; no saved prerequisite is changed.
            lesson.BeginLesson();
            State(QpcrLessonState.ConfigureProtocol);
            var values = Field<Dictionary<QpcrProtocolParameter, float>>(screen, "values");
            foreach (QpcrProtocolParameter parameter in Enum.GetValues(typeof(QpcrProtocolParameter)))
                values[parameter] = QpcrProtocolValidator.ExpectedValue(parameter);
            screen.ValidateProtocol();
            Assert.IsTrue(screen.IsProtocolValid);
            run.StartRun();
            Assert.AreEqual(QpcrLessonState.ObserveAmplification, lesson.CurrentState);
            Assert.IsTrue(video.IsPanelOpen);
            Assert.IsTrue(video.IsUserMuted);
            yield return WaitForVideo();
            yield return new WaitForSeconds(1f);
            video.TogglePlayback();
            Assert.IsFalse(player.isPlaying);
            var before = graph.Progress;
            yield return new WaitForSeconds(2f);
            Assert.Greater(graph.Progress, before);
            Assert.IsTrue(run.IsRunning);
            video.Close();
            before = graph.Progress;
            yield return new WaitForSeconds(.75f);
            Assert.Greater(graph.Progress, before);
            var deadline = Time.realtimeSinceStartup + 32f;
            while (lesson.CurrentState == QpcrLessonState.ObserveAmplification && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(QpcrLessonState.InterpretResults, lesson.CurrentState);
            Assert.IsFalse(run.IsRunning);
            Assert.AreEqual(1f, graph.Progress);
            Assert.IsFalse(video.IsPanelOpen);
            Assert.IsTrue(Field<UnityEngine.UI.Button>(video, "watchButton").interactable);
            video.Open();
            yield return WaitForVideo();
            lesson.ResetLesson();
            Assert.IsFalse(video.IsPanelOpen);
            Assert.IsNull(player.clip);
            Assert.IsFalse(Field<UnityEngine.UI.Button>(video, "watchButton").interactable);
        }

        [UnityTest]
        public IEnumerator NarrationPriorityDuplicateStateAndResetDuringPreparation()
        {
            State(QpcrLessonState.ObserveAmplification);
            yield return WaitForVideo();
            var audio = Field<AudioSource>(video, "videoAudio");
            Assert.IsTrue(audio.mute);
            video.ToggleSound();
            narration.SetMuted(false);
            narration.PlayInstruction("Instruction_Qpcr_ObserveAmplification", "Run", "Observe the graph.");
            yield return null;
            yield return null;
            Assert.IsTrue(audio.mute, "Instruction narration takes priority over video sound.");
            narration.StopReading();
            yield return null;
            yield return null;
            Assert.IsFalse(audio.mute, "Restore the learner's chosen video sound after narration.");
            video.Close();
            State(QpcrLessonState.ObserveAmplification);
            Assert.IsFalse(video.IsPanelOpen, "Repeated state events cannot reopen a dismissed clip.");
            video.Open();
            State(QpcrLessonState.NotStarted);
            yield return null;
            yield return null;
            Assert.IsFalse(video.IsPanelOpen);
            Assert.IsNull(player.clip);
            Assert.IsFalse(Field<UnityEngine.UI.RawImage>(video, "picture").enabled);
        }

        [UnityTest]
        public IEnumerator MissingCueStaysHiddenAndPreparationFailureCanBeReplayed()
        {
            var cues = Field<LessonVideoCue[]>(video, "cues");
            var clip = cues[0].Clip;
            cues[0].Clip = null;
            State(QpcrLessonState.ObserveAmplification);
            Assert.IsFalse(video.IsPanelOpen);
            Assert.IsFalse(Field<UnityEngine.UI.Button>(video, "watchButton").interactable);
            cues[0].Clip = clip;
            State(QpcrLessonState.NotStarted);
            State(QpcrLessonState.ObserveAmplification);
            typeof(LessonVideoController).GetMethod("OnError", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(video, new object[] { player, "Synthetic preparation error for test" });
            yield return null;
            Assert.That(Field<TMPro.TMP_Text>(video, "status").text, Does.Contain("continues"));
            video.Replay();
            yield return WaitForVideo();
        }

        [Test]
        public void SavedWiringUsesOptimizedClipGateAndExistingSingleListener()
        {
            var cue = Field<LessonVideoCue[]>(video, "cues").Single(c => c.State == QpcrLessonState.ObserveAmplification);
            Assert.AreEqual("QpcrProcess_Quest", cue.Clip.name);
            Assert.AreEqual(720u, cue.Clip.width);
            Assert.AreEqual(1280u, cue.Clip.height);
            Assert.AreEqual(1280, Field<RenderTexture>(video, "videoTexture").width);
            Assert.AreEqual(720, Field<RenderTexture>(video, "videoTexture").height);
            Assert.AreEqual(VideoAspectRatio.FitInside, player.aspectRatio);
            Assert.IsFalse(player.isLooping);
            Assert.IsFalse(player.playOnAwake);
            Assert.IsFalse(Field<UnityEngine.UI.RawImage>(video, "picture").raycastTarget);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l => l.isActiveAndEnabled));
            var floor = UnityEngine.Object.FindObjectsByType<DoorAwareTeleportationArea>(FindObjectsSortMode.None).Single();
            Assert.NotNull(Field<DoorwayAccessGate>(floor, "doorway"));
            Assert.NotNull(Field<CharacterController>(floor, "learnerBody"));
            Assert.IsFalse(UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Any(c => c != null && (c.GetType().Name == "XRInteractionSimulator" || c.GetType().Name == "CollisionAwareXRInteractionSimulator")));
        }
    }
}
