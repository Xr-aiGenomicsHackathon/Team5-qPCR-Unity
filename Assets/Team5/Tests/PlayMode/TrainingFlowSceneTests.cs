using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace Team5.qPCR.PlayModeTests
{
    [Category("CurrentTrainingRoute")]
    public sealed class TrainingFlowSceneTests
    {
        private static void Set(object obj,string field,object value)=>obj.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);
        private static T Get<T>(object obj,string field)=>(T)obj.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
        private XRSimulatedController leftDevice;
        private XRSimulatedController rightDevice;
        [UnitySetUp] public IEnumerator Load()
        {
            // These tests exercise controller-driven PPE. Supply tracked input explicitly;
            // the simulator auto-loader intentionally does not run inside Unity Test Runner.
            leftDevice=InputSystem.AddDevice<XRSimulatedController>();rightDevice=InputSystem.AddDevice<XRSimulatedController>();
            InputSystem.SetDeviceUsage(leftDevice,CommonUsages.LeftHand);InputSystem.SetDeviceUsage(rightDevice,CommonUsages.RightHand);
            var state=new XRSimulatedControllerState{isTracked=true,trackingState=3,deviceRotation=Quaternion.identity};
            InputSystem.QueueStateEvent(leftDevice,state);InputSystem.QueueStateEvent(rightDevice,state);InputSystem.Update();
            yield return SceneManager.LoadSceneAsync("DemoScene");yield return null;yield return null;
        }
        [TearDown] public void RemoveDevices()
        {
            if(leftDevice!=null&&leftDevice.added)InputSystem.RemoveDevice(leftDevice);
            if(rightDevice!=null&&rightDevice.added)InputSystem.RemoveDevice(rightDevice);
        }
        [Test] public void SceneGatesPcrAndHasOneRigAndInputSystem()
        {
            var lesson=Object.FindFirstObjectByType<QpcrLessonController>();lesson.BeginLesson();
            Assert.IsFalse(lesson.IsLessonActive);Assert.IsFalse(lesson.CanBeginLesson);
            Assert.AreEqual(1,Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1,Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1,Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length);
            Assert.IsTrue(Object.FindFirstObjectByType<PPELessonController>().IsLessonActive);
        }
        [Test] public void PpeWiringIncludesBothHandsAndRealWater()
        {
            var probes=Object.FindObjectsByType<PPEHandProbe>(FindObjectsSortMode.None);
            Assert.AreEqual(1,probes.Count(p=>p.Side==PPEHandSide.Left));Assert.AreEqual(1,probes.Count(p=>p.Side==PPEHandSide.Right));
            var tap=Object.FindFirstObjectByType<TapController>();Assert.NotNull(Get<ParticleSystem>(tap,"waterParticles"));Assert.NotNull(Get<AudioSource>(tap,"waterAudio").clip);
            Assert.AreEqual(2,Object.FindObjectsByType<GlovePickupController>(FindObjectsSortMode.None).Length);
            var ppe=Object.FindFirstObjectByType<PPELessonController>();Assert.AreEqual(20f,Get<float>(ppe,"rubHandsSeconds"));
            Assert.IsFalse(ppe.TryWearGlove(PPEHandSide.Left));
        }
        [Test] public void WhyAndParameterSelectionAreSilentAndReplayRemainsInstruction()
        {
            var ui=Object.FindFirstObjectByType<QpcrLessonUI>(FindObjectsInactive.Include);
            var narration=Object.FindFirstObjectByType<NarrationController>();
            narration.PlayInstruction("Instruction_Qpcr_InspectPlateId","Check plate ID","Check the plate ID.");
            ui.ToggleWhy();Assert.AreEqual("Instruction_Qpcr_InspectPlateId",narration.CurrentKey);
            ui.ShowParameterExplanation(QpcrProtocolParameter.ReactionVolume);
            Assert.AreEqual("Instruction_Qpcr_InspectPlateId",narration.CurrentKey);
            ui.ReadExplanation();Assert.AreEqual("Parameter_ReactionVolume",narration.CurrentKey);
            narration.ReplayInstruction();Assert.AreEqual("Instruction_Qpcr_InspectPlateId",narration.CurrentKey);
            narration.SetMuted(true);narration.SetMuted(false);Assert.IsFalse(narration.IsSpeaking);
        }
        [Test] public void EmptyVideoSlotNeverOpensOrChangesTimeScale()
        {
            var video=Object.FindFirstObjectByType<LessonVideoController>();
            Assert.IsFalse(video.HasVideo);Assert.IsFalse(video.IsPanelOpen);Assert.IsTrue(video.IsUserMuted);
            video.Open();video.TogglePlayback();video.Replay();video.Close();
            Assert.IsFalse(video.IsPanelOpen);Assert.AreEqual(1f,Time.timeScale);
            var player=Get<VideoPlayer>(video,"player");Assert.IsFalse(player.playOnAwake);Assert.IsFalse(player.isLooping);
            var texture=Get<RenderTexture>(video,"videoTexture");Assert.AreEqual(1280,texture.width);Assert.AreEqual(720,texture.height);Assert.AreEqual(0,texture.depth);
        }
        [Test] public void ReachChecksUseActualControllerRatherThanRayEndpoint()
        {
            var target=Object.FindFirstObjectByType<TapController>().GetComponent<XRSimpleInteractable>();var filter=target.GetComponent<NearbySelectionFilter>();
            var origin=new GameObject("Test controller");var child=new GameObject("Test direct interactor");child.transform.SetParent(origin.transform);
            var hand=child.AddComponent<XRDirectInteractor>();Set(filter,"leftController",origin.transform);Set(filter,"rightController",null);
            try
            {
                origin.transform.position=target.transform.position+Vector3.right*2;
                Assert.IsFalse(filter.Process(hand,target));
                origin.transform.position=target.transform.position;
                Assert.IsTrue(filter.Process(hand,target));
            }
            finally{Object.Destroy(origin);}
        }
        [UnityTest] public IEnumerator FullPpeUsesMotionThenUnblocksPcr()
        {
            var ppe=Object.FindFirstObjectByType<PPELessonController>();
            ppe.ConfigureMode(PPELessonMode.FullTraining); ppe.BeginLesson();
            Set(ppe,"wetHandsSeconds",.05f);Set(ppe,"rinseHandsSeconds",.05f);Set(ppe,"dryHandsSeconds",.05f);
            Assert.IsTrue(ppe.TryWearCoat());Assert.IsTrue(ppe.TrySetTap(true));
            ppe.SetHandUnderWater(PPEHandSide.Left,true);ppe.SetHandUnderWater(PPEHandSide.Right,true);
            yield return new WaitForSeconds(.1f);Assert.AreEqual(PPELessonStep.UseSoap,ppe.CurrentStep);Assert.IsTrue(ppe.TryUseSoap());
            var left=new GameObject("Synthetic left hand pose");var right=new GameObject("Synthetic right hand pose");
            Set(ppe,"leftHand",left.transform);Set(ppe,"rightHand",right.transform);
            var stoppedProgress=ppe.StepProgress;yield return new WaitForSeconds(.1f);Assert.AreEqual(stoppedProgress,ppe.StepProgress);
            var deadline=Time.realtimeSinceStartup+23;
            while(ppe.CurrentStep==PPELessonStep.RubHands&&Time.realtimeSinceStartup<deadline)
            {left.transform.position=new Vector3(Mathf.Sin(Time.time*18)*.06f,1,0);right.transform.position=new Vector3(0,1,0);yield return null;}
            Assert.AreEqual(PPELessonStep.RinseHands,ppe.CurrentStep);
            yield return new WaitForSeconds(.12f);Assert.AreEqual(PPELessonStep.TurnOffTap,ppe.CurrentStep);Assert.IsTrue(ppe.TrySetTap(false));
            ppe.SetHandInDryer(PPEHandSide.Left,true);ppe.SetHandInDryer(PPEHandSide.Right,true);yield return new WaitForSeconds(.1f);
            Assert.IsTrue(ppe.TryWearGlove(PPEHandSide.Left));Assert.IsTrue(ppe.TryWearGlove(PPEHandSide.Right));Assert.IsTrue(ppe.IsComplete);
            Assert.IsTrue(Object.FindFirstObjectByType<QpcrLessonController>().CanBeginLesson);
            var session=Object.FindFirstObjectByType<LabSessionCoordinator>();session.ContinueToQpcr();
            Object.FindFirstObjectByType<QpcrLessonUI>().BeginLesson();
            Assert.AreEqual(QpcrLessonState.InspectPlateId,Object.FindFirstObjectByType<QpcrLessonController>().CurrentState);
            Object.Destroy(left);Object.Destroy(right);
        }
        [Test] public void GlovePickupsRejectWrongHandAndChangeEachHandIndependently()
        {
            var ppe=Object.FindFirstObjectByType<PPELessonController>();
            typeof(PPELessonController).GetMethod("SetStep",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ppe,new object[]{PPELessonStep.WearLeftGlove,true});
            var leftRoot=GameObject.Find("Left Controller").transform;var rightRoot=GameObject.Find("Right Controller").transform;
            var left=new GameObject("Synthetic left select");left.transform.SetParent(leftRoot);var leftHand=left.AddComponent<XRDirectInteractor>();
            var right=new GameObject("Synthetic right select");right.transform.SetParent(rightRoot);var rightHand=right.AddComponent<XRDirectInteractor>();
            try
            {
                var appearance=Object.FindFirstObjectByType<HandAppearanceController>();var leftRenderer=Get<Renderer[]>(appearance,"leftHandRenderers").First();var rightRenderer=Get<Renderer[]>(appearance,"rightHandRenderers").First();
                var bareRight=rightRenderer.sharedMaterial;var glove=Get<Material>(appearance,"blueGloveMaterial");
                var pickups=Object.FindObjectsByType<GlovePickupController>(FindObjectsSortMode.None);var leftPickup=pickups.Single(p=>Get<PPEHandSide>(p,"gloveSide")==PPEHandSide.Left);
                var rightPickup=pickups.Single(p=>Get<PPEHandSide>(p,"gloveSide")==PPEHandSide.Right);
                var interactable=leftPickup.GetComponent<XRSimpleInteractable>();
                interactable.selectEntered.Invoke(new UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs{interactorObject=rightHand,interactableObject=interactable});
                Assert.AreEqual(PPELessonStep.WearLeftGlove,ppe.CurrentStep);
                interactable.selectEntered.Invoke(new UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs{interactorObject=leftHand,interactableObject=interactable});
                Assert.AreEqual(PPELessonStep.WearRightGlove,ppe.CurrentStep);Assert.AreEqual(glove,leftRenderer.sharedMaterial);Assert.AreEqual(bareRight,rightRenderer.sharedMaterial);
                var rightInteractable=rightPickup.GetComponent<XRSimpleInteractable>();
                rightInteractable.selectEntered.Invoke(new UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs{interactorObject=rightHand,interactableObject=rightInteractable});
                Assert.IsTrue(ppe.IsComplete);Assert.AreEqual(glove,rightRenderer.sharedMaterial);
                ppe.BeginLesson();Assert.AreEqual(bareRight,rightRenderer.sharedMaterial);Assert.IsFalse(ppe.IsComplete);
            }
            finally{Object.DestroyImmediate(left);Object.DestroyImmediate(right);}
        }
        [UnityTest] public IEnumerator ReturningToLobbyAndReenteringStartsFreshPpe()
        {
            TrainingSession.TryBegin("First learner");
            Object.FindFirstObjectByType<LabSessionCoordinator>().ReturnToLobby();
            var deadline=Time.realtimeSinceStartup+10;
            while(SceneManager.GetActiveScene().name!="LobbyScene"&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;yield return null;
            Assert.AreEqual("LobbyScene",SceneManager.GetActiveScene().name);
            var lobby=Object.FindFirstObjectByType<LobbyController>();lobby.AppendCharacter("Second learner");lobby.ConfirmNickname();lobby.EnterLaboratory();
            deadline=Time.realtimeSinceStartup+10;
            while(SceneManager.GetActiveScene().name!="DemoScene"&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;yield return null;
            Assert.AreEqual("Second learner",TrainingSession.Nickname);Assert.IsFalse(TrainingSession.PpeComplete);
            Assert.AreEqual(PPELessonStep.WearLabCoat,Object.FindFirstObjectByType<PPELessonController>().CurrentStep);
            Assert.AreEqual(1,Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1,Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length);
        }
    }
}
