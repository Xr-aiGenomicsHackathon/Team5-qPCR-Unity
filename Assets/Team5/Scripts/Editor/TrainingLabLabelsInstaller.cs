#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.UI;
using static Team5.qPCR.Editor.TrainingUiBuilder;

namespace Team5.qPCR.Editor
{
    public static partial class TrainingFlowSceneInstaller
    {
        [MenuItem("Team 5/Training Flow/Install Or Refresh Hover Labels")]
        public static void InstallLabels()
        {
            if(EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != DemoPath)
                throw new InvalidOperationException("Open DemoScene in Edit Mode before refreshing labels.");
            var layer = LabelLayer();
            var roots = new[]{Named("10_LAB_STRUCTURE"),Named("20_LAB_FURNITURE"),Named("30_PPE_AND_SAFETY"),Named("40_QPCR_STATION")};
            var transforms=roots.SelectMany(root=>root.GetComponentsInChildren<Transform>(true)).Where(t=>t.gameObject.activeInHierarchy).OrderBy(Depth).ToArray();
            foreach(var t in transforms)
            {
                if(t.GetComponentInParent<Canvas>()!=null || t.name.StartsWith("Tour_") || t.name.StartsWith("Inspect_") || t.name.Contains("Sound"))continue;
                if(!Describe(t,out var title,out var purpose))continue;
                var previous=t.parent==null?null:t.parent.GetComponentInParent<LabLabelTarget>();
                if(previous!=null&&previous.DisplayName==title)continue;
                Ensure<LabLabelTarget>(t.gameObject).Configure(title,purpose);
            }
            // Cover unknown artist props without inventing their contents or scientific role.
            foreach(var renderer in transforms.SelectMany(t=>t.GetComponents<Renderer>()).Where(r=>r.enabled&&!(r is LineRenderer)&&!(r is ParticleSystemRenderer)))
            {
                if(renderer.GetComponentInParent<LabLabelTarget>()!=null || renderer.GetComponentInParent<Canvas>()!=null || renderer.name.Contains("Pointer") || renderer.name.Contains("Outline"))continue;
                Ensure<LabLabelTarget>(renderer.gameObject).Configure("Laboratory fixture","Part of the modelled laboratory environment; no task action is required.");
            }
            var targets=UnityEngine.Object.FindObjectsByType<LabLabelTarget>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            foreach(var target in targets.Where(t=>t.gameObject.scene==UnityEngine.SceneManagement.SceneManager.GetActiveScene()))
                FitProxy(target,layer);
            var rootObject=Child(Named("50_LESSON_AND_UI"),"LAB_HOVER_LABELS");
            var controller=Ensure<LabHoverLabelController>(rootObject);
            Set(controller,"rig",Named("00_XR_SYSTEMS"));Set(controller,"dwellSeconds",.2f);Set(controller,"maximumDistance",3f);
            var serialized=new SerializedObject(controller);serialized.FindProperty("labelMask").intValue=~(1<<2);serialized.ApplyModifiedPropertiesWithoutUndo();
            var hands=new[]{Named("Left Controller"),Named("Right Controller")};
            Objects(controller,"fallbackAim",hands.Cast<UnityEngine.Object>().ToArray());
            Objects(controller,"pointers",hands.Select(h=>(UnityEngine.Object)h.GetComponentInChildren<NearFarInteractor>(true)).ToArray());
            var views=serialized.FindProperty("views");views.arraySize=2;
            for(var hand=0;hand<2;hand++)
            {
                var panel=Canvas(rootObject.transform,hand==0?"Left_Object_Label":"Right_Object_Label",Vector3.zero,new Vector2(480,190),.0007f);
                var raycaster=panel.GetComponent<TrackedDeviceGraphicRaycaster>();if(raycaster!=null)raycaster.enabled=false;
                var group=Ensure<CanvasGroup>(panel);group.blocksRaycasts=false;group.interactable=false;
                Image(panel.transform,"Background",Vector2.zero,new Vector2(480,190),Paper);
                Image(panel.transform,"Accent",new Vector2(-230,0),new Vector2(6,150),Teal);
                var heading=Text(panel.transform,"Object_Name",new Vector2(0,55),new Vector2(420,55),32,"");
                var purpose=Text(panel.transform,"Object_Purpose",new Vector2(0,-25),new Vector2(420,105),22,"");
                var view=views.GetArrayElementAtIndex(hand);view.FindPropertyRelative("Panel").objectReferenceValue=panel;view.FindPropertyRelative("Title").objectReferenceValue=heading;view.FindPropertyRelative("Purpose").objectReferenceValue=purpose;
                panel.SetActive(false);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach(var caster in Named("00_XR_SYSTEMS").GetComponentsInChildren<CurveInteractionCaster>(true))caster.raycastMask= caster.raycastMask.value & ~(1<<layer);
            foreach(var caster in Named("00_XR_SYSTEMS").GetComponentsInChildren<SphereInteractionCaster>(true))caster.physicsLayerMask=caster.physicsLayerMask.value & ~(1<<layer);
            foreach(var ray in Named("00_XR_SYSTEMS").GetComponentsInChildren<XRRayInteractor>(true))ray.raycastMask=ray.raycastMask.value & ~(1<<layer);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        }
        private static int Depth(Transform t){var depth=0;while(t.parent!=null){depth++;t=t.parent;}return depth;}
        private static int LabelLayer()
        {
            var existing=LayerMask.NameToLayer("LabLabelsOnly");if(existing>=0)return existing;
            var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);var layers=tags.FindProperty("layers");
            for(var index=8;index<32;index++)if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(index).stringValue))
            {layers.GetArrayElementAtIndex(index).stringValue="LabLabelsOnly";tags.ApplyModifiedPropertiesWithoutUndo();return index;}
            throw new InvalidOperationException("A free user physics layer is needed for label-only proxies.");
        }
        private static void FitProxy(LabLabelTarget target,int layer)
        {
            var existing=target.transform.Find("LabelOnlyProxy");
            if(target.GetComponentsInChildren<Collider>(true).Any(c=>c.enabled&&!c.isTrigger&&c.GetComponentInParent<LabLabelTarget>()==target))
            {if(existing!=null)existing.gameObject.SetActive(false);return;}
            var renderers=target.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&!(r is LineRenderer)&&!(r is ParticleSystemRenderer)&&r.GetComponentInParent<Canvas>()==null&&r.GetComponentInParent<LabLabelTarget>()==target).ToArray();
            Bounds bounds;
            if(renderers.Length>0){bounds=BoundsInLabelSpace(renderers[0],target.transform);foreach(var r in renderers.Skip(1))bounds.Encapsulate(BoundsInLabelSpace(r,target.transform));}
            else if(target.GetComponent<Collider>()!=null){var world=target.GetComponent<Collider>().bounds;bounds=new Bounds(target.transform.InverseTransformPoint(world.center),Vector3.zero);for(var corner=0;corner<8;corner++)bounds.Encapsulate(target.transform.InverseTransformPoint(world.center+Vector3.Scale(world.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1))));}
            else return;
            var proxy=Child(target.transform,"LabelOnlyProxy");proxy.SetActive(true);proxy.layer=layer;proxy.transform.localPosition=Vector3.zero;proxy.transform.localRotation=Quaternion.identity;proxy.transform.localScale=Vector3.one;
            var box=Ensure<BoxCollider>(proxy);box.isTrigger=true;box.center=bounds.center;
            box.size=new Vector3(Mathf.Max(.004f,bounds.size.x),Mathf.Max(.004f,bounds.size.y),Mathf.Max(.004f,bounds.size.z));
            Ensure<LabLabelProxy>(proxy).Configure(target);
        }
        private static Bounds BoundsInLabelSpace(Renderer renderer,Transform target)
        {
            var local=renderer.localBounds;var result=new Bounds(target.InverseTransformPoint(renderer.transform.TransformPoint(local.center)),Vector3.zero);
            for(var corner=0;corner<8;corner++)
            {
                var point=local.center+Vector3.Scale(local.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                result.Encapsulate(target.InverseTransformPoint(renderer.transform.TransformPoint(point)));
            }
            return result;
        }
        private static bool Describe(Transform t,out string title,out string purpose)
        {
            title=null;purpose=null;var n=t.name.ToLowerInvariant();
            if(n=="power_button"){title="Power switch";purpose="Turns the qPCR instrument on before loading and configuration.";}
            else if(n=="drawer_open_button"){title="Open compartment";purpose="Opens the instrument drawer so the plate can be loaded.";}
            else if(n=="drawer_close_button"){title="Close compartment";purpose="Closes the drawer after the plate is correctly seated.";}
            else if(n=="start_run_button"){title="Start run";purpose="Starts thermal cycling after the protocol has been validated.";}
            else if(n=="a1_orientation_marker"){title="A1 reference corner";purpose="Identifies the reference corner used to orient the plate.";}
            else if(n=="transparent_optical_seal"){title="Optical plate seal";purpose="Seals the wells while allowing the optical measurement required by the assay.";}
            else if(n.StartsWith("label_plate id")){title="Plate identification";purpose="Links this physical plate to its experiment record.";}
            else if(n=="a1_plate_socket_v2"){title="Plate holder";purpose="Supports the plate in the correct position and orientation.";}
            else if(n=="motorized_plate_drawer"){title="Plate drawer";purpose="Carries the prepared plate into the instrument compartment.";}
            else if(n=="touchscreen_glass"||n=="touchscreen_bezel"){title="qPCR touchscreen";purpose="Use the screen to configure the protocol and inspect the teaching results.";}
            else if(n=="team5_real_time_qpcr_instrument"){title="Real-time PCR instrument";purpose="Cycles temperature and measures fluorescence to monitor amplification.";}
            else if(n=="t4_qpcr_2026_05_prepared_plate"){title="Prepared 96-well PCR plate";purpose="Contains prepared reactions; inspect the ID, A1 corner, seal and wells before loading.";}
            else if(n=="labcoat_grab"||n=="labcoat"){title="Lab coat";purpose="Helps protect clothing and skin from splashes.";}
            else if(n=="left_glove_pickup"||n=="right_glove_pickup"){title=n.StartsWith("left")?"Left glove":"Right glove";purpose="Put on clean gloves after washing and drying your hands.";}
            else if(n.Contains("glove_pack")||n.Contains("open_glove_box")){title="Glove pack";purpose="Stores gloves for use after hand hygiene.";}
            else if(n.StartsWith("glove_rubber")){title="Lab glove";purpose="Helps protect hands and reduce contact contamination.";}
            else if(n=="tap_handle"){title="Tap handle";purpose="Controls running water for handwashing.";}
            else if(n=="soap_dispenser"){title="Soap dispenser";purpose="Supplies soap before the twenty-second scrub step.";}
            else if(n.Contains("hand_dryer")){title="Hand dryer";purpose="Represents drying clean hands before putting on gloves.";}
            else if(n=="sink_and_tap"){title="Handwashing sink";purpose="Provides a place to wash and rinse bare hands.";}
            else if(n.Contains("ethanol_spray")){title="Surface-cleaning bottle";purpose="Represents a cleaning supply; follow the laboratory's approved cleaning procedure.";}
            else if(n.Contains("nose_mask")){title="Face mask";purpose="A displayed PPE item; follow the protection requirements of your laboratory.";}
            else if(n=="microscope"||n.StartsWith("microscope (")){title="Microscope";purpose="Magnifies specimens; it is background equipment in this qPCR lesson.";}
            else if(n=="vortex_mixer"){title="Vortex mixer";purpose="Mixes small liquid samples; it is not required during this prepared-plate lesson.";}
            else if(n=="plate_centrifuge"){title="Plate centrifuge";purpose="Can collect liquid at the bottoms of wells when required by the approved protocol.";}
            else if(n=="fume_hood"){title="Fume hood model";purpose="Represents ventilated equipment; it is not a biosafety cabinet or an active simulation.";}
            else if(n.Contains("multichannel pipettes")){title="Multichannel pipettes";purpose="Transfer measured liquid volumes across several wells at once.";}
            else if(n.StartsWith("edited pipettes")||n.StartsWith("pipette_" )&&char.IsDigit(n.Last())){title="Pipettes";purpose="Transfer measured liquid volumes; sample preparation occurs before this lesson.";}
            else if(n.Contains("pipette_rack")||n=="pipette_stand"){title="Pipette stand";purpose="Stores pipettes between uses.";}
            else if(n.Contains("tube_and_rack")||n=="tubes_and_racks"||n.StartsWith("tube_rack")){title="Tube rack";purpose="Holds sample tubes upright.";}
            else if(n=="cold_block"||n.StartsWith("cold_block_artist")){title="Cold block";purpose="Supports tubes during temperature-controlled preparation when specified by the protocol.";}
            else if(n.Contains("trash")&&!n.Equals("trash")){title="Waste container";purpose="Follow local waste labels and disposal procedures; colour alone does not define the waste category.";}
            else if(n.Contains("fire_extinguisher")){title="Fire extinguisher";purpose="Emergency safety equipment; use only according to local training and procedures.";}
            else if(n.Contains("fridge")||n.Contains("freezer")){title="Laboratory refrigerator model";purpose="Represents storage equipment; no storage temperature is simulated.";}
            else if(n=="cpu"){title="Computer unit";purpose="Represents the laboratory's computer hardware.";}
            else if(n=="printer"){title="Printer";purpose="Represents equipment for printing laboratory records.";}
            else if(n=="computer_monitor"||n=="computer_screen"){title="Computer monitor";purpose="Displays information; qPCR controls in this lesson are on the instrument touchscreen.";}
            else if(n=="keyboard"){title="Keyboard";purpose="Represents computer input equipment.";}
            else if(n.Contains("office_chair")||n=="stool"||n.StartsWith("stool (")){title="Laboratory seat";purpose="Provides seating beside a laboratory workstation.";}
            else if(n.Contains("glass table")){title="Laboratory table";purpose="Provides a work surface for laboratory equipment.";}
            else if(n.Contains("bench_worktop")){title="Laboratory worktop";purpose="Supports equipment and provides an organised work surface.";}
            else if(n.StartsWith("refined_")&&n.Contains("cabinet")||n.StartsWith("lab_hanging_cabinet_")){title="Storage cabinet";purpose="Stores laboratory supplies and displayed containers.";}
            else if(n.StartsWith("cylinder")&&t.parent!=null&&t.parent.name.ToLowerInvariant().Contains("reagents_inside")){title="Displayed container";purpose="A background supply container; its contents are not identified in this simulation.";}
            else if(n.Contains("door_frame")||n.Contains("door_glass")){title="Laboratory door";purpose="Provides the entrance to the room.";}
            else if(n=="door_handle"){title="Door handle";purpose="The handle of the laboratory entrance.";}
            else if(n.Contains("window")){title="Laboratory window";purpose="Part of the room's structure and lighting design.";}
            else if(n.Contains("wall")&&!n.Contains("shell")){title="Laboratory wall";purpose="Defines the room boundary.";}
            else if(n=="epoxy_floor"){title="Laboratory floor";purpose="Provides a stable walking surface inside the room.";}
            else if(n=="removable_ceiling"){title="Laboratory ceiling";purpose="Forms the overhead room surface.";}
            else if(n.StartsWith("led_panel")){title="Ceiling light";purpose="Illuminates the room and work surfaces.";}
            return title!=null;
        }
    }
}
#endif
