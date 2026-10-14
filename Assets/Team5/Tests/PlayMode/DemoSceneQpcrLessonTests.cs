using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace Team5.qPCR.PlayModeTests
{
    public sealed class DemoSceneQpcrLessonTests
    {
        [UnitySetUp]
        public IEnumerator LoadDemoScene()
        {
            yield return SceneManager.LoadSceneAsync("DemoScene", LoadSceneMode.Single);
            yield return null;
            // Isolate the pre-existing station regressions from the new PPE route.
            // TrainingFlowSceneTests separately exercise the real prerequisite and reach gates.
            var session = Object.FindFirstObjectByType<LabSessionCoordinator>();
            if (session != null) { session.StopAllCoroutines(); session.enabled = false; }
            Object.FindFirstObjectByType<QpcrLessonController>(FindObjectsInactive.Include).ConfigurePpePrerequisite(null);
            foreach(var filter in Object.FindObjectsByType<NearbySelectionFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None)) filter.enabled=false;
            var card=Resources.FindObjectsOfTypeAll<GameObject>().First(go=>go.scene.IsValid()&&go.name=="Plate_Instruction_Canvas");card.SetActive(true);
        }

        [Test]
        public void SceneContainsExactlyOneVrFirstLessonArchitecture()
        {
            Assert.AreEqual(1, Object.FindObjectsByType<QpcrLessonController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<PlateInspectionController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
            Assert.AreEqual(4, Object.FindObjectsByType<PlateInspectionHotspot>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<QpcrPowerController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<QpcrDrawerController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<QpcrPlateSocket>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<QpcrTouchscreenController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<QpcrGuidancePointer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);

            var sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>().Where(item => item.scene.IsValid()).ToArray();
            Assert.AreEqual(1, sceneObjects.Count(item => item.name == "Plate_Instruction_Canvas"));
            Assert.AreEqual(1, sceneObjects.Count(item => item.name == "QPCR_TOUCHSCREEN_V2"));
            Assert.AreEqual(1, sceneObjects.Count(item => item.name == "A1_PLATE_SOCKET_V2"));
            Assert.AreEqual(1, sceneObjects.Count(item => item.name == "Confirm_Visual_Check"));
            Assert.AreEqual(1, sceneObjects.Count(item => item.name == "QPCR_GUIDANCE_POINTER"));
        }

        [UnityTest]
        public IEnumerator VisualPlateChecksUsePointerAndConfirmInsteadOfTouchingLabels()
        {
            var lesson = Object.FindFirstObjectByType<QpcrLessonController>(FindObjectsInactive.Include);
            var inspection = Object.FindFirstObjectByType<PlateInspectionController>(FindObjectsInactive.Include);
            var pointer = Object.FindFirstObjectByType<QpcrGuidancePointer>(FindObjectsInactive.Include);
            lesson.BeginLesson();
            yield return null;

            Assert.AreEqual(QpcrLessonState.InspectPlateId, lesson.CurrentState);
            var hotspots = Object.FindObjectsByType<PlateInspectionHotspot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(0, hotspots.Count(item => item.GetComponent<Collider>().enabled),
                "Visual observations should be confirmed on the lesson card, not by touching labels.");
            Assert.AreEqual(PlateInspectionKind.PlateId,
                pointer.ActiveTarget.GetComponent<PlateInspectionHotspot>().Kind);

            Assert.IsTrue(inspection.ConfirmCurrentVisualCheck());
            Assert.AreEqual(QpcrLessonState.InspectA1Marker, lesson.CurrentState);
            Assert.IsTrue(inspection.ConfirmCurrentVisualCheck());
            Assert.AreEqual(QpcrLessonState.InspectOpticalSeal, lesson.CurrentState);
            Assert.IsTrue(inspection.ConfirmCurrentVisualCheck());
            Assert.AreEqual(QpcrLessonState.InspectBubbles, lesson.CurrentState);
            Assert.IsTrue(inspection.ConfirmCurrentVisualCheck());
            Assert.AreEqual(QpcrLessonState.PowerOn, lesson.CurrentState,
                "A plate already resting at its handoff station should not require an artificial return action.");
        }

        [Test]
        public void PlateFoundationMatchesTheMetricSpecification()
        {
            var grab = Object.FindFirstObjectByType<PlateInspectionController>(FindObjectsInactive.Include)
                .GetComponent<XRGrabInteractable>();
            var body = grab.GetComponent<Rigidbody>();
            var collider = grab.GetComponent<BoxCollider>();
            Assert.Less(Vector3.Distance(new Vector3(2.49f,.98f,3.28f), grab.transform.position), .005f);
            Assert.AreEqual(.04f, body.mass);
            Assert.AreEqual(8f, body.linearDamping);
            Assert.AreEqual(8f, body.angularDamping);
            Assert.IsTrue(body.isKinematic);
            Assert.AreEqual(RigidbodyInterpolation.Interpolate, body.interpolation);
            Assert.AreEqual(new Vector3(.13f,.02f,.09f), collider.size);
            Assert.AreEqual(XRBaseInteractable.MovementType.Kinematic, grab.movementType);
            Assert.IsFalse(grab.trackScale);
            Assert.IsFalse(grab.throwOnDetach);
        }

        [UnityTest]
        public IEnumerator ReleasedPlateFallsUnderGravity()
        {
            var lesson = Object.FindFirstObjectByType<QpcrLessonController>();
            var grab = Object.FindFirstObjectByType<PlateInspectionController>().GetComponent<XRGrabInteractable>();
            var manager = Object.FindFirstObjectByType<XRInteractionManager>();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Epoxy_Floor";
            floor.transform.position = new Vector3(100f,-.05f,100f);
            floor.transform.localScale = new Vector3(2f,.1f,2f);
            var handObject = new GameObject("Regression test hand", typeof(SphereCollider), typeof(XRDirectInteractor));
            handObject.GetComponent<SphereCollider>().isTrigger = true;
            var hand = handObject.GetComponent<XRDirectInteractor>();
            hand.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
            hand.selectInput.manualPerformed = true;
            hand.selectInput.manualValue = 1f;
            hand.keepSelectedTargetValid = true;
            handObject.transform.position = grab.transform.position;
            lesson.BeginLesson();
            yield return null;
            manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
            Assert.IsTrue(grab.isSelected, "The release test must exercise the actual XRI selection lifecycle.");
            // Inject a held pose; this test measures release physics, not the
            // simulated controller's pose provider or a grab transformer.
            grab.trackPosition = false;
            grab.trackRotation = false;
            // Keep the free-fall volume clear of the merged laboratory's furniture.
            grab.transform.position = new Vector3(100f,1.2f,100f);
            grab.GetComponent<Rigidbody>().position = grab.transform.position;
            hand.selectInput.manualPerformed = false;
            hand.selectInput.manualValue = 0f;
            manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
            // XRI detach and our release resolver run after the select event.
            yield return null;
            yield return new WaitForFixedUpdate();
            var height = grab.transform.position.y;
            Assert.IsFalse(grab.GetComponent<Rigidbody>().isKinematic, "Release must restore a dynamic Rigidbody.");
            Assert.IsTrue(grab.GetComponent<Rigidbody>().useGravity);
            for(var tick=0;tick<15;tick++)yield return new WaitForFixedUpdate();
            Assert.Less(grab.transform.position.y, height - .1f, "A released plate must fall, not float.");
            for(var tick=0;tick<50;tick++)yield return new WaitForFixedUpdate();
            Assert.IsTrue(grab.GetComponent<QpcrPlateHandling>().IsCompromised, "A floor drop must block reuse of the plate.");
            Object.Destroy(handObject);
            Object.Destroy(floor);
        }

        [UnityTest]
        public IEnumerator AmplificationGraphHasRendererAndProducesVisibleMesh()
        {
            var graph = Object.FindFirstObjectByType<AmplificationGraphView>(FindObjectsInactive.Include);
            Assert.IsNotNull(graph.GetComponent<CanvasRenderer>(), "The graph requires a CanvasRenderer.");
            graph.GetComponentInParent<Canvas>(true).gameObject.SetActive(true);
            for (var t = graph.transform; t != null && t.GetComponent<Canvas>() == null; t = t.parent)
                t.gameObject.SetActive(true);
            graph.SetProgress(.75f);
            Canvas.ForceUpdateCanvases();
            yield return null;
            var mesh = graph.canvasRenderer.GetMesh();
            Assert.Greater(mesh.vertexCount, 100, "The graph must render actual curves and axes.");
        }

        [UnityTest]
        public IEnumerator CompleteGuidedProtocolRunsAllCyclesAndRequiresCorrectControlInterpretation()
        {
            var lesson = Object.FindFirstObjectByType<QpcrLessonController>(FindObjectsInactive.Include);
            var inspection = Object.FindFirstObjectByType<PlateInspectionController>(FindObjectsInactive.Include);
            var touchscreen = Object.FindFirstObjectByType<QpcrTouchscreenController>(FindObjectsInactive.Include);
            var run = Object.FindFirstObjectByType<QpcrRunController>(FindObjectsInactive.Include);
            var results = Object.FindFirstObjectByType<ResultsLessonController>(FindObjectsInactive.Include);

            lesson.BeginLesson();
            for (var index = 0; index < 4; index++)
                Assert.IsTrue(inspection.ConfirmCurrentVisualCheck());

            Assert.IsTrue(lesson.TryComplete(QpcrLessonState.PowerOn));
            Assert.IsTrue(lesson.TryComplete(QpcrLessonState.OpenDrawer));
            Assert.IsTrue(lesson.TryComplete(QpcrLessonState.LoadPlate));
            Assert.IsTrue(lesson.TryComplete(QpcrLessonState.CloseDrawer));
            Assert.AreEqual(QpcrLessonState.ConfigureProtocol, lesson.CurrentState);

            touchscreen.gameObject.SetActive(true);
            foreach (QpcrProtocolParameter parameter in System.Enum.GetValues(typeof(QpcrProtocolParameter)))
            {
                touchscreen.SelectField((int)parameter);
                var value = QpcrProtocolValidator.ExpectedValue(parameter)
                    .ToString("0.##", CultureInfo.InvariantCulture);
                foreach (var character in value)
                    touchscreen.AppendCharacter(character.ToString());
                touchscreen.ConfirmCurrentValue();
            }

            touchscreen.ValidateProtocol();
            Assert.AreEqual(QpcrLessonState.StartRun, lesson.CurrentState);
            Assert.IsTrue(touchscreen.IsProtocolValid);

            typeof(QpcrRunController).GetField("simulatedDurationSeconds",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(run, .2f);
            touchscreen.RequestStartRun();
            var timeout = 3f;
            while (lesson.CurrentState != QpcrLessonState.InterpretResults && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.AreEqual(QpcrLessonState.InterpretResults, lesson.CurrentState);
            Assert.AreEqual(35, run.CurrentCycle);
            results.ConfirmValidControls();
            Assert.AreEqual(QpcrLessonState.InterpretResults, lesson.CurrentState);
            results.ConfirmValidControls();
            results.ConfirmValidControls();
            Assert.AreEqual(QpcrLessonState.Complete, lesson.CurrentState);
        }

        [UnityTest]
        public IEnumerator HomeAndDrawerSnapUseReleasedPoseAndRejectWrongA1()
        {
            var lesson=Object.FindFirstObjectByType<QpcrLessonController>();
            var inspection=Object.FindFirstObjectByType<PlateInspectionController>();
            var handling=inspection.GetComponent<QpcrPlateHandling>();
            var home=handling.transform.parent;
            lesson.BeginLesson();
            handling.transform.position=home.position+new Vector3(.04f,.04f,.02f);
            handling.ResolveReleasedPose();
            Assert.Less(Vector3.Distance(handling.transform.position,home.position),.001f);
            for(var i=0;i<4;i++) inspection.ConfirmCurrentVisualCheck();
            Object.FindFirstObjectByType<QpcrPowerController>().GetComponent<XRSimpleInteractable>()
                .selectEntered.Invoke(new SelectEnterEventArgs());
            GameObject.Find("/40_QPCR_STATION/TEAM5_REAL_TIME_QPCR_INSTRUMENT/PHYSICAL_MACHINE_CONTROLS/Drawer_Open_Button")
                .GetComponent<XRSimpleInteractable>().selectEntered.Invoke(new SelectEnterEventArgs());
            yield return new WaitForSeconds(.8f);
            var socket=Object.FindFirstObjectByType<QpcrPlateSocket>();
            var attach=socket.transform.Find("Plate_Attach");
            handling.transform.SetPositionAndRotation(attach.position,attach.rotation*Quaternion.Euler(0,180,0));
            handling.ResolveReleasedPose();
            Assert.IsFalse(socket.IsPlateSeated);
            Assert.AreEqual(QpcrLessonState.LoadPlate,lesson.CurrentState);
            handling.transform.SetPositionAndRotation(attach.position+Vector3.up*.03f,attach.rotation);
            handling.ResolveReleasedPose();
            Assert.IsTrue(socket.IsPlateSeated);
            Assert.AreEqual(QpcrLessonState.CloseDrawer,lesson.CurrentState);
            Assert.Less(Vector3.Distance(handling.transform.position,attach.position),.001f);
            GameObject.Find("/40_QPCR_STATION/TEAM5_REAL_TIME_QPCR_INSTRUMENT/PHYSICAL_MACHINE_CONTROLS/Drawer_Close_Button")
                .GetComponent<XRSimpleInteractable>().selectEntered.Invoke(new SelectEnterEventArgs());
            yield return new WaitForSeconds(.8f);
            Assert.Less(Vector3.Distance(handling.transform.position,attach.position),.001f);
            Object.FindFirstObjectByType<QpcrLessonUI>().ResetLesson();
            Assert.Less(Vector3.Distance(handling.transform.position,home.position),.001f);
            Assert.IsFalse(socket.IsPlateSeated);
        }
    }
}
