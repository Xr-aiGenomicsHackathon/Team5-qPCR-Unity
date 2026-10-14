using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace Team5.qPCR.PlayModeTests
{
    public sealed class LabHoverLabelTests
    {
        private GameObject fixture;
        private LabHoverLabelController controller;
        private LabLabelTarget target;
        private readonly Vector3 origin = new Vector3(100, 1, 100);
        private Ray Aim => new Ray(origin, Vector3.forward);
        [SetUp] public void Create()
        {
            fixture = new GameObject("Label test fixture");
            controller = fixture.AddComponent<LabHoverLabelController>();controller.enabled=false;
            var model=GameObject.CreatePrimitive(PrimitiveType.Cube);model.transform.SetParent(fixture.transform);
            model.transform.position=origin+Vector3.forward;model.transform.localScale=Vector3.one*.2f;
            target=model.AddComponent<LabLabelTarget>();target.Configure("Test instrument","An identified test fixture.");
            Physics.SyncTransforms();
        }
        [TearDown] public void Clean()=>Object.DestroyImmediate(fixture);
        [Test] public void LabelWaitsForDwellAndBothControllersCanRead()
        {
            controller.UpdatePointer(0,Aim,false,.19f);Assert.IsNull(controller.VisibleTarget(0));
            controller.UpdatePointer(0,Aim,false,.02f);Assert.AreEqual(target,controller.VisibleTarget(0));
            controller.UpdatePointer(1,Aim,false,.21f);Assert.AreEqual(target,controller.VisibleTarget(1));
        }
        [Test] public void UiPriorityAndPointingAwayHideImmediately()
        {
            controller.UpdatePointer(0,Aim,false,.21f);controller.UpdatePointer(0,Aim,true,.21f);Assert.IsNull(controller.VisibleTarget(0));
            controller.UpdatePointer(0,Aim,false,.21f);controller.UpdatePointer(0,new Ray(origin,Vector3.right),false,.21f);Assert.IsNull(controller.VisibleTarget(0));
        }
        [Test] public void WallsOccludeAndThreeMetresIsTheLimit()
        {
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(fixture.transform);wall.transform.position=origin+Vector3.forward*.5f;wall.transform.localScale=Vector3.one*.1f;
            Physics.SyncTransforms();controller.UpdatePointer(0,Aim,false,.3f);Assert.IsNull(controller.VisibleTarget(0));
            Object.DestroyImmediate(wall);target.transform.position=origin+Vector3.forward*3.3f;Physics.SyncTransforms();
            controller.UpdatePointer(0,Aim,false,.3f);Assert.IsNull(controller.VisibleTarget(0));
        }
        [Test] public void DisabledObjectsAndTargetsCannotLeaveStaleLabels()
        {
            controller.UpdatePointer(0,Aim,false,.3f);target.enabled=false;controller.UpdatePointer(0,Aim,false,.3f);Assert.IsNull(controller.VisibleTarget(0));
            target.enabled=true;controller.UpdatePointer(0,Aim,false,.3f);target.gameObject.SetActive(false);controller.UpdatePointer(0,Aim,false,.3f);Assert.IsNull(controller.VisibleTarget(0));
        }
        [UnityTest] public IEnumerator SceneLabelsAreNonInteractiveAndUseExistingAimPoses()
        {
            yield return SceneManager.LoadSceneAsync("DemoScene");yield return null;
            var installed=Object.FindObjectsByType<LabHoverLabelController>(FindObjectsSortMode.None).Single(c=>c!=controller);
            var targets=Object.FindObjectsByType<LabLabelTarget>(FindObjectsSortMode.None);
            Assert.Greater(targets.Length,50);Assert.IsTrue(targets.Any(t=>t.DisplayName=="A1 reference corner"));Assert.IsTrue(targets.Any(t=>t.DisplayName=="Optical plate seal"));
            foreach(var proxy in Object.FindObjectsByType<LabLabelProxy>(FindObjectsSortMode.None))
            {
                Assert.AreEqual("LabLabelsOnly",LayerMask.LayerToName(proxy.gameObject.layer));Assert.IsTrue(proxy.GetComponent<Collider>().isTrigger);
                Assert.IsNull(proxy.GetComponent<XRBaseInteractable>());Assert.IsTrue(Physics.GetIgnoreLayerCollision(proxy.gameObject.layer,0));
            }
            foreach(var caster in Object.FindObjectsByType<CurveInteractionCaster>(FindObjectsSortMode.None))
                Assert.AreEqual(0,caster.raycastMask.value & (1<<LayerMask.NameToLayer("LabLabelsOnly")));
            var views=(LabLabelView[])typeof(LabHoverLabelController).GetField("views",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(installed);
            Assert.AreEqual(2,views.Length);
            foreach(var view in views)
            {Assert.IsFalse(view.Panel.GetComponent<CanvasGroup>().blocksRaycasts);Assert.IsTrue(view.Panel.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).All(g=>!g.raycastTarget));}
            var narration=Object.FindFirstObjectByType<NarrationController>();var cue=narration.CurrentKey;
            installed.UpdatePointer(0,new Ray(new Vector3(0,2,0),Vector3.down),false,.3f);Assert.AreEqual(cue,narration.CurrentKey);
        }
    }
}
