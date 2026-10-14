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

namespace Team5.qPCR.PlayModeTests
{
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
        [Test] public void BlankNameAndIncompletePracticeCannotEnter()
        {
            var lobby=Object.FindFirstObjectByType<LobbyController>();lobby.ConfirmNickname();lobby.EnterLaboratory();
            Assert.IsFalse(lobby.PracticeComplete);Assert.AreEqual("LobbyScene",SceneManager.GetActiveScene().name);
            for(var i=0;i<28;i++)lobby.AppendCharacter("J");
            var text=(TMPro.TMP_Text)typeof(LobbyController).GetField("nicknameText",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(lobby);
            Assert.AreEqual(24,text.text.Length);
            lobby.ClearName();lobby.AppendCharacter("Joshua");lobby.ConfirmNickname();lobby.EnterLaboratory();Assert.AreEqual("LobbyScene",SceneManager.GetActiveScene().name);
        }
        [UnityTest] public IEnumerator PracticeRequiresSelectGrabReleaseAndLoadsPpeWithoutDuplicateRig()
        {
            var lobby=Object.FindFirstObjectByType<LobbyController>();lobby.AppendCharacter("Joshua");lobby.ConfirmNickname();lobby.PracticeSelect();
            var grab=Object.FindFirstObjectByType<XRGrabInteractable>();
            var dummy=new GameObject("Practice test controller");var interactor=dummy.AddComponent<XRDirectInteractor>();
            grab.selectEntered.Invoke(new SelectEnterEventArgs{interactorObject=interactor,interactableObject=grab});
            Assert.IsFalse(lobby.PracticeComplete);
            grab.selectExited.Invoke(new SelectExitEventArgs{interactorObject=interactor,interactableObject=grab});Assert.IsTrue(lobby.PracticeComplete);
            lobby.EnterLaboratory();lobby.EnterLaboratory();
            var deadline=Time.realtimeSinceStartup+10;
            while(SceneManager.GetActiveScene().name!="DemoScene"&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;yield return null;
            Assert.AreEqual("DemoScene",SceneManager.GetActiveScene().name);Assert.AreEqual("Joshua",TrainingSession.Nickname);
            Assert.IsTrue(Object.FindFirstObjectByType<PPELessonController>().IsLessonActive);
            Assert.AreEqual(1,Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1,Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsSortMode.None).Length);
            Object.Destroy(dummy);
        }
    }
}
