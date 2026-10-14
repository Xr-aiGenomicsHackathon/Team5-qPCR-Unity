using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Team5.qPCR.PlayModeTests
{
    [Category("CurrentTrainingRoute")]
    public sealed class PpePresentationRegressionTests
    {
        private readonly List<GameObject> created = new List<GameObject>();
        private PPELessonController lesson;

        [UnitySetUp]
        public IEnumerator LoadLab()
        {
            yield return SceneManager.LoadSceneAsync("DemoScene");
            yield return null;
            yield return null;
            lesson = Object.FindFirstObjectByType<PPELessonController>();
            lesson.BeginLesson();
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
            created.Clear();
        }

        [Test]
        public void WaterStartsAtTheVisibleFaucetOutlet()
        {
            var water = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)
                .Single(p => p.name == "Running_Water");
            Assert.That(Vector3.Distance(water.transform.position,
                new Vector3(2.562f, 1.322f, -2.014f)), Is.LessThan(.015f),
                "The stream must start at the visible nozzle, not at the faucet base.");
        }

        [UnityTest]
        public IEnumerator BothHandsUnderVisibleNozzleCompleteOneSecondRinse()
        {
            var left = Probe(PPEHandSide.Left);
            var right = Probe(PPEHandSide.Right);
            Assert.IsTrue(lesson.TryWearCoat());
            Assert.IsTrue(lesson.TrySetTap(true));
            left.MovePosition(new Vector3(2.522f, 1.17f, -2.014f));
            right.MovePosition(new Vector3(2.602f, 1.17f, -2.014f));
            yield return new WaitForSeconds(.2f);
            Assert.IsTrue(Object.FindFirstObjectByType<PPEOutletStatus>().IsVisible,
                "Feedback belongs beside the active outlet, not only on the distant preparation card.");
            yield return new WaitForSeconds(1.05f);
            Assert.AreEqual(PPELessonStep.TurnOffTap, lesson.CurrentStep,
                "Actual hand-trigger contacts must advance directly to tap-off.");
        }

        [UnityTest]
        public IEnumerator OneHandAndInterruptedContactCannotCompleteRinse()
        {
            var box = Object.FindFirstObjectByType<HandWashZone>().GetComponent<BoxCollider>();
            var centre = box.transform.TransformPoint(box.center);
            var left = Probe(PPEHandSide.Left);
            var right = Probe(PPEHandSide.Right);
            Assert.IsTrue(lesson.TryWearCoat());
            Assert.IsTrue(lesson.TrySetTap(true));
            left.MovePosition(centre + Vector3.left * .04f);
            right.MovePosition(centre + Vector3.right * 1f);
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(PPELessonStep.WetHands, lesson.CurrentStep);
            right.MovePosition(centre + Vector3.right * .04f);
            yield return new WaitForSeconds(.55f);
            right.MovePosition(centre + Vector3.right * 1f);
            yield return new WaitForSeconds(.15f);
            right.MovePosition(centre + Vector3.right * .04f);
            yield return new WaitForSeconds(.55f);
            Assert.AreEqual(PPELessonStep.WetHands, lesson.CurrentStep,
                "Two interrupted half-second contacts are not one continuous second.");
            yield return new WaitForSeconds(.7f);
            Assert.AreEqual(PPELessonStep.TurnOffTap, lesson.CurrentStep);
        }

        [UnityTest]
        public IEnumerator ResetWhileHandsRemainInsideWaterRecoversDetection()
        {
            var box = Object.FindFirstObjectByType<HandWashZone>().GetComponent<BoxCollider>();
            var centre = box.transform.TransformPoint(box.center);
            var left = Probe(PPEHandSide.Left);
            var right = Probe(PPEHandSide.Right);
            Assert.IsTrue(lesson.TryWearCoat());
            Assert.IsTrue(lesson.TrySetTap(true));
            left.MovePosition(centre + Vector3.left * .04f);
            right.MovePosition(centre + Vector3.right * .04f);
            yield return new WaitForSeconds(.2f);
            Assert.Greater(lesson.StepProgress, 0f);
            lesson.BeginLesson();
            Assert.IsTrue(lesson.TryWearCoat());
            Assert.IsTrue(lesson.TrySetTap(true));
            yield return new WaitForSeconds(1.25f);
            Assert.AreEqual(PPELessonStep.TurnOffTap, lesson.CurrentStep,
                "Reset must not require hands to leave and re-enter the trigger.");
        }

        private Rigidbody Probe(PPEHandSide side)
        {
            var go = new GameObject("Test physical " + side + " hand");
            created.Add(go);
            go.transform.position = new Vector3(0, 3, 0);
            var sphere = go.AddComponent<SphereCollider>();
            sphere.radius = .055f;
            sphere.isTrigger = true;
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var probe = go.AddComponent<PPEHandProbe>();
            typeof(PPEHandProbe).GetField("side", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(probe, side);
            typeof(PPEHandProbe).GetField("lesson", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(probe, lesson);
            lesson.RegisterHand(probe);
            return body;
        }

        [UnityTest]
        public IEnumerator ResetWithHandsStillInDryerRecoversAndDisabledProbeClears()
        {
            var box = Object.FindFirstObjectByType<HandDryerZone>().GetComponent<BoxCollider>();
            var centre = box.transform.TransformPoint(box.center);
            var left = Probe(PPEHandSide.Left); var right = Probe(PPEHandSide.Right);
            left.MovePosition(centre + Vector3.left * .04f); right.MovePosition(centre + Vector3.right * .04f);
            yield return new WaitForSeconds(.15f);
            lesson.BeginLesson();
            typeof(PPELessonController).GetMethod("SetStep", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(lesson, new object[] { PPELessonStep.DryHands, true });
            yield return new WaitForSeconds(.3f);
            Assert.IsTrue(lesson.LeftInDryer && lesson.RightInDryer);
            right.gameObject.SetActive(false);
            yield return new WaitForSeconds(.1f);
            Assert.IsFalse(lesson.RightInDryer, "Disabled probes cannot remain detected.");
            Assert.AreEqual(PPELessonStep.DryHands, lesson.CurrentStep);
            right.gameObject.SetActive(true);
            yield return new WaitForSeconds(2.2f);
            Assert.AreEqual(PPELessonStep.WearLeftGlove, lesson.CurrentStep);
        }

        [Test]
        public void PresentationHasEightStableStepsAndNoSkippedAudio()
        {
            Assert.AreEqual(PPELessonMode.Presentation, lesson.Mode);
            Assert.AreEqual(8, lesson.ActiveStepCount);
            Assert.AreEqual(1, lesson.ActiveStepNumber);
            Assert.AreEqual(6, (int)PPELessonStep.TurnOffTap, "Serialized enum IDs must stay compatible.");
            Assert.That(lesson.InstructionCueKey, Does.Contain("Presentation"));
            Assert.That(PPELessonController.InstructionFor(PPELessonStep.Complete, lesson.Mode), Does.Contain("not complete handwashing"));
        }
    }
}
