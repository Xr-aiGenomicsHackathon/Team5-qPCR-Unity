using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Team5.qPCR.PlayModeTests
{
    public sealed class PpeSlidingGlassDoorTests
    {
        private GameObject fixture;
        private PPELessonController ppe;
        private PpeSlidingGlassDoor door;
        private Rigidbody body;
        private Vector3 closed;
        private Vector3 expectedOpen;

        [UnitySetUp]
        public IEnumerator CreateDoor()
        {
            fixture = new GameObject("Sliding door test fixture");
            fixture.SetActive(false);
            fixture.transform.position = new Vector3(1000f, 5f, 1000f);

            var lessonObject = new GameObject("PPE test lesson");
            lessonObject.transform.SetParent(fixture.transform, false);
            ppe = lessonObject.AddComponent<PPELessonController>();
            SetField(ppe, "beginOnStart", false);

            var panel = new GameObject("Moving test glass");
            panel.transform.SetParent(fixture.transform, false);
            panel.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            panel.transform.localScale = Vector3.one * 3f;
            panel.AddComponent<BoxCollider>();
            body = panel.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            door = panel.AddComponent<PpeSlidingGlassDoor>();
            SetField(door, "ppe", ppe);
            SetField(door, "slideDistance", 1.1f);
            SetField(door, "openingSeconds", 0.12f);

            fixture.SetActive(true);
            ppe.BeginLesson();
            closed = body.position;
            expectedOpen = closed + panel.transform.TransformDirection(Vector3.left) * 1.1f;
            yield return null;
        }

        [TearDown]
        public void Cleanup()
        {
            if (fixture != null) Object.DestroyImmediate(fixture);
        }

        [UnityTest]
        public IEnumerator EarlyRequestCannotOpenDoor()
        {
            door.OpenAfterPpe();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(door.IsOpening);
            Assert.IsFalse(door.IsOpen);
            Assert.That(Vector3.Distance(body.position, closed), Is.LessThan(0.001f));
        }

        [UnityTest]
        public IEnumerator FinalGloveOpensOnceAndTravelIgnoresModelScale()
        {
            SetStep(PPELessonStep.WearLeftGlove);
            Assert.IsTrue(ppe.TryWearGlove(PPEHandSide.Left));
            Assert.IsFalse(door.IsOpening, "The left glove is not the final PPE checkpoint.");
            Assert.IsTrue(ppe.TryWearGlove(PPEHandSide.Right));
            Assert.IsTrue(door.IsOpening);
            door.OpenAfterPpe();

            yield return new WaitForSeconds(0.25f);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(door.IsOpen);
            Assert.IsFalse(door.IsOpening);
            Assert.That(Vector3.Distance(body.position, expectedOpen), Is.LessThan(0.003f));
            Assert.That(Vector3.Distance(body.position, closed), Is.EqualTo(1.1f).Within(0.003f));

            door.OpenAfterPpe();
            yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Distance(body.position, expectedOpen), Is.LessThan(0.003f));
        }

        [UnityTest]
        public IEnumerator EnablingAfterPpeCompletionStillOpensDoor()
        {
            door.enabled = false;
            SetStep(PPELessonStep.WearRightGlove);
            Assert.IsTrue(ppe.TryWearGlove(PPEHandSide.Right));
            Assert.IsFalse(door.IsOpening);
            door.enabled = true;
            Assert.IsTrue(door.IsOpening);
            yield return new WaitForSeconds(0.25f);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(door.IsOpen);
            Assert.That(Vector3.Distance(body.position, expectedOpen), Is.LessThan(0.003f));
        }

        private void SetStep(PPELessonStep step)
        {
            typeof(PPELessonController).GetMethod("SetStep", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(ppe, new object[] { step, true });
        }

        private static void SetField(object instance, string name, object value)
        {
            instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(instance, value);
        }
    }
}
