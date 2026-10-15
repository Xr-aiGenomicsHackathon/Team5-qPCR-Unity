using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Team5.qPCR.PlayModeTests
{
    [Category("VideoDoor")]
    public sealed class DoorwayAccessTests
    {
        private GameObject root;
        private PPELessonController ppe;
        private PpeSlidingGlassDoor door;
        private DoorwayAccessGate gate;
        private BoxCollider blocker;
        private CharacterController body;
        private DoorAwareTeleportationArea area;

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        [UnitySetUp]
        public IEnumerator Setup()
        {
            root = new GameObject("Door access test fixture");
            root.SetActive(false);
            root.transform.position = new Vector3(1000, 0, 1000);
            var lesson = new GameObject("PPE");
            lesson.transform.SetParent(root.transform, false);
            ppe = lesson.AddComponent<PPELessonController>();
            Set(ppe, "beginOnStart", false);
            var leaf = new GameObject("Glass");
            leaf.transform.SetParent(root.transform, false);
            leaf.transform.localPosition = new Vector3(0, 1.35f, 0);
            leaf.AddComponent<BoxCollider>().size = new Vector3(.06f, 2.7f, 1.4f);
            leaf.AddComponent<Rigidbody>();
            door = leaf.AddComponent<PpeSlidingGlassDoor>();
            Set(door, "ppe", ppe);
            Set(door, "localSlideDirection", Vector3.forward);
            Set(door, "slideDistance", 1.45f);
            Set(door, "openingSeconds", .12f);
            var barrier = new GameObject("Fixed doorway");
            barrier.layer = 9;
            barrier.transform.SetParent(root.transform, false);
            barrier.transform.localPosition = new Vector3(0, 1.35f, 0);
            blocker = barrier.AddComponent<BoxCollider>();
            blocker.size = new Vector3(.1f, 2.7f, 1.4f);
            gate = barrier.AddComponent<DoorwayAccessGate>();
            Set(gate, "door", door);
            Set(gate, "blocker", blocker);
            var learner = new GameObject("Learner body");
            learner.layer = 2;
            learner.transform.SetParent(root.transform, false);
            learner.transform.localPosition = new Vector3(1.2f, 0, 0);
            body = learner.AddComponent<CharacterController>();
            body.radius = .18f;
            body.height = 1.65f;
            body.center = new Vector3(0, .825f, 0);
            body.skinWidth = .02f;
            body.stepOffset = .15f;
            body.minMoveDistance = 0;
            var floor = new GameObject("Floor");
            floor.transform.SetParent(root.transform, false);
            floor.transform.localPosition = new Vector3(0, -.06f, 0);
            floor.AddComponent<BoxCollider>().size = new Vector3(6, .1f, 6);
            area = floor.AddComponent<DoorAwareTeleportationArea>();
            Set(area, "doorway", gate);
            Set(area, "learnerBody", body);
            root.SetActive(true);
            ppe.BeginLesson();
            yield return null;
            Physics.SyncTransforms(); // One-off test setup, never a per-frame runtime sync.
        }

        [TearDown]
        public void Cleanup() { if (root != null) Object.DestroyImmediate(root); }

        private Vector3 Point(float x, float z = 0, float y = 0) => root.transform.TransformPoint(new Vector3(x, y, z));
        private void FinishPpe()
        {
            typeof(PPELessonController).GetMethod("SetStep", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(ppe, new object[] { PPELessonStep.WearRightGlove, true });
            Assert.IsTrue(ppe.TryWearGlove(PPEHandSide.Right));
        }

        [Test]
        public void ClosedDoorRejectsBothDirectionsAboveDoorAndCornerClipping()
        {
            Assert.IsFalse(gate.AllowsTeleport(Point(1), Point(-1), .18f));
            Assert.IsFalse(gate.AllowsTeleport(Point(-1), Point(1), .18f));
            Assert.IsFalse(gate.AllowsTeleport(Point(1, 0, 4), Point(-1), .18f));
            Assert.IsFalse(gate.AllowsTeleport(Point(1, .82f), Point(-1, .82f), .18f));
            Assert.IsTrue(gate.AllowsTeleport(Point(1, 0), Point(2, .5f), .18f));
        }

        [Test]
        public void MissingDoorFailsClosed()
        {
            Set(gate, "door", null);
            Assert.IsFalse(gate.IsPassageOpen);
            Assert.IsFalse(gate.AllowsTeleport(Point(1), Point(2), .18f));
            Set(area, "doorway", null);
            Assert.IsFalse(area.CanTeleportTo(Point(2)));
        }

        [Test]
        public void RotatedDoorUsesItsLocalOpeningNotWorldAxes()
        {
            gate.transform.rotation = Quaternion.Euler(0, 90, 0);
            Assert.IsFalse(gate.AllowsTeleport(gate.transform.TransformPoint(Vector3.right), gate.transform.TransformPoint(Vector3.left), .18f));
        }

        [UnityTest]
        public IEnumerator CharacterControllerBlockedUntilOpeningAnimationFinishes()
        {
            body.Move(Vector3.left * 2.4f);
            Assert.Greater(body.transform.localPosition.x, .18f);
            FinishPpe();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(blocker.enabled, "The blocker must not slide away with the glass.");
            yield return new WaitForSeconds(.25f);
            Assert.IsTrue(door.IsOpen);
            Assert.IsFalse(blocker.enabled);
            body.Move(Vector3.left * 1.3f);
            Assert.Less(body.transform.localPosition.x, -.3f);
        }

        [UnityTest]
        public IEnumerator ActualTeleportRequestGeneratorRejectsClosedAndAcceptsOpen()
        {
            Assert.IsTrue(Physics.Raycast(Point(-1, 0, 2), Vector3.down, out var hit, 4));
            var method = typeof(DoorAwareTeleportationArea).GetMethod("GenerateTeleportRequest", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.IsFalse((bool)method.Invoke(area, new object[] { null, hit, new TeleportRequest() }));
            FinishPpe();
            yield return new WaitForSeconds(.25f);
            Assert.IsTrue((bool)method.Invoke(area, new object[] { null, hit, new TeleportRequest() }));
        }

        [UnityTest]
        public IEnumerator HeadCrossingStaysFadedUntilReturningWithoutMovingTrackedHead()
        {
            var trackedHead = new GameObject("Tracked test head");
            trackedHead.transform.SetParent(root.transform, false);
            trackedHead.transform.position = Point(1, 0, 1.6f);
            var warningRoot = new GameObject("Boundary test");
            warningRoot.SetActive(false);
            warningRoot.transform.SetParent(root.transform, false);
            var warning = warningRoot.AddComponent<HeadBoundaryWarning>();
            Set(warning, "head", trackedHead.transform);
            Set(warning, "doorwayGate", gate);
            Set(warning, "solidLayers", (LayerMask)(1 << 9));
            warningRoot.SetActive(true);
            yield return null;
            trackedHead.transform.position = Point(-1, 0, 1.6f);
            yield return null;
            yield return null;
            Assert.IsTrue(warning.IsPenetrating);
            Assert.AreEqual(Point(-1, 0, 1.6f), trackedHead.transform.position, "Never freeze or relocate tracked head poses.");
            yield return null;
            Assert.IsTrue(warning.IsPenetrating, "Do not clear the warning just because the eye passed entirely through the glass.");
            trackedHead.transform.position = Point(1, 0, 1.6f);
            yield return null;
            yield return null;
            Assert.IsFalse(warning.IsPenetrating);
        }
    }
}
