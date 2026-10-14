using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Team5.qPCR.PlayModeTests
{
    public sealed class DemoSceneQpcrLessonTests
    {
        [UnitySetUp]
        public IEnumerator LoadDemoScene()
        {
            yield return SceneManager.LoadSceneAsync("DemoScene", LoadSceneMode.Single);
            yield return null;
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
            Assert.AreEqual(QpcrLessonState.Complete, lesson.CurrentState);
        }
    }
}
