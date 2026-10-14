using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace Team5.qPCR.PlayModeTests
{
    [Category("CurrentTrainingRoute")]
    public sealed class CollisionAwareSimulatorTests
    {
        private CollisionAwareXRInteractionSimulator simulator;
        private XROrigin origin;
        [UnitySetUp] public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("DemoScene"); yield return null; yield return null;
            simulator = Object.FindFirstObjectByType<CollisionAwareXRInteractionSimulator>();
            Assert.NotNull(simulator, "The saved scene must own the collision-aware simulator.");
            origin = Object.FindFirstObjectByType<XROrigin>();
            simulator.targetedDeviceInput = TargetedDevices.FPS;
            simulator.translateXInput.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            simulator.translateYInput.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            simulator.translateZInput.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            yield return new WaitForSeconds(1.1f);
        }
        private void Spawn(Vector3 head, Vector3 forward)
        {
            var body = origin.GetComponent<CharacterController>(); body.enabled = false;
            origin.MatchOriginUpCameraForward(Vector3.up, forward); origin.MoveCameraToWorldLocation(head);
            body.enabled = true; Physics.SyncTransforms();
        }
        private void Input(float x, float z, float y = 0)
        { simulator.translateXInput.manualValue = x; simulator.translateZInput.manualValue = z; simulator.translateYInput.manualValue = y; }

        [UnityTest] public IEnumerator ActualSimulatorNavigationStopsAtWorktopAndWall()
        {
            Spawn(new Vector3(0, 1.65f, 2.2f), Vector3.forward);
            Input(0, 1); yield return new WaitForSeconds(1.6f); Input(0, 0);
            Assert.Less(origin.Camera.transform.position.z, 2.97f, "Worktop must block actual simulator movement.");
            Assert.Greater(origin.Camera.transform.position.z, 2.35f, "Input must really move the body, not merely pass a static check.");
            Spawn(new Vector3(5.3f, 1.65f, 0), Vector3.right);
            Input(0, 1); yield return new WaitForSeconds(1.6f); Input(0, 0);
            Assert.Less(origin.Camera.transform.position.x, 5.91f);
            Assert.Greater(origin.Camera.transform.position.x, 5.4f);
        }
        [UnityTest] public IEnumerator DoorwayRemainsPassableAndHeadModeCannotFly()
        {
            Spawn(new Vector3(4.25f, 1.65f, -3.6f), Vector3.back);
            Input(0, 1); yield return new WaitForSeconds(1.6f); Input(0, 0);
            Assert.Less(origin.Camera.transform.position.z, -4.5f, "Furniture repairs must not plug the entrance opening.");
            Spawn(new Vector3(0, 1.65f, 0), Vector3.forward);
            simulator.targetedDeviceInput = TargetedDevices.HMD;
            // Let normal gravity settle the teleported capsule before testing vertical input.
            yield return new WaitForSeconds(1f);
            var height = origin.Camera.transform.position.y;
            Input(0, 0, 1); yield return new WaitForSeconds(.6f); Input(0, 0);
            Assert.AreEqual(height, origin.Camera.transform.position.y, .05f, "HMD navigation must not free-fly.");
        }
        [UnityTest] public IEnumerator ControllerManipulationStillMovesTheHand()
        {
            Spawn(new Vector3(0, 1.65f, 0), Vector3.forward);
            simulator.targetedDeviceInput = TargetedDevices.LeftDevice;
            var state = typeof(XRInteractionSimulator).GetField("m_LeftControllerState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var before = ((XRSimulatedControllerState)state.GetValue(simulator)).devicePosition;
            var hand = GameObject.Find("Left Controller").transform;
            var renderedBefore = hand.position;
            Input(1, 0); yield return new WaitForSeconds(.4f); Input(0, 0);
            var after = ((XRSimulatedControllerState)state.GetValue(simulator)).devicePosition;
            Assert.Greater(Vector3.Distance(before, after), .1f);
            Assert.Greater(Vector3.Distance(renderedBefore, hand.position), .1f,
                "Changing internal simulator state alone is insufficient: the actual controller must move.");
            yield return new WaitForFixedUpdate(); yield return null;
            Assert.Less(Vector3.Distance(GameObject.Find("LeftWashProbe").transform.position,
                hand.TransformPoint(new Vector3(0,-.02f,-.06f))), .025f);
        }
        [Test] public void CharacterSettingsAndSinkCavityArePreserved()
        {
            var body = origin.GetComponent<CharacterController>();
            Assert.AreEqual(.18f, body.radius); Assert.AreEqual(.02f, body.skinWidth);
            Assert.AreEqual(.15f, body.stepOffset); Assert.AreEqual(0, body.minMoveDistance);
            Assert.AreEqual(1f, simulator.GroundSpeed);
            Assert.NotNull(simulator.GetComponent<XRInteractionSimulatorInputBridge>());
            var sink = GameObject.Find("Sink_and_tap").transform.Find("Back_Bench_Worktop").GetComponent<MeshCollider>();
            Assert.NotNull(sink); Assert.IsFalse(sink.convex); Assert.IsFalse(sink.isTrigger);
        }

        [UnityTest] public IEnumerator SimulatorHandsDriveActualWashingDetection()
        {
            var ppe=Object.FindFirstObjectByType<PPELessonController>();
            ppe.BeginLesson(); Assert.IsTrue(ppe.TryWearCoat()); Assert.IsTrue(ppe.TrySetTap(true));
            PlaceHand(true,new Vector3(2.522f,1.19f,-1.954f));
            yield return new WaitForSeconds(1.15f);
            Assert.AreEqual(PPELessonStep.WetHands,ppe.CurrentStep); Assert.IsTrue(ppe.LeftUnderWater); Assert.IsFalse(ppe.RightUnderWater);
            PlaceHand(false,new Vector3(2.602f,1.19f,-1.954f));
            yield return new WaitForSeconds(.25f);
            Assert.IsTrue(ppe.LeftUnderWater && ppe.RightUnderWater);
            Assert.IsTrue(Object.FindFirstObjectByType<PPEOutletStatus>().IsVisible);
            yield return new WaitForSeconds(1.05f);
            Assert.AreEqual(PPELessonStep.TurnOffTap,ppe.CurrentStep);
        }
        private void PlaceHand(bool left,Vector3 world)
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var field=typeof(XRInteractionSimulator).GetField(left?"m_LeftControllerState":"m_RightControllerState",flags);
            var state=(XRSimulatedControllerState)field.GetValue(simulator);
            state.devicePosition=GameObject.Find(left?"Left Controller":"Right Controller").transform.parent.InverseTransformPoint(world);
            state.deviceRotation=Quaternion.identity; state.isTracked=true; state.trackingState=3;
            field.SetValue(simulator,state);
        }

        [UnityTest] public IEnumerator MouseLookSurvivesAndCornerBlocksDiagonalMovement()
        {
            Spawn(new Vector3(0, 1.65f, 0), Vector3.forward);
            simulator.mouseRotationDeltaInput.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            simulator.toggleMouseInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
            simulator.toggleMouseInput.manualPerformed = true;
            var before = origin.Camera.transform.rotation;
            simulator.mouseRotationDeltaInput.manualValue = new Vector2(1f, 0);
            yield return new WaitForSeconds(.2f);
            simulator.mouseRotationDeltaInput.manualValue = Vector2.zero;
            simulator.toggleMouseInput.manualPerformed = false;
            Assert.Greater(Quaternion.Angle(before, origin.Camera.transform.rotation), 1f, "Mouse-look must still change the headset pose; allow quaternion rounding and frame-count variation.");
            Spawn(new Vector3(5.6f, 1.65f, 4f), new Vector3(1, 0, 1).normalized);
            Input(0, 1); yield return new WaitForSeconds(1.3f); Input(0, 0);
            Assert.Less(origin.Camera.transform.position.x, 5.91f);
            Assert.Less(origin.Camera.transform.position.z, 4.41f);
        }

        [UnityTest] public IEnumerator BoundaryWarningDoesNotMoveTrackedHead()
        {
            var probe = new GameObject("Independent boundary head");
            var overlay = new GameObject("Independent boundary fade"); var group = overlay.AddComponent<CanvasGroup>();
            var warning = probe.AddComponent<HeadBoundaryWarning>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(HeadBoundaryWarning).GetField("head", flags).SetValue(warning, probe.transform);
            typeof(HeadBoundaryWarning).GetField("fade", flags).SetValue(warning, group);
            typeof(HeadBoundaryWarning).GetField("solidLayers", flags).SetValue(warning, (LayerMask)(1 << LayerMask.NameToLayer("LabWorldSolid")));
            try
            {
                var position = new Vector3(5.95f, 1.65f, 0); probe.transform.position = position;
                yield return new WaitForSeconds(.2f);
                Assert.IsTrue(warning.IsPenetrating); Assert.Greater(group.alpha, .5f);
                Assert.AreEqual(position, probe.transform.position);
                probe.transform.position = new Vector3(0, 1.65f, 0);
                yield return new WaitForSeconds(.3f);
                Assert.IsFalse(warning.IsPenetrating); Assert.AreEqual(0f, group.alpha, .01f);
            }
            finally { Object.DestroyImmediate(probe); Object.DestroyImmediate(overlay); }
        }
    }
}
