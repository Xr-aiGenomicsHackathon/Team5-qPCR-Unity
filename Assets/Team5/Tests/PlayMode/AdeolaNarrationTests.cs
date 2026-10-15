using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Team5.qPCR.PlayModeTests
{
    public sealed class NarrationPlaybackTests
    {
        private GameObject root;
        private AudioSource source;
        private NarrationController narration;
        private AudioClip instruction;
        private AudioClip explanation;

        [UnitySetUp]
        public IEnumerator CreateNarration()
        {
            root = new GameObject("Narration test only");
            source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.volume = 0f; // Test playback state without adding audible test tones.
            narration = root.AddComponent<NarrationController>();
            instruction = AudioClip.Create("Instruction_Test", 88200, 1, 44100, false);
            explanation = AudioClip.Create("Why_Test", 88200, 1, 44100, false);
            narration.Configure(source, null, null, null, null, null, new[]
            {
                new NarrationCue { Key = "Instruction_Test", Caption = "Task instruction", Clip = instruction },
                new NarrationCue { Key = "Why_Test", Caption = "Optional explanation", Clip = explanation }
            });
            yield return null;
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(instruction);
            Object.DestroyImmediate(explanation);
            TrainingSession.VoiceMuted = false;
            TrainingSession.VoiceVolume = .8f;
        }

        [Test]
        public void ReplayInstructionDoesNotReplayTheLastExplanation()
        {
            narration.PlayInstruction("Instruction_Test", "Task", "Task instruction");
            narration.Play("Why_Test", "Why?", "Optional explanation");
            Assert.AreSame(explanation, source.clip);
            narration.ReplayInstruction();
            Assert.AreEqual("Instruction_Test", narration.CurrentKey);
            Assert.AreSame(instruction, source.clip);
        }

        [Test]
        public void MutingStopsSpeechAndUnmutingDoesNotRestartIt()
        {
            narration.PlayInstruction("Instruction_Test", "Task", "Task instruction");
            narration.SetMuted(true);
            Assert.IsFalse(source.isPlaying);
            Assert.IsTrue(TrainingSession.VoiceMuted);
            narration.SetMuted(false);
            Assert.IsFalse(source.isPlaying);
            Assert.IsFalse(TrainingSession.VoiceMuted);
        }

        [Test]
        public void MissingRecordingStopsOldSpeechAndKeepsWrittenGuidance()
        {
            narration.PlayInstruction("Instruction_Test", "Task", "Task instruction");
            narration.Play("Missing_Test", "Next task", "Written guidance remains available.");
            Assert.IsNull(source.clip);
            Assert.IsFalse(source.isPlaying);
            Assert.AreEqual("Written guidance remains available.", narration.CurrentCaption);
        }

        [Test]
        public void VolumeIsClampedAndSavedOnlyInTheSession()
        {
            narration.SetVolume(2f);
            Assert.AreEqual(1f, source.volume);
            Assert.AreEqual(1f, TrainingSession.VoiceVolume);
            narration.SetVolume(-1f);
            Assert.AreEqual(0f, source.volume);
            Assert.AreEqual(0f, TrainingSession.VoiceVolume);
        }
    }

    public sealed class AdeolaSceneAudioTests
    {
        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        [UnityTest]
        public IEnumerator BothScenesUseExactAdeolaNamesAndOneListener()
        {
            // No simulated controllers or simulator GameObjects are created by these tests.
            foreach (var sceneName in new[] { "LobbyScene", "DemoScene" })
            {
                yield return SceneManager.LoadSceneAsync(sceneName);
                yield return null;
                yield return null;
                var controller = Object.FindFirstObjectByType<NarrationController>();
                Assert.NotNull(controller);
                var cues = Field<NarrationCue[]>(controller, "cues");
                Assert.AreEqual(88, cues.Length, sceneName);
                Assert.AreEqual(88, cues.Select(cue => cue.Key).Distinct().Count(), sceneName);
                Assert.AreEqual(88, cues.Count(cue => cue.Clip != null), sceneName + ": every cue needs its approved recording.");
                foreach (var cue in cues.Where(cue => cue.Clip != null))
                    Assert.IsTrue(cue.Clip.name == cue.Key ||
                        (cue.Key == "Parameter_PostRunHold" && cue.Clip.name == "Parameter_PostRunHold_15C") ||
                        (cue.Key == "Why_Ppe_Presentation_TurnOnTap" && cue.Clip.name == "Why_Ppe_Presentation_WetHands"),
                        sceneName + ": unexpected recording for " + cue.Key);
                Assert.AreEqual("Instruction_Lobby_Welcome", cues.Single(cue => cue.Key == "Instruction_Lobby_Welcome").Clip.name);
                Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(listener => listener.isActiveAndEnabled));
                var audio = Field<AudioSource>(controller, "audioSource");
                Assert.AreEqual(0f, audio.spatialBlend);
                Assert.IsFalse(audio.playOnAwake);
                Assert.IsFalse(audio.loop);
            }
        }

        [UnityTest]
        public IEnumerator EveryAssignedCueLoadsAndStartsPlaybackIncludingFifteenDegreeHold()
        {
            yield return SceneManager.LoadSceneAsync("DemoScene");
            yield return null;
            yield return null;
            var narration = Object.FindFirstObjectByType<NarrationController>();
            var source = Field<AudioSource>(narration, "audioSource");
            var previousMute = narration.IsMuted;
            var previousVolume = source.volume;
            narration.SetMuted(false);
            narration.SetVolume(0f); // Inspect playback without playing 88 rapid snippets aloud.
            var cues = Field<NarrationCue[]>(narration, "cues");
            foreach (var cue in cues)
            {
                Assert.NotNull(cue.Clip, cue.Key);
                Assert.Greater(cue.Clip.length, 0f, cue.Key);
                Assert.AreEqual(1, cue.Clip.channels, cue.Key);
                Assert.AreEqual(44100, cue.Clip.frequency, cue.Key);
                Assert.IsTrue(cue.Clip.LoadAudioData(), cue.Key);
                narration.Play(cue.Key, cue.Key, cue.Caption);
                yield return null;
                Assert.AreSame(cue.Clip, source.clip, cue.Key);
                Assert.IsTrue(source.isPlaying, cue.Key);
                Assert.IsTrue(narration.IsSpeaking, cue.Key);
            }
            Assert.AreEqual("Parameter_PostRunHold_15C", cues.Single(c => c.Key == "Parameter_PostRunHold").Clip.name);
            Assert.That(cues.Single(c => c.Key == "Parameter_PostRunHold").Caption, Does.Contain("15 degrees C"));
            narration.StopReading();
            narration.SetVolume(previousVolume);
            narration.SetMuted(previousMute);
        }

        [UnityTest]
        public IEnumerator WhyAndParameterSelectionStaySilentUntilReadAloud()
        {
            yield return SceneManager.LoadSceneAsync("DemoScene");
            yield return null;
            yield return null;
            var narration = Object.FindFirstObjectByType<NarrationController>();
            var ui = Object.FindFirstObjectByType<QpcrLessonUI>(FindObjectsInactive.Include);
            narration.PlayInstruction("Instruction_Qpcr_InspectPlateId", "Plate ID", "Check the plate ID.");
            var instruction = Field<AudioSource>(narration, "audioSource").clip;
            ui.ToggleWhy();
            Assert.AreEqual("Instruction_Qpcr_InspectPlateId", narration.CurrentKey);
            Assert.AreSame(instruction, Field<AudioSource>(narration, "audioSource").clip);
            ui.ShowParameterExplanation(QpcrProtocolParameter.ReactionVolume);
            Assert.AreEqual("Instruction_Qpcr_InspectPlateId", narration.CurrentKey);
            ui.ReadExplanation();
            Assert.AreEqual("Parameter_ReactionVolume", narration.CurrentKey);
            Assert.AreEqual("Parameter_ReactionVolume", Field<AudioSource>(narration, "audioSource").clip.name);
            narration.ReplayInstruction();
            Assert.AreSame(instruction, Field<AudioSource>(narration, "audioSource").clip);
            narration.StopReading();
        }

        [UnityTest]
        public IEnumerator PresentationPpeRequestsItsOwnCuesWithoutBlockingOnMissingAudio()
        {
            yield return SceneManager.LoadSceneAsync("DemoScene");
            yield return null;
            yield return null;
            var ppe = Object.FindFirstObjectByType<PPELessonController>();
            var narration = Object.FindFirstObjectByType<NarrationController>();
            ppe.ConfigureMode(PPELessonMode.Presentation);
            ppe.BeginLesson();
            Assert.AreEqual("Instruction_Ppe_Presentation_WearLabCoat", narration.CurrentKey);
            Assert.IsTrue(ppe.TryWearCoat());
            Assert.IsTrue(ppe.TrySetTap(true));
            Assert.AreEqual("Instruction_Ppe_Presentation_WetHands", narration.CurrentKey);
            ppe.SetHandUnderWater(PPEHandSide.Left, true);
            ppe.SetHandUnderWater(PPEHandSide.Right, true);
            yield return new WaitForSeconds(1.15f);
            Assert.AreEqual(PPELessonStep.TurnOffTap, ppe.CurrentStep);
            Assert.IsTrue(ppe.TrySetTap(false));
            Assert.AreEqual("Instruction_Ppe_Presentation_DryHands", narration.CurrentKey);
            ppe.SetHandInDryer(PPEHandSide.Left, true);
            ppe.SetHandInDryer(PPEHandSide.Right, true);
            yield return new WaitForSeconds(2.15f);
            Assert.IsTrue(ppe.TryWearGlove(PPEHandSide.Left));
            Assert.IsTrue(ppe.TryWearGlove(PPEHandSide.Right));
            Assert.IsTrue(ppe.IsComplete);
            Assert.AreEqual("Instruction_Ppe_Presentation_Complete", narration.CurrentKey);
        }
    }
}
