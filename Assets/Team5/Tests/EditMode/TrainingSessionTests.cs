using NUnit.Framework;
using UnityEngine;

namespace Team5.qPCR.Tests
{
    public sealed class TrainingSessionTests
    {
        [TearDown] public void Reset() { TrainingSession.TryBegin("Learner"); TrainingSession.VoiceMuted=false; TrainingSession.VoiceVolume=.8f; }
        [TestCase(null)] [TestCase("")] [TestCase("   ")]
        public void BlankNamesCannotStartSession(string name) => Assert.IsFalse(TrainingSession.TryBegin(name));
        [Test] public void NamesAreTrimmedAndPpeCleared()
        {
            TrainingSession.PpeComplete=true;
            Assert.IsTrue(TrainingSession.TryBegin("  Joshua  "));
            Assert.AreEqual("Joshua",TrainingSession.Nickname);
            Assert.IsFalse(TrainingSession.PpeComplete);
        }
        [Test] public void NamesAreLimitedToTwentyFourCharacters()
        { Assert.IsTrue(TrainingSession.TryBegin(new string('J',24)));Assert.IsFalse(TrainingSession.TryBegin(new string('J',25))); }
        [Test] public void StillOrTogetherMovingHandsDoNotCountAsScrubbing()
        { Assert.IsFalse(PPELessonController.IsScrubMotion(new Vector3(.1f,0,0),new Vector3(.1f,0,0),.1f)); }
        [Test] public void RelativeMotionCountsButSmallJitterDoesNot()
        {
            Assert.IsTrue(PPELessonController.IsScrubMotion(new Vector3(.1f,0,0),new Vector3(.12f,0,0),.1f));
            Assert.IsFalse(PPELessonController.IsScrubMotion(Vector3.zero,new Vector3(.0001f,0,0),.1f));
        }
        [Test] public void PrerequisiteStopsLessonWithoutReplacingStateMachine()
        {
            var go=new GameObject("Prerequisite test");
            try
            {
                var lesson=go.AddComponent<QpcrLessonController>();var ppe=go.AddComponent<PPELessonController>();
                lesson.ConfigurePpePrerequisite(ppe);lesson.BeginLesson();
                Assert.AreEqual(QpcrLessonState.NotStarted,lesson.CurrentState);Assert.IsFalse(lesson.IsLessonActive);
                lesson.ConfigurePpePrerequisite(null);lesson.BeginLesson();Assert.AreEqual(QpcrLessonState.InspectPlateId,lesson.CurrentState);
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}
