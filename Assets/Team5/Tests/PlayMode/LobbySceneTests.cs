using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace Team5.qPCR.PlayModeTests
{
    [Category("CurrentTrainingRoute")]
    public sealed class LobbySceneTests
    {
        [UnitySetUp]public IEnumerator Load(){yield return SceneManager.LoadSceneAsync("LobbyScene");yield return null;}
        [Test] public void LobbyHasOneRigAndWorldSpaceKeyboard()
        {
            Assert.AreEqual(1,Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1,Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1,Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length);
            var keyboard=GameObject.Find("Nickname_Entry");Assert.NotNull(keyboard);
            Assert.AreEqual(36,keyboard.GetComponentsInChildren<UnityEngine.UI.Button>(true).Count(b=>b.name.StartsWith("Key_")));
            var canvas=GameObject.Find("Lobby_Welcome_Canvas").GetComponent<Canvas>();Assert.AreEqual(RenderMode.WorldSpace,canvas.renderMode);Assert.NotNull(canvas.worldCamera);
        }
        [Test] public void NicknameEnablesEntryWithoutPractice()
        {
            var lobby=Object.FindFirstObjectByType<LobbyController>();
            lobby.AppendCharacter("Joshua");lobby.ConfirmNickname();
            var button=(UnityEngine.UI.Button)typeof(LobbyController).GetField("enterButton",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(lobby);
            Assert.IsTrue(button.gameObject.activeInHierarchy);
            Assert.IsTrue(button.interactable,"A valid nickname must enable Enter Laboratory without a hidden practice prerequisite.");
        }
        [Test] public void BlankNameCannotEnterAndNicknameIsLimited()
        {
            var lobby=Object.FindFirstObjectByType<LobbyController>();lobby.ConfirmNickname();lobby.EnterLaboratory();
            Assert.IsFalse(lobby.CanEnterLaboratory);Assert.AreEqual("LobbyScene",SceneManager.GetActiveScene().name);
            for(var i=0;i<28;i++)lobby.AppendCharacter("J");
            var text=(TMPro.TMP_Text)typeof(LobbyController).GetField("nicknameText",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(lobby);
            Assert.AreEqual(24,text.text.Length);
            lobby.ClearName();Assert.IsFalse(lobby.CanEnterLaboratory);
        }
        [UnityTest] public IEnumerator EnterButtonLoadsPpeWithoutPracticeOrDuplicateRig()
        {
            var lobby=Object.FindFirstObjectByType<LobbyController>();lobby.AppendCharacter("Joshua");lobby.ConfirmNickname();
            var button=(UnityEngine.UI.Button)typeof(LobbyController).GetField("enterButton",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(lobby);
            var click=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
            UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,click,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            lobby.EnterLaboratory();
            var deadline=Time.realtimeSinceStartup+10;
            while(SceneManager.GetActiveScene().name!="DemoScene"&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;yield return null;
            Assert.AreEqual("DemoScene",SceneManager.GetActiveScene().name);Assert.AreEqual("Joshua",TrainingSession.Nickname);
            Assert.IsTrue(Object.FindFirstObjectByType<PPELessonController>().IsLessonActive);
            Assert.AreEqual(1,Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1,Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsSortMode.None).Length);
        }
        [Test] public void InvalidSceneReportsFailureAndAllowsRetry()
        {
            var lobby=Object.FindFirstObjectByType<LobbyController>();lobby.AppendCharacter("Joshua");lobby.ConfirmNickname();
            var fade=Object.FindFirstObjectByType<TrainingSceneFade>();string error=null;fade.LoadFailed+=message=>error=message;
            Assert.IsFalse(fade.TryLoad("MissingTeachingScene"));Assert.IsNotEmpty(error);Assert.IsFalse(fade.IsLoading);Assert.IsTrue(lobby.CanEnterLaboratory);
            Assert.AreEqual(0f,fade.GetComponent<CanvasGroup>().alpha);
        }
        [Test] public void SimulatorUiIsHiddenButSimulationRemainsAvailable()
        {
            var settings=Object.FindFirstObjectByType<TrainingAudioSettings>();
            var simulator=(UnityEngine.UI.Toggle)typeof(TrainingAudioSettings).GetField("simulator",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(settings);
            Assert.IsFalse(simulator.gameObject.activeSelf);
            Assert.IsTrue((bool)typeof(TrainingAudioSettings).GetField("useSimulatorInEditor",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(settings));
            Assert.IsFalse(Resources.FindObjectsOfTypeAll<GameObject>().Any(go=>go.scene.IsValid()&&go.name=="Practice_Select"&&go.activeInHierarchy));
        }
        [UnityTest] public IEnumerator SimulatorReevaluatesTrackedControllersAfterSceneStartup()
        {
            var left=InputSystem.AddDevice<XRSimulatedController>();
            var right=InputSystem.AddDevice<XRSimulatedController>();
            try
            {
                InputSystem.SetDeviceUsage(left,CommonUsages.LeftHand);InputSystem.SetDeviceUsage(right,CommonUsages.RightHand);
                var state=new XRSimulatedControllerState {isTracked=true,trackingState=3,deviceRotation=Quaternion.identity};
                InputSystem.QueueStateEvent(left,state);InputSystem.QueueStateEvent(right,state);InputSystem.Update();
                yield return null;
                var modality=Object.FindFirstObjectByType<XRInputModalityManager>();
                modality.leftController.SetActive(false);modality.rightController.SetActive(false);
                Object.FindFirstObjectByType<TrainingAudioSettings>().SetSimulator(true);
                yield return null;yield return null;yield return null;
                Assert.IsTrue(modality.leftController.activeInHierarchy);
                Assert.IsTrue(modality.rightController.activeInHierarchy);
            }
            finally {InputSystem.RemoveDevice(left);InputSystem.RemoveDevice(right);}
        }
    }
}
