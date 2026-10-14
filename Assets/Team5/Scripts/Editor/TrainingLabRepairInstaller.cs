#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using Unity.XR.CoreUtils;
using static Team5.qPCR.Editor.TrainingUiBuilder;

namespace Team5.qPCR.Editor
{
    public static partial class TrainingFlowSceneInstaller
    {
        [MenuItem("Team 5/Training Flow/Repair Presentation PPE And Navigation")]
        public static void RepairPresentationLab()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || (scene.path != DemoPath && scene.path != LobbyPath))
                throw new InvalidOperationException("Open LobbyScene or DemoScene in Edit Mode.");
            RepairNavigation();
            if (scene.path == DemoPath)
            {
                RepairPpeGeometry();
                AddMissingFurnitureSolids();
                BuildT5Identity();
                CompactLabels();
                var sound = PpeAudio(Named("50_LESSON_AND_UI"), "T5_Confirmation_Sound", PpeSoundKind.Confirmation);
                sound.spatialBlend = 0; sound.volume = .22f;
                var feedback = Ensure<LessonStepFeedback>(sound.gameObject);
                Set(feedback, "ppe", Named("PPE_LessonSystem").GetComponent<PPELessonController>());
                Set(feedback, "qpcr", UnityEngine.Object.FindFirstObjectByType<QpcrLessonController>(FindObjectsInactive.Include));
                Set(feedback, "confirmation", sound);
            }
            RefreshTayo(); // Loads local clips only, never contacts a speech service.
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void RepairPpeGeometry()
        {
            var ppe = Named("PPE_LessonSystem").GetComponent<PPELessonController>();
            foreach(var side in new[]{"Left","Right"})
            {
                var probe=Named(side+"WashProbe").GetComponent<PPEHandProbe>();
                Set(probe,"trackedController",Named(side+" Controller"));
                Set(probe,"controllerLocalPosition",new Vector3(0,-.02f,-.06f));
                probe.transform.localPosition=new Vector3(0,-.02f,-.06f);
            }
            EnumValue(ppe, "mode", (int)PPELessonMode.Presentation);
            Set(ppe, "wetHandsSeconds", 1f); Set(ppe, "dryHandsSeconds", 2f); Set(ppe, "rubHandsSeconds", 20f);
            var faucet = Named("Sink_and_tap").Find("Lab Faucet") ?? Named("Sink_and_tap");
            var anchor = Child(faucet, "Tap_Outlet_Anchor").transform;
            anchor.position = new Vector3(2.562f, 1.322f, -2.014f);
            anchor.rotation = Quaternion.identity; anchor.localScale = Vector3.one;
            var water = Named("Running_Water");
            water.SetParent(anchor, false); water.localPosition = Vector3.zero;
            water.localRotation = Quaternion.Euler(90, 0, 0); water.localScale = Vector3.one;
            var particle = water.GetComponent<ParticleSystem>();
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particle.main; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = .25f; main.startSpeed = 1.15f; main.gravityModifier = .2f;
            main.startSize = .003f; main.maxParticles = 30;
            var shape = particle.shape; shape.angle = 1.5f; shape.radius = .007f;
            var emission=particle.emission; emission.rateOverTime=20;
            var temporary=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var dropletMesh=temporary.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(temporary);
            var particles=particle.GetComponent<ParticleSystemRenderer>();
            particles.renderMode=ParticleSystemRenderMode.Mesh;particles.mesh=dropletMesh;
            particles.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; particles.receiveShadows=false;
            var waterMaterial=TrainingMaterial("M_T5_Water_Stream",new Color(.50f,.80f,.90f,.45f),0,.55f);
            waterMaterial.SetFloat("_Surface",1);waterMaterial.SetFloat("_Blend",0);waterMaterial.SetFloat("_ZWrite",0);
            waterMaterial.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            waterMaterial.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            waterMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");waterMaterial.renderQueue=3000;
            var stream=Primitive(anchor,"Running_Water_Stream",PrimitiveType.Cylinder,anchor.position+Vector3.down*.174f,
                new Vector3(.011f,.174f,.011f),waterMaterial);
            stream.transform.localRotation=Quaternion.identity;
            stream.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            stream.GetComponent<Renderer>().receiveShadows=false;stream.layer=2;stream.SetActive(false);
            Set(Named("Tap_Handle").GetComponent<TapController>(),"waterVisual",stream);
            // 1.15 * .25 + .5 * 9.81 * .2 * .25^2 = .349 m, terminating at basin height .973 m.
            var wash = Named("HandWashVolume");
            wash.SetParent(anchor, false); wash.localPosition = new Vector3(0, -.13f, 0);
            wash.localRotation = Quaternion.identity; wash.localScale = Vector3.one;
            var box = wash.GetComponent<BoxCollider>(); box.center = Vector3.zero;
            box.size = new Vector3(.40f, .26f, .30f); box.isTrigger = true;
            Set(wash.GetComponent<HandWashZone>(), "lesson", ppe);

            var dryer = Named("Hand_Dryer");
            var dryerAnchor = Child(dryer, "Dryer_Outlet_Anchor").transform;
            var bounds = dryer.GetComponent<Renderer>()?.bounds ?? dryer.GetComponentInChildren<Renderer>().bounds;
            dryerAnchor.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z - .045f);
            dryerAnchor.rotation = Quaternion.identity; dryerAnchor.localScale = Vector3.one;
            var dry = Named("Dryer_Hand_Zone"); dry.SetParent(dryerAnchor, false);
            dry.localPosition = new Vector3(0, -.115f, 0); dry.localRotation = Quaternion.identity; dry.localScale = Vector3.one;
            var dryBox = dry.GetComponent<BoxCollider>(); dryBox.center = Vector3.zero;
            dryBox.size = new Vector3(.34f, .23f, .30f); dryBox.isTrigger = true;
            Set(dry.GetComponent<HandDryerZone>(), "lesson", ppe);

            var canvas = Named("PPE_Instruction_Canvas"); var ui = canvas.GetComponent<PPELessonUI>();
            var notice = Text(canvas, "Presentation_Notice", new Vector2(0, -245), new Vector2(900, 30), 21, PPELessonController.PresentationNotice, new Color(.43f, .30f, .08f));
            var detection = Text(canvas, "Hand_Detection", new Vector2(0, -102), new Vector2(900, 32), 22, "", Teal);
            Set(ui, "presentationNotice", notice); Set(ui, "detection", detection);
            var feedbackPanel=Canvas(Named("HANDWASH_STATION"),"PPE_Outlet_Status",wash.position+Vector3.up*.41f,new Vector2(600,160),.0006f);
            feedbackPanel.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>().enabled=false;
            var group=Ensure<CanvasGroup>(feedbackPanel);group.blocksRaycasts=false;group.interactable=false;
            Image(feedbackPanel.transform,"Background",Vector2.zero,new Vector2(600,160),Paper);
            var handStatus=Text(feedbackPanel.transform,"Hands",new Vector2(0,35),new Vector2(550,42),28,"",Teal);
            var holdStatus=Text(feedbackPanel.transform,"Hold_Progress",new Vector2(0,-32),new Vector2(550,45),26,"");
            var outlet=Ensure<PPEOutletStatus>(Named("HANDWASH_STATION").gameObject);
            Set(outlet,"lesson",ppe);Set(outlet,"waterZone",wash);Set(outlet,"dryerZone",dry);
            Set(outlet,"panel",feedbackPanel);Set(outlet,"hands",handStatus);Set(outlet,"progress",holdStatus);
            Set(outlet,"sourceProgress",canvas.Find("Progress").GetComponent<TMP_Text>());feedbackPanel.SetActive(false);
            canvas.Find("Progress").GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -144);
            canvas.Find("Continue_qPCR").GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -142);
            var soap = Named("Soap_Dispenser").GetComponent<LabLabelTarget>();
            if (soap != null) soap.Configure("Soap dispenser", "Required for real handwashing; soap and scrubbing are omitted only in this abbreviated demonstration.");
        }

        private static int WorldSolidLayer()
        {
            var existing = LayerMask.NameToLayer("LabWorldSolid"); if (existing >= 0) return existing;
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("layers");
            for (var i = 8; i < 32; i++) if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
            { layers.GetArrayElementAtIndex(i).stringValue = "LabWorldSolid"; tags.ApplyModifiedPropertiesWithoutUndo(); return i; }
            throw new InvalidOperationException("No free physics layer for room boundaries.");
        }

        private static void RepairNavigation()
        {
            var origin = Named("XR Origin (XR Rig)"); var controller = Ensure<CharacterController>(origin.gameObject);
            controller.radius = .18f; controller.skinWidth = .02f; controller.stepOffset = .15f; controller.minMoveDistance = 0;
            controller.height = 1.65f; controller.center = new Vector3(0, .825f, 0);
            var transformer = origin.GetComponentInChildren<XRBodyTransformer>(true) ?? Ensure<XRBodyTransformer>(origin.gameObject);
            transformer.xrOrigin = origin.GetComponent<XROrigin>(); transformer.useCharacterControllerIfExists = true;
            foreach (var provider in origin.GetComponentsInChildren<ContinuousMoveProvider>(true))
            { provider.moveSpeed = 1f; provider.enableFly = false; provider.useGravity = true; }
            var old = UnityEngine.Object.FindObjectsByType<XRInteractionSimulator>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(s => s.gameObject.scene == SceneManager.GetActiveScene());
            if (old != null && !(old is CollisionAwareXRInteractionSimulator))
            {
                var replacement = old.gameObject.AddComponent<CollisionAwareXRInteractionSimulator>();
                EditorUtility.CopySerializedManagedFieldsOnly(old, replacement);
                // Preserve all serialized references to the simulator, including the input bridge and sample UI.
                foreach (var component in SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)))
                {
                    if (component == null || component == old) continue;
                    var serialized = new SerializedObject(component); var property = serialized.GetIterator(); var changed = false;
                    while (property.Next(true)) if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == old)
                    { property.objectReferenceValue = replacement; changed = true; }
                    if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                UnityEngine.Object.DestroyImmediate(old); old = replacement;
            }
            if (old is CollisionAwareXRInteractionSimulator simulator)
            {
                simulator.Configure(transformer); Set(simulator, "groundSpeed", 1f);
                Ensure<XRInteractionSimulatorInputBridge>(simulator.gameObject).Configure(simulator);
            }
            var layer = WorldSolidLayer();
            var structure = Named("10_LAB_STRUCTURE");
            if (structure != null) foreach (var collider in structure.GetComponentsInChildren<Collider>(true))
                if (!collider.isTrigger && collider.name != "Epoxy_Floor") collider.gameObject.layer = layer;
            var camera = Named("Main Camera");
            var overlay = Canvas(camera, "Room_Boundary_Fade", camera.position, new Vector2(2000, 2000), .001f);
            overlay.transform.localPosition = new Vector3(0, 0, .33f); overlay.transform.localRotation = Quaternion.identity;
            overlay.GetComponent<Canvas>().sortingOrder = 999;
            var raycaster = overlay.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>(); if (raycaster != null) raycaster.enabled = false;
            Image(overlay.transform, "Darkness", Vector2.zero, new Vector2(2000, 2000), Color.black).sprite = null;
            var message = Text(overlay.transform, "Boundary_Message", Vector2.zero, new Vector2(720, 150), 32, "", Paper);
            message.alignment = TextAlignmentOptions.Center;
            var group = Ensure<CanvasGroup>(overlay); group.alpha = 0; group.blocksRaycasts = false; group.interactable = false;
            var warning = Ensure<HeadBoundaryWarning>(origin.gameObject);
            Set(warning, "head", camera); Set(warning, "fade", group); Set(warning, "warning", message);
            var so = new SerializedObject(warning); so.FindProperty("solidLayers").intValue = 1 << layer; so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddMissingFurnitureSolids()
        {
            var layer = WorldSolidLayer();
            var sinkTop = Named("Sink_and_tap").Find("Back_Bench_Worktop");
            var mesh = sinkTop.GetComponent<MeshFilter>();
            if (mesh != null)
            {
                var collider = Ensure<MeshCollider>(sinkTop.gameObject); collider.sharedMesh = mesh.sharedMesh; collider.convex = false;
                collider.isTrigger = false; sinkTop.gameObject.layer = layer; // Triangle surface preserves the basin cavity.
            }
            foreach (var root in new[] { Named("20_LAB_FURNITURE"), Named("30_PPE_AND_SAFETY") })
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.GetComponentInParent<Canvas>() != null) continue;
                    var name = renderer.name.ToLowerInvariant();
                    var table = renderer.GetComponentsInParent<Transform>(true).Any(t => t.name.ToLowerInvariant().Contains("glass table"));
                    var solid = table || name == "cabinet" || name.Contains("hanging_cabinet") || name.Contains("fridge") || name.Contains("freezer");
                    if (!solid || renderer.GetComponent<Collider>() != null || renderer.GetComponentInParent<Rigidbody>() != null) continue;
                    if(table && renderer.GetComponent<MeshFilter>()!=null)
                    {
                        var surface=Ensure<MeshCollider>(renderer.gameObject);
                        surface.sharedMesh=renderer.GetComponent<MeshFilter>().sharedMesh; surface.convex=false; surface.isTrigger=false;
                        renderer.gameObject.layer=layer; continue;
                    }
                    var collider = Ensure<BoxCollider>(Child(renderer.transform, "T5_Furniture_Solid"));
                    collider.center = renderer.localBounds.center; collider.size = renderer.localBounds.size; collider.isTrigger = false;
                    collider.gameObject.layer = layer;
                }
            foreach (var collider in Named("20_LAB_FURNITURE").GetComponentsInChildren<Collider>(true))
                if (!collider.isTrigger && collider.GetComponentInParent<Rigidbody>() == null) collider.gameObject.layer = layer;
            var secondary = Named("Back_Bench_Worktop (1)"); if (secondary != null) secondary.gameObject.layer = layer;
        }

        private static GameObject Sign(Transform parent, string name, Vector3 position, float yaw, string code, string title, string body)
        {
            var sign = Canvas(parent, name, position, new Vector2(780, 460), .0006f);
            sign.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var raycaster = sign.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>(); if (raycaster != null) raycaster.enabled = false;
            Ensure<CanvasGroup>(sign).blocksRaycasts = false;
            Image(sign.transform, "Card", Vector2.zero, new Vector2(780, 460), Paper);
            Image(sign.transform, "Rule", new Vector2(-353, 0), new Vector2(6, 394), Teal);
            Text(sign.transform, "Brand", new Vector2(0, 172), new Vector2(665, 44), 25, "T5  /  XR GENOMICS LAB    " + code, Teal);
            Text(sign.transform, "Heading", new Vector2(0, 102), new Vector2(665, 65), 42, title);
            Text(sign.transform, "Copy", new Vector2(0, -55), new Vector2(665, 225), 27, body);
            return sign;
        }

        private static void BuildT5Identity()
        {
            var root = Child(Named("50_LESSON_AND_UI"), "T5_LAB_IDENTITY").transform;
            Sign(root, "T5_Preparation_Sign", new Vector3(2.86f, 1.81f, -1.90f), 0, "01 / PREPARE", "Clean start. Careful science.",
                "Coat → water checkpoint → tap off → dry → gloves\n\nPresentation route only. Real handwashing also requires soap and thorough scrubbing.");
            Sign(root, "T5_Plate_Handoff_Sign", new Vector3(1.43f, 1.48f, 3.65f), 0, "02 / VERIFY", "One plate. Four checks.",
                "1  Match the experiment ID\n2  Find the A1 reference corner\n3  Inspect the optical seal\n4  Look for visible bubbles");
            Sign(root, "T5_qPCR_Workflow_Sign", new Vector3(4.33f, 1.72f, 3.97f), 0, "03 / RUN", "From plate to evidence.",
                "POWER → LOAD → REVIEW → RUN\n\nWatch fluorescence across cycles. Review controls, cycle threshold and melt behaviour before interpreting samples.\n\nSynthetic teaching data — not a clinical result.");
            var home = Named("Prepared_Plate_Home_Anchor");
            var matMaterial = TrainingMaterial("M_T5_Bench_Mat", new Color(.075f, .25f, .29f), 0, .15f);
            var mat = Primitive(root, "T5_Plate_Bench_Mat", PrimitiveType.Cube,
                home.position + new Vector3(0, -.003f, 0), new Vector3(.32f, .004f, .24f), matMaterial);
            var print = Canvas(root, "T5_Mat_Print", home.position + new Vector3(0, .001f, 0), new Vector2(620, 440), .0005f);
            print.transform.rotation = Quaternion.Euler(90, 0, 0);
            print.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>().enabled = false;
            Text(print.transform, "Mat_Title", new Vector2(0, 185), new Vector2(550, 40), 24, "T5  /  PREPARED PLATE", Paper).alignment = TextAlignmentOptions.Center;
            for (var i = 0; i < 4; i++)
            {
                var x = (i % 2 == 0 ? -1 : 1) * 143; var y = (i < 2 ? -1 : 1) * 94;
                Image(print.transform, "CornerH_" + i, new Vector2(x, y), new Vector2(26, 3), Paper);
                Image(print.transform, "CornerV_" + i, new Vector2(x + (x < 0 ? -12 : 12), y + (y < 0 ? 12 : -12)), new Vector2(3, 26), Paper);
            }
            Text(print.transform, "Mat_Footer", new Vector2(0, -186), new Vector2(550, 38), 22, "ID  •  A1  •  SEAL  •  WELLS", Paper).alignment = TextAlignmentOptions.Center;
            var handoff = Sign(root, "T5_Experiment_Handoff_Card", home.position + new Vector3(-.27f, .17f, .04f), 0,
                "T4 → T5", "Prepared plate handoff", "EXPERIMENT  T4-QPCR-2026-05\n\nPrepared reactions. Keep the optical seal intact. Check A1 before seating the plate.");
            handoff.GetComponent<RectTransform>().sizeDelta = new Vector2(780, 460);
            handoff.transform.localScale = Vector3.one * .00035f;
        }

        private static void CompactLabels()
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<LabHoverLabelController>(FindObjectsInactive.Include);
            var dots = new System.Collections.Generic.List<UnityEngine.Object>();
            var material = TrainingMaterial("M_T5_Identify_Cue", Teal, 0, .12f);
            for(var i=0;i<2;i++)
            {
                var dot = Primitive(Named("LAB_HOVER_LABELS"), i==0?"Left_Identify_Dot":"Right_Identify_Dot", PrimitiveType.Sphere,
                    Vector3.zero, Vector3.one*.012f, material);
                dot.layer=2; dot.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                dot.GetComponent<Renderer>().receiveShadows=false; dot.SetActive(false); dots.Add(dot.transform);
            }
            if(controller!=null) Objects(controller,"identificationDots",dots.ToArray());
            if(controller!=null) Set(controller,"preparation",Named("PPE_LessonSystem").GetComponent<PPELessonController>());
            foreach (var name in new[] { "Left_Object_Label", "Right_Object_Label" })
            {
                var panel = Named(name); if (panel == null) continue;
                panel.GetComponent<RectTransform>().sizeDelta = new Vector2(420, 168);
                Image(panel, "Background", Vector2.zero, new Vector2(420, 168), Paper);
                Image(panel, "Accent", new Vector2(-199, 0), new Vector2(4, 134), Teal);
                Text(panel, "Object_Name", new Vector2(0, 44), new Vector2(365, 40), 28, "");
                Text(panel, "Object_Purpose", new Vector2(0, -28), new Vector2(365, 96), 22, "");
            }
        }
    }
}
#endif
