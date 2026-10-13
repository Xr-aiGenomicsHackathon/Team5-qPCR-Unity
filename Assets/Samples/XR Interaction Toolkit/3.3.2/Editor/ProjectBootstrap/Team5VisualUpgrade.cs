using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

namespace Team5.qPCR.Editor
{
    /// <summary>Idempotent, in-place scene migration. Existing functional objects and GUIDs survive.</summary>
    public static class Team5VisualUpgrade
    {
        public const string ScenePath = "Assets/Team5/Scenes/Team5_qPCR_RealisticLab.unity";
        private const string Root = "Assets/Team5/";
        private static readonly Color Ink = Hex("142C4B"), Muted = Hex("5B6C82"), Blue = Hex("1D5FD0"), Pale = Hex("F0F5FB"), Border = Hex("D7E3F0");
        private static TMP_FontAsset regular, semibold;
        private static Sprite rounded;

        [MenuItem("Team 5/Apply blue-white visual upgrade (preserve scene)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before upgrading the scene.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (scene.isDirty && !string.IsNullOrEmpty(scene.path))
                    throw new InvalidOperationException("Save the current scene before opening the Team 5 scene.");
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            // Save a recoverable copy, not a new scene or a replacement of the original GUID.
            var backup = Path.GetFullPath("../deliverables/Team5-qPCR/Backups/BeforeVisualUpgrade.unity");
            if (!File.Exists(backup)) { Directory.CreateDirectory(Path.GetDirectoryName(backup)); EditorSceneManager.SaveScene(scene, backup, true); }
            AssetDatabase.Refresh();
            regular = EnsureFont("SourceSans3-Regular"); semibold = EnsureFont("SourceSans3-Semibold");
            rounded = EnsureRoundedSprite();
            UpgradeScience();
            UpgradeEquipment();
            UpgradeFurniture();
            UpgradeLighting();
            UpgradeNavigationAndUI();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Team5 Visual Upgrade] Preserved scene; upgraded science, scale, assets, UI and camera modes.");
        }

        private static void UpgradeScience()
        {
            var mix = AssetDatabase.LoadAssetAtPath<ReactionMixDefinition>(Root + "Data/Teaching_SYBR_ReactionMix.asset");
            if (mix == null) { mix = ScriptableObject.CreateInstance<ReactionMixDefinition>(); AssetDatabase.CreateAsset(mix, Root + "Data/Teaching_SYBR_ReactionMix.asset"); }
            var handoff = AssetDatabase.LoadAssetAtPath<PlateHandoffData>(Root + "Data/Team4_PlateHandoff.asset");
            if (handoff == null) throw new InvalidOperationException("Missing prepared Team 4 handoff.");
            handoff.SetReactionMix(mix); EditorUtility.SetDirty(handoff);
            var dialogue = AssetDatabase.LoadAssetAtPath<DialogueSequence>(Root + "Data/Team5_DialogueSequence.asset");
            dialogue.Configure(Enumerable.Range(0, 10).Select(i =>
            {
                var stage = (WorkflowStage)i; var content = WorkflowContent.For(stage);
                return new DialogueEntry { Stage = stage, Title = content.Title, Instruction = content.Instruction,
                    Action = content.Action, Why = content.Why, Focus = LabCameraDirector.FocusForStage(stage) };
            }).ToArray());
            EditorUtility.SetDirty(dialogue);
        }

        private static void UpgradeEquipment()
        {
            var plate = Find("T4_QPCR_2026_05_PREPARED_PLATE");
            var home = Find("Prepared_Plate_Home_Anchor");
            home.position = new Vector3(2.24f, .987f, 3.15f);
            plate.localScale = Vector3.one; plate.localPosition = Vector3.zero; plate.localRotation = Quaternion.identity;
            var body = Find("Polypropylene_96_Well_Plate", plate);
            body.localScale = new Vector3(.12776f, .01f, .08548f); body.localPosition = Vector3.up * .005f;
            body.GetComponent<Renderer>().enabled = false;
            Model(plate, "ClinicalPlateMesh", "Clinical_Plate", Vector3.zero);
            var wells = Find("ALL_96_WELLS_8x12", plate); wells.localPosition = Vector3.up * .0135f;
            for (var i = 0; i < wells.childCount; i++)
            { var well = wells.GetChild(i); well.localPosition = new Vector3(-.0495f + i % 12 * .009f, 0, .0315f - i / 12 * .009f); well.localScale = new Vector3(.005f, .0001f, .005f); }
            var seal = Find("Transparent_Optical_Seal", plate);
            seal.localPosition = Vector3.up * .0142f; seal.localScale = new Vector3(.124f, .00015f, .081f);
            var marker = Find("A1_ORIENTATION_MARKER", plate);
            marker.localPosition = new Vector3(-.059f, .0145f, .038f); marker.localScale = new Vector3(.005f, .0004f, .005f);
            foreach (var label in plate.GetComponentsInChildren<TMP_Text>(true))
            {
                label.font = regular; label.fontSize *= label.fontSize > 1 ? 1 : .075f;
                label.transform.localPosition = label.text == "A1" ? new Vector3(-.057f,.016f,.033f) : new Vector3(0,.015f,-.04f);
                label.transform.localRotation = Quaternion.Euler(90, 0, 0);
                label.transform.localScale = Vector3.one;
                label.fontSize = label.text == "A1" ? .04f : .024f;
                label.characterSpacing=0;
                label.rectTransform.sizeDelta = new Vector2(.115f,.008f);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.fontSharedMaterial = regular.material;
                label.color = Ink;
            }
            var rb = plate.GetComponent<Rigidbody>(); rb.mass = .04f; rb.isKinematic = true;

            var instrument = Find("TEAM5_REAL_TIME_QPCR_INSTRUMENT");
            instrument.position = new Vector3(2.80f, .985f, 3.43f); instrument.localScale = Vector3.one;
            foreach (Transform child in instrument)
            {
                if (child.name == "Motorized_Plate_Drawer" || child.name == "Power_Status_LED" || child.name == "ClinicalInstrumentMesh") continue;
                child.gameObject.SetActive(false);
            }
            var instrumentMesh = Model(instrument, "ClinicalInstrumentMesh", "Clinical_Instrument", Vector3.zero);
            if (IsArtistModel(instrumentMesh, "Q-PCR.fbx"))
            {
                // Q-PCR.fbx was authored ten times larger than real-world Unity units. At 0.1,
                // its body is approximately 404 x 400 x 581 mm. Re-centre the body and place its
                // base on the bench while preserving the functional controller on the parent.
                instrumentMesh.localScale = Vector3.one * .1f;
                instrumentMesh.localRotation = Quaternion.identity;
                instrumentMesh.localPosition = new Vector3(-.03408f, -.03097f, 0f);

                // The FBX includes a beautiful open drawer and 96-well block for presentation,
                // but the lesson needs the separate animated drawer, socket and prepared plate.
                // Hide the static duplicates and retain the machine housing/screen (Cube).
                foreach (var duplicate in new[] { "Gasket_Frame", "Plane.002", "Thermal_Block", "Wells_96" })
                    SetArtistPartVisible(instrumentMesh, duplicate, false);
                SetArtistPartVisible(instrumentMesh, "Plane", true);
                SetArtistPartVisible(instrumentMesh, "Plane.001", true);
            }
            var housingCollider = Ensure<BoxCollider>(instrument.gameObject);
            housingCollider.center = new Vector3(0,.20f,0); housingCollider.size = new Vector3(.404f,.40f,.581f);
            instrument.gameObject.layer = 2; // Physical obstacle, not a ray target; child buttons stay on Default.
            var drawer = Find("Motorized_Plate_Drawer", instrument);
            drawer.gameObject.SetActive(true); drawer.localScale = Vector3.one; drawer.localPosition = new Vector3(0,.105f,-.205f);
            drawer.GetComponent<Renderer>().enabled = false;
            drawer.GetComponent<BoxCollider>().size = new Vector3(.21f,.048f,.14f);
            drawer.gameObject.layer = 2;
            Find("Drawer_Thermal_Block", drawer).gameObject.SetActive(false);
            var drawerMesh = Model(drawer, "ClinicalDrawerMesh", "Clinical_Drawer", Vector3.zero);
            if (IsArtistModel(drawerMesh, "Lab Drawer.fbx"))
            {
                drawerMesh.localRotation = Quaternion.Euler(0,270,0);
                drawerMesh.localPosition = new Vector3(0f, 0f, .0179f);
                drawer.GetComponent<BoxCollider>().size = new Vector3(.14f,.048f,.21f);
            }
            var anchor = Find("A1_ALIGNED_PLATE_ANCHOR", drawer); anchor.localPosition = new Vector3(0,.014f,0); anchor.localScale = Vector3.one;
            if (IsArtistModel(instrumentMesh, "Q-PCR.fbx"))
            {
                // Reuse the qPCR artist's own moving tray, gasket, block and wells. Lab Drawer.fbx
                // is furniture, not the instrument's tray. The source was modelled open.
                drawerMesh.gameObject.SetActive(false);
                var assembly = FindOrCreate("Artist_Moving_Drawer_Assembly", drawer);
                foreach (Transform child in assembly.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                assembly.localPosition = new Vector3(-.03408f, -.13597f, .425f);
                assembly.localRotation = Quaternion.identity;
                assembly.localScale = Vector3.one * .1f;
                foreach (var partName in new[] { "Plane.002", "Gasket_Frame", "Thermal_Block", "Wells_96" })
                {
                    var original = instrumentMesh.Find(partName);
                    var part = Object.Instantiate(original.gameObject, assembly, false);
                    part.name = partName;
                    part.SetActive(true);
                }
                anchor.localPosition = new Vector3(-.0009f,-.0079f,.08f);
                drawer.GetComponent<BoxCollider>().size = new Vector3(.404f,.183f,.415f);
            }
            var indicator = Find("Power_Status_LED", instrument);
            indicator.localPosition = new Vector3(.174f,.30f,-.247f); indicator.localScale = Vector3.one * .009f;
            var instrumentSerialized = new SerializedObject(instrument.GetComponent<InstrumentController>());
            instrumentSerialized.FindProperty("drawerOpenOffset").vector3Value = new Vector3(0,0,-.22f);
            instrumentSerialized.ApplyModifiedPropertiesWithoutUndo();

            SetTransform("Computer_Monitor", new Vector3(3.60f,1.30f,3.57f), new Vector3(.53f,.33f,.026f));
            SetTransform("Computer_Screen", new Vector3(3.60f,1.30f,3.549f), new Vector3(.49f,.28f,.006f));
            SetTransform("Monitor_Stand", new Vector3(3.60f,1.06f,3.57f), new Vector3(.06f,.17f,.06f));
            SetTransform("Keyboard", new Vector3(3.60f,1.004f,3.12f), new Vector3(.44f,.025f,.15f));
            SetTransform("Plate_Centrifuge", new Vector3(-2.9f,1.12f,3.52f), new Vector3(.48f,.26f,.38f));
            SetTransform("Cold_Block", new Vector3(1.85f,1.001f,3.23f), new Vector3(.15f,.04f,.10f));
            // Tube meshes are metre-scale assets rather than enlarged coloured cylinders.
            foreach (var rackName in new[] { "Tube_Rack_1", "Tube_Rack_2", "Cold_Block" })
            {
                var rack = Find(rackName); if (rack == null) continue;
                var sourceChildren = rack.Cast<Transform>().Where(t => t.name.StartsWith("Microtube_") || t.name.StartsWith("ColdBlock_Tube_")).ToArray();
                foreach (var item in sourceChildren) item.gameObject.SetActive(false);
                if (rackName.StartsWith("Tube_Rack"))
                { rack.localScale = new Vector3(.14f,.025f,.11f); rack.position = new Vector3(-5.2f, .992f, rackName.EndsWith("1") ? .4f : -.1f); }
                var rackVisual = FindOrCreate(rackName + "_ArtistVisual", rack.parent);
                rackVisual.position = rack.position;
                rackVisual.rotation = Quaternion.identity;
                rackVisual.localScale = Vector3.one;
                Model(rackVisual, "ArtistRackMesh", "Artist_Microtube_Rack", Vector3.zero);
                var rackRenderer = rack.GetComponent<Renderer>();
                if (rackRenderer != null) rackRenderer.enabled = false;

                var rackTop = new Vector3(rack.position.x, .998f, rack.position.z);
                var container = FindOrCreate(rackName + "_RealScale_Tubes", rack.parent); container.position = rackTop;
                foreach (Transform existing in container.Cast<Transform>().ToArray()) Object.DestroyImmediate(existing.gameObject);
                Model(container, "TubeCluster_Left", "Artist_Microtube_Cluster", new Vector3(-.045f, .018f, 0f));
                Model(container, "TubeCluster_Right", "Artist_Microtube_Cluster", new Vector3(.045f, .018f, 0f));
            }
            var stand = Find("Pipette_Stand");
            foreach (var existing in stand.Cast<Transform>().ToArray()) existing.gameObject.SetActive(false);
            stand.localScale = new Vector3(.12f,.025f,.14f); stand.position = new Vector3(-4.95f,1.01f,1.7f);
            var pipettes = FindOrCreate("RealScale_Pipettes", stand.parent); pipettes.position = new Vector3(-4.95f,1.035f,1.7f);
            for (var i = 0; i < 3; i++) Model(pipettes, "Pipette_" + i, "Clinical_Pipette", new Vector3(-.035f+i*.035f,0,0));
            var schematic = Find("DNA_CYCLE_VISUALIZER"); schematic.position = new Vector3(4.23f,1.30f,3.48f); schematic.localScale = Vector3.one * .16f;
            // The old front header was below the ceiling and left a wide open wall in walkthrough.
            SetTransform("Front_Header", new Vector3(0,2.85f,-4.45f), new Vector3(12,.7f,.1f));
            SetTransform("Front_Left_Return", new Vector3(-5.45f,1.25f,-4.45f), new Vector3(1.0f,2.5f,.1f));
            SetTransform("Front_Right_Return", new Vector3(.75f,1.25f,-4.45f), new Vector3(10.4f,2.5f,.1f));
        }

        private static void UpgradeLighting()
        {
            if (UniversalRenderPipeline.asset == null) throw new InvalidOperationException("URP must be active.");
            RenderSettings.ambientSkyColor = Hex("DFE6EF"); RenderSettings.ambientEquatorColor = Hex("B2BDCA");
            RenderSettings.ambientGroundColor = Hex("667482"); RenderSettings.ambientIntensity = .72f;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                light.color = new Color(1,.97f,.92f);
                if (light.type == LightType.Point) { light.intensity = 2.8f; light.shadows = LightShadows.None; }
                else if (light.type == LightType.Directional) { light.intensity = .9f; light.shadows = LightShadows.Soft; }
            }
            var colors = new[] { ("M_EpoxyFloor","8F9EAC"), ("M_PaintedWall","D5DEE8"), ("M_LabWorktop","3B4C5D"),
                ("M_LabCabinet","C7D2DE"),("M_InstrumentPlastic","34455C"),("M_TealEmission","D8E8F9"),
                ("M_BlueEmission","2E65B9"),("M_SampleLiquid","CEE2E9"),("M_ControlLiquid","CEE2E9"),("M_UnusedWell","768593") };
            foreach (var item in colors)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/RealisticLab/" + item.Item1 + ".mat");
                if (material == null) continue;
                material.SetColor("_BaseColor", Hex(item.Item2)); material.SetColor("_EmissionColor", Color.black); material.DisableKeyword("_EMISSION"); EditorUtility.SetDirty(material);
            }
            foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (volume.sharedProfile == null) continue;
                var profile = volume.sharedProfile;
                if (profile.TryGet<ColorAdjustments>(out var color)) { color.postExposure.Override(-.08f); color.contrast.Override(6); color.saturation.Override(-4); }
                if (profile.TryGet<Bloom>(out var bloom)) bloom.intensity.Override(.035f);
                if (profile.TryGet<Vignette>(out var vignette)) vignette.intensity.Override(.025f);
                if (profile.TryGet<Tonemapping>(out var tone)) tone.mode.Override(TonemappingMode.Neutral);
                EditorUtility.SetDirty(profile);
            }
        }

        private static void UpgradeFurniture()
        {
            var furniture = Find("LAB_FURNITURE_AND_STORAGE");
            // Work only from the original blockout children. Existing Refined_* visuals may be
            // replaced by Model(); iterating those destroyed references would stop the upgrade
            // before the sink, coats, centrifuge and scientist are processed.
            foreach (var original in furniture.Cast<Transform>()
                         .Where(item => item != null && !item.name.StartsWith("Refined_"))
                         .ToArray())
            {
                var side = original.name.StartsWith("Side_Cabinet_");
                if (side || original.name.StartsWith("Base_Cabinet_"))
                {
                    original.GetComponent<Renderer>().enabled = false;
                    var mesh = Model(furniture, "Refined_"+original.name, "Clinical_Cabinet",
                        new Vector3(original.position.x,0,original.position.z));
                    if (IsArtistModel(mesh, "Cabinet.fbx"))
                    {
                        // The team cabinet is 1.04 m wide and 0.85 m tall. Compress only its
                        // unusually deep 0.95 m axis to a realistic 0.60 m laboratory cabinet.
                        mesh.localScale = new Vector3(1f, 1f, .63f);
                        if (side)
                        {
                            mesh.localRotation = Quaternion.Euler(0, 90, 0);
                            mesh.localPosition += new Vector3(-.00775f, .1717f, .8923f);
                        }
                        else
                        {
                            mesh.localRotation = Quaternion.Euler(0, 180, 0);
                            mesh.localPosition += new Vector3(.8923f, .1717f, .00775f);
                        }
                    }
                    else if(side){mesh.localRotation=Quaternion.Euler(0,-90,0)*mesh.localRotation;mesh.localScale=Vector3.Scale(mesh.localScale,new Vector3(.96f,1,.91f));}
                }
                if (original.name.StartsWith("Cabinet_Handle_") || original.name.StartsWith("Side_Drawer_Handle_")) original.gameObject.SetActive(false);
            }
            var sink=Find("Handwashing_Sink");
            // Keep the simple hidden blockout active as a dependable collision surface.
            sink.gameObject.SetActive(true);
            foreach (var renderer in sink.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            Model(furniture,"Refined_HandwashingSink","Clinical_Sink",new Vector3(-4.35f,1.10f,3.465f));
            var coats=Find("PPE_AND_LAB_COAT_AREA");
            for(var i=0;i<3;i++)
            {
                Find("Coat_"+(i+1),coats).gameObject.SetActive(false);
                Model(coats,"Refined_Coat_"+i,"Clinical_HangingCoat",new Vector3(-.47f+i*.47f,1.3f,.19f));
            }
            var centrifuge=Find("Plate_Centrifuge");
            centrifuge.GetComponent<Renderer>().enabled=false;
            foreach(Transform child in centrifuge)child.gameObject.SetActive(false);
            var centrifugeMesh = Model(centrifuge.parent,"Refined_Centrifuge","Clinical_Centrifuge",new Vector3(-2.9f,.985f,3.52f));
            if (IsArtistModel(centrifugeMesh, "Compact_plate_centrifuge.fbx"))
                centrifugeMesh.localPosition += new Vector3(0f, -.7737f, .0325f);
        }

        private static void UpgradeNavigationAndUI()
        {
            var camera = Find("Desktop_Overview_Camera").GetComponent<Camera>();
            camera.nearClipPlane = .012f; camera.backgroundColor = Hex("E7EEF6");
            camera.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            var orbit = camera.GetComponent<CameraOrbitController>(); if (orbit != null) orbit.enabled = false;
            var workflow = Object.FindFirstObjectByType<WorkflowController>();
            var plate = Object.FindFirstObjectByType<PlateController>();
            var grab=plate.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            var highlight=FindOrCreate("Selection_Outline",plate.transform);
            var line=Ensure<LineRenderer>(highlight.gameObject);
            line.useWorldSpace=false;line.loop=true;line.positionCount=4;line.widthMultiplier=.0012f;
            line.SetPositions(new[]{new Vector3(-.066f,.017f,-.044f),new Vector3(.066f,.017f,-.044f),new Vector3(.066f,.017f,.044f),new Vector3(-.066f,.017f,.044f)});
            var lineMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/RealisticLab/M_SelectionBlue.mat");
            if(lineMaterial==null){lineMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));lineMaterial.SetColor("_BaseColor",Blue);AssetDatabase.CreateAsset(lineMaterial,Root+"Materials/RealisticLab/M_SelectionBlue.mat");}
            line.sharedMaterial=lineMaterial;line.enabled=false;
            Ensure<PreparedPlateInteraction>(plate.gameObject).Configure(workflow,grab,line);
            var shell = Object.FindFirstObjectByType<LabShellController>();
            var simulator = Object.FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRInteractionSimulator>(FindObjectsInactive.Include);
            if (simulator != null)
                Ensure<XRInteractionSimulatorInputBridge>(simulator.gameObject).Configure(simulator);
            var director = Ensure<LabCameraDirector>(camera.gameObject);
            director.enabled = true;
            director.SetDialogueSequence(AssetDatabase.LoadAssetAtPath<DialogueSequence>(Root+"Data/Team5_DialogueSequence.asset"));
            var player = FindOrCreate("LEARNER_SCIENTIST", null); player.position = new Vector3(2.8f,.05f,1.9f); player.rotation = Quaternion.identity;
            var controller = Ensure<CharacterController>(player.gameObject);
            controller.height = 1.78f; controller.radius = .25f; controller.center = new Vector3(0,.89f,0); controller.stepOffset = .15f; controller.skinWidth = .025f;
            var avatar = Model(player, "ScientistVisual", "Laboratory_Scientist", Vector3.zero);
            var animations = SetupAnimation(avatar);
            var targetsRoot = FindOrCreate("GUIDED_CAMERA_TARGETS", null);
            var positions = new[] {new Vector3(0,1,0),new Vector3(2.8f,1.28f,3.17f),new Vector3(2.24f,1.002f,3.15f),
                new Vector3(2.68f,1.1f,3.16f),new Vector3(3.6f,1.3f,3.55f)};
            var targets = new Transform[5];
            for (var i=0;i<5;i++) { targets[i] = FindOrCreate(((CameraFocus)i).ToString(), targetsRoot); targets[i].position = positions[i]; }
            var canvas = Find("COMPACT_DESKTOP_AND_XR_UI");
            RestyleCanvas(canvas, false); RestyleCanvas(Find("XR_WORLD_SPACE_LEARNING_UI"), true);
            var fadeObject = Panel("CameraTransitionFade", canvas, Color.white);
            Stretch(fadeObject); fadeObject.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            var fade = Ensure<CanvasGroup>(fadeObject.gameObject); fade.alpha = 0; fade.blocksRaycasts = false;
            director.Configure(workflow, shell, camera, controller, avatar.gameObject, animations, targets, fade);
            var mentor = Find("Reusable_Mentor_Dialogue", canvas);
            var mentorController = mentor.GetComponent<MentorPanelController>();
            var visibility = Ensure<CanvasGroup>(mentor.gameObject);
            mentorController.ConfigureNavigation(director, visibility);
            At(mentor, new Vector2(0,0), new Vector2(24,24), new Vector2(480,260));
            Place(mentor,"Step_Counter",16,226,190,26,14);
            Place(mentor,"Stage_Title",16,182,448,42,28);
            Place(mentor,"Short_Instruction",16,108,448,76,18);
            Place(mentor,"Context_Status",16,62,448,42,16);
            Place(mentor,"Why_Button",338,226,62,28);
            Stretch(Find("Why_Button/Label",mentor).GetComponent<RectTransform>());
            Find("Why_Button/Label",mentor).GetComponent<TMP_Text>().text = "Why?";
            Place(mentor,"Primary_Action",246,14,218,40);
            Place(mentor,"Secondary_Action",16,14,218,40);
            Place(mentor,"Run_Progress",16,56,448,3);
            Find("Optional_Why_Explanation",mentor).gameObject.SetActive(false);
            Button(mentor,"BackExplanation","Back",new Rect(274,226,58,28),mentorController.Back,false);
            Button(mentor,"MinimizeDialogue","—",new Rect(418,226,46,28),mentorController.Minimize,false);

            var bar=Find("Top_Status_Bar",canvas);
            Anchored(bar,new Vector2(0,1),new Vector2(1,1),new Vector2(0,-72),Vector2.zero);
            Place(bar,"Project_Title",24,13,350,46,25);
            Find("Project_Title",bar).GetComponent<TMP_Text>().text="qPCR  /  Team 5";
            var chapterText=Text("ChapterNavigation",bar,"",18,true); At(chapterText.rectTransform,new Vector2(.5f,.5f),new Vector2(-190,-14),new Vector2(600,28));
            var disclaimer=Find("Scientific_Disclaimer",bar).GetComponent<TMP_Text>();
            At(disclaimer.rectTransform,new Vector2(1,.5f),new Vector2(-340,-16),new Vector2(316,32)); disclaimer.fontSize=16;disclaimer.color=Muted;
            var toolbar=Panel("ViewToolbar",canvas,Color.white);
            At(toolbar,new Vector2(0,1),new Vector2(24,-132),new Vector2(890,46));
            Button(toolbar,"GuidedView","1  Guided",new Rect(8,5,124,36),director.Guided,false);
            Button(toolbar,"FirstPersonView","2  First person",new Rect(140,5,146,36),director.FirstPerson,false);
            Button(toolbar,"ThirdPersonView","3  Third person",new Rect(294,5,154,36),director.ThirdPerson,false);
            Button(toolbar,"ReturnToStep","Return to step",new Rect(456,5,150,36),director.ReturnToStep,false);
            Button(toolbar,"ReopenDialogue","Show instruction",new Rect(614,5,168,36),mentorController.Reopen,false);
            var controls=Text("ViewHelp",canvas,"",16,false); At(controls.rectTransform,new Vector2(0,1),new Vector2(28,-171),new Vector2(1000,28)); controls.color=Ink;
            var settings = Panel("CameraSettings",canvas,Color.white); At(settings,new Vector2(1,1),new Vector2(-348,-132),new Vector2(324,46));
            var motionButton = Button(settings,"MotionSetting","Motion: full",new Rect(6,5,100,36),director.ToggleReducedMotion,false);
            var zoomLabel=Text("ZoomLabel",settings,"Zoom speed",16,false);At(zoomLabel.rectTransform,Vector2.zero,new Vector2(114,10),new Vector2(90,26));
            var zoom=Slider(settings,"ZoomSensitivity",new Rect(199,14,115,18));zoom.minValue=.5f;zoom.maxValue=2;zoom.value=1;
            while(zoom.onValueChanged.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(zoom.onValueChanged,0);
            UnityEventTools.AddPersistentListener(zoom.onValueChanged,director.SetZoomSensitivity);
            LayoutProtocol(Find("Machine_Protocol_Touchscreen",canvas));
            var results=Find("Dedicated_Results_Dialogue",canvas);At(results,new Vector2(1,.5f),new Vector2(-1024,-340),new Vector2(1000,680));
            Find("Results_Heading",results).GetComponent<TMP_Text>().text="Amplification results";
            var mix=BuildMixSheet(canvas);
            var hud=Ensure<LabHudController>(canvas.gameObject);
            hud.Configure(workflow,director,chapterText,controls,mix.gameObject,Find("Machine_Protocol_Touchscreen",canvas).gameObject,results.gameObject);
            hud.SetMotionLabel(motionButton.GetComponentInChildren<TMP_Text>());
            while(motionButton.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(motionButton.onClick,0);
            UnityEventTools.AddPersistentListener(motionButton.onClick,hud.ToggleMotion);
            Button(toolbar,"ReactionMix","Mix",new Rect(790,5,92,36),hud.OpenMix,false);
            Button(mix,"CloseMix","Close",new Rect(492,18,120,40),hud.CloseMix,true);
            mix.gameObject.SetActive(false);
            shell.SetMode(InteractionMode.Desktop);shell.SetDesktopOverview(true);
            // Give the saved scene an immediately useful overview even outside Play mode.
            var rotation=Quaternion.Euler(34,-12,0);camera.transform.SetPositionAndRotation(positions[0]-rotation*Vector3.forward*14,rotation);
            avatar.gameObject.SetActive(false);
            foreach(var component in new Object[]{director,shell,mentorController,hud})EditorUtility.SetDirty(component);
        }

        private static void LayoutProtocol(Transform panel)
        {
            At((RectTransform)panel,new Vector2(1,.5f),new Vector2(-784,-340),new Vector2(760,680));
            Place(panel,"Protocol_Heading",24,624,712,38,28);Find("Protocol_Heading",panel).GetComponent<TMP_Text>().text="Configure the run";
            Place(panel,"Protocol_Subheading",24,585,712,38,17);Find("Protocol_Subheading",panel).GetComponent<TMP_Text>().text="Review the teaching protocol. Changes are checked before loading.";
            var groups = new[] { ("Reaction",554), ("Temperature programme",443), ("Fluorescence",151) };
            foreach(var group in groups){var label=Text("Group_"+group.Item1,panel,group.Item1,18,true);At(label.rectTransform,Vector2.zero,new Vector2(24,group.Item2),new Vector2(710,26));label.color=Blue;}
            var order = new[] {0,1,2,3,4,5,6,7,8,9,10,11,12,13};
            for(var i=0;i<14;i++)
            {
                float x,y;
                if(i<2){x=24+i*240;y=494;}
                else if(i<11){var cell=i-2;x=24+cell%3*240;y=383-cell/3*87;}
                else{x=24+(i-11)*240;y=91;}
                Place(panel,"Label_"+i.ToString("00"),x,y+37,222,23,16);
                Place(panel,"Protocol_Field_"+i.ToString("00"),x,y,222,34);
            }
            Place(panel,"Fluorophore_Label",504,531,220,23,16);
            Place(panel,"Fluorophore_Dropdown",504,494,222,34);
            Place(panel,"Protocol_Validation_Feedback",24,22,710,48,17);
        }

        private static RectTransform BuildMixSheet(Transform parent)
        {
            var panel=Panel("ReactionMixTeachingSheet",parent,Color.white);At(panel,new Vector2(.5f,.5f),new Vector2(-320,-350),new Vector2(640,700));
            var title=Text("MixTitle",panel,"What is inside each well?",28,true);At(title.rectTransform,Vector2.zero,new Vector2(28,634),new Vector2(580,40));
            var text=Text("MixTable",panel,"<color=#1D5FD0>Teaching example · 20 µL per active reaction</color>\n\n"+
                "2× SYBR Green supermix<pos=440>10 µL\nForward primer (10 µM)<pos=440>0.6 µL\nReverse primer (10 µM)<pos=440>0.6 µL\nDNA template<pos=440>2 µL\nNuclease-free water<pos=440>6.8 µL\n\n"+
                "<b>NTC: replace the 2 µL of DNA with water.</b>\nFinal supermix: 1×. Each primer: 300 nM.\n\n"+
                "The supermix already contains polymerase, nucleotides, magnesium and SYBR dye.\n\n"+
                "26 samples + 1 positive control + 1 NTC.\nThe other 68 wells are unused.\n\n"+
                "Composition based on Bio-Rad iTaq guidance. The team's 35-cycle programme is a classroom example, not a validated diagnostic assay.",18,false);
            At(text.rectTransform,Vector2.zero,new Vector2(28,77),new Vector2(584,546));text.lineSpacing=2;text.alignment=TextAlignmentOptions.TopLeft;
            return panel;
        }

        private static void RestyleCanvas(Transform canvas,bool xr)
        {
            if(canvas==null)return;
            foreach(var image in canvas.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                image.color=image.name.Contains("Fill")?Blue:image.GetComponent<UnityEngine.UI.Selectable>()!=null?Pale:Color.white;
                image.sprite=rounded;image.type=UnityEngine.UI.Image.Type.Sliced;image.pixelsPerUnitMultiplier=2;
                var outline=image.GetComponent<UnityEngine.UI.Outline>();if(outline!=null){outline.effectColor=Border;outline.effectDistance=new Vector2(1,-1);}
            }
            foreach(var text in canvas.GetComponentsInChildren<TMP_Text>(true))
            {text.font=(text.fontStyle&FontStyles.Bold)!=0?semibold:regular;text.fontSharedMaterial=text.font.material;text.characterSpacing=0;text.wordSpacing=0;text.color=Ink;text.raycastTarget=false;}
            foreach(var button in canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true))StyleButton(button,button.name.Contains("Primary"));
            foreach(var field in canvas.GetComponentsInChildren<TMP_InputField>(true))
            {field.selectionColor=new Color(.11f,.37f,.81f,.25f);field.caretColor=Blue;field.textComponent.fontSize=xr?22:18;}
            foreach(var heading in canvas.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.name.Contains("Heading"))) heading.fontSize=xr?30:28;
            foreach(var graph in canvas.GetComponentsInChildren<AmplificationCurveGraphic>(true))
            {var so=new SerializedObject(graph);so.FindProperty("gridColor").colorValue=Border;so.FindProperty("thresholdColor").colorValue=Muted;so.ApplyModifiedPropertiesWithoutUndo();graph.raycastTarget=false;}
            foreach(var legend in canvas.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.name=="Curve_Legend"))
                legend.text="<color=#143361>● Positive control</color>     <color=#1C5ED0>● Example sample</color>     <color=#8A4526>● NTC</color>";
        }

        private static Animation SetupAnimation(Transform avatar)
        {
            var path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(avatar.gameObject);
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            var animation=Ensure<Animation>(avatar.gameObject);
            var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
            if (clips.Length == 0)
            {
                // The team scientist is correctly life-sized but currently has no armature or
                // animation clips. It still follows the working third-person CharacterController.
                animation.playAutomatically = false;
                animation.clip = null;
                return animation;
            }
            if(importer.animationType!=ModelImporterAnimationType.Legacy){importer.animationType=ModelImporterAnimationType.Legacy;importer.SaveAndReimport();}
            foreach(var clip in clips)
            {
                foreach(var name in new[]{"Idle","Walk","Reach"})if(clip.name.Contains(name)){animation.AddClip(clip,name);animation[name].wrapMode=WrapMode.Loop;}
            }
            animation.playAutomatically=true;animation.clip=animation.GetClip("Idle");
            return animation;
        }

        private static Transform Model(Transform parent,string name,string filename,Vector3 position)
        {
            var sourcePath = ResolveModelPath(filename);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if(prefab==null)throw new InvalidOperationException("Missing Blender export: "+filename);
            var existing=parent.Find(name);
            if (existing != null)
            {
                var existingSource = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(existing.gameObject);
                if (!string.Equals(existingSource, sourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    Object.DestroyImmediate(existing.gameObject);
                    existing = null;
                }
            }
            GameObject model;
            if(existing!=null)model=existing.gameObject;
            else
            {
                model=(GameObject)PrefabUtility.InstantiatePrefab(prefab);model.name=name;model.transform.SetParent(parent,false);
            }
            model.SetActive(true);model.transform.localPosition=position;
            // FBX contains Blender's axis conversion and centimetre scale. Preserve those,
            // then orient equipment fronts toward the room (the learner faces forward).
            model.transform.localScale=prefab.transform.localScale;
            model.transform.localRotation=Quaternion.Euler(0,filename=="Laboratory_Scientist"||filename=="Clinical_Instrument"?0:180,0)*prefab.transform.localRotation;
            if (filename == "Artist_Microtube_Rack") model.transform.localPosition += new Vector3(-.0642f, .0105f, -.0411f);
            if (filename == "Artist_Microtube_Cluster")
            {
                // The supplied file is an exploded arrangement. Place each body and matching cap
                // into a rack slot without altering the artist's source FBX.
                for (var index = 0; index < 3; index++)
                {
                    var suffix = index == 0 ? "" : ".00" + index;
                    var body = model.transform.Find("Tube body" + suffix);
                    var cap = model.transform.Find("Tube cover" + suffix);
                    if (body == null || cap == null) continue;
                    var original = prefab.transform.Find(body.name);
                    var originalCap = prefab.transform.Find(cap.name);
                    body.localPosition = original.localPosition;
                    cap.localPosition = originalCap.localPosition;
                    var center = model.transform.InverseTransformPoint(body.GetComponent<Renderer>().bounds.center);
                    var delta = new Vector3((index - 1) * .024f, .0175f, 0f) - center;
                    body.localPosition += delta;
                    cap.localPosition += delta;
                }
            }
            var modelStem = SafeAssetName(Path.GetFileNameWithoutExtension(sourcePath));
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var originalRenderer = prefab.transform.Find(AnimationUtility.CalculateTransformPath(renderer.transform, model.transform))?.GetComponent<Renderer>();
                var sourceMaterials = originalRenderer != null ? originalRenderer.sharedMaterials : renderer.sharedMaterials;
                renderer.sharedMaterials=sourceMaterials.Select(source=>
                {
                    var path=Root+"Materials/RealisticLab/Artist_"+modelStem+"_"+SafeAssetName(source.name)+".mat";
                    var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(mat==null)
                    {
                        mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.name="Artist_"+modelStem+"_"+source.name;
                        var color=source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):source.color;
                        mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",source.name.Contains("Cotton")?.2f:.42f);
                        var metal = source.name.Contains("Stainless") || source.name.Contains("Aluminium") || sourcePath.EndsWith("Stainless_washbasin.fbx",StringComparison.OrdinalIgnoreCase);
                        mat.SetFloat("_Metallic",metal ? .85f : 0);AssetDatabase.CreateAsset(mat,path);
                    }
                    return mat;
                }).ToArray();
            }
            return model.transform;
        }

        private static string ResolveModelPath(string filename)
        {
            var artistPath = filename switch
            {
                "Clinical_Instrument" => Root + "Models/Our-design/Q-PCR.fbx",
                "Clinical_Drawer" => Root + "Models/Our-design/Lab Drawer.fbx",
                "Artist_Microtube_Rack" => Root + "Models/Our-design/Microtube_Rack.fbx",
                "Artist_Microtube_Cluster" => Root + "Models/Our-design/Microtube.fbx",
                "Clinical_Cabinet" => Root + "Models/Our-design/Cabinet.fbx",
                "Clinical_Centrifuge" => Root + "Models/Our-design/Compact_plate_centrifuge.fbx",
                "Clinical_HangingCoat" => Root + "Models/Our-design/HangingCoat.fbx",
                "Clinical_Sink" => Root + "Models/Our-design/Stainless_washbasin.fbx",
                "Laboratory_Scientist" => Root + "Models/Our-design/Laboratory_Scientist.fbx",
                _ => string.Empty
            };
            if (!string.IsNullOrEmpty(artistPath) && AssetDatabase.LoadAssetAtPath<GameObject>(artistPath) != null)
                return artistPath;
            return Root + "Models/Redesign/" + filename + ".fbx";
        }

        private static bool IsArtistModel(Transform instance, string fileName)
        {
            if (instance == null) return false;
            var source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance.gameObject);
            return source.EndsWith("/Our-design/" + fileName, StringComparison.OrdinalIgnoreCase);
        }

        private static void SetArtistPartVisible(Transform root, string partName, bool visible)
        {
            var part = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == partName);
            if (part != null) part.gameObject.SetActive(visible);
        }

        private static string SafeAssetName(string value)
        {
            return new string(value.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
        }

        private static TMP_FontAsset EnsureFont(string name)
        {
            var path=Root+"UI/Fonts/"+name+" SDF.asset";var asset=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if(asset!=null){PrepareFont(asset);return asset;}
            var source=AssetDatabase.LoadAssetAtPath<Font>(Root+"UI/Fonts/"+name+".ttf");if(source==null)throw new InvalidOperationException("Missing Source Sans font.");
            asset=TMP_FontAsset.CreateFontAsset(source,60,7,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
            asset.name=name+" SDF";AssetDatabase.CreateAsset(asset,path);
            AssetDatabase.AddObjectToAsset(asset.material,asset);
            foreach(var atlas in asset.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,asset);
            PrepareFont(asset);return asset;
        }
        private static void PrepareFont(TMP_FontAsset asset)
        {
            asset.atlasPopulationMode=AtlasPopulationMode.Dynamic;
            asset.TryAddCharacters(new string(Enumerable.Range(32,224).Select(i=>(char)i).ToArray())+"Δ–—…✓•→≤≥▼●");
            asset.atlasPopulationMode=AtlasPopulationMode.Static;
            // Screen-space derivatives remain stable across overview, close-up and world UI scales.
            asset.material.shader=Shader.Find("TextMeshPro/Mobile/Distance Field SSD");
            EditorUtility.SetDirty(asset.material);EditorUtility.SetDirty(asset);
        }
        private static Sprite EnsureRoundedSprite()
        {
            const string path=Root+"UI/RoundedPanel.asset";var existing=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(existing!=null)return existing;
            var texture=new Texture2D(128,128,TextureFormat.RGBA32,false);texture.name="RoundedPanelTexture";
            for(var y=0;y<128;y++)for(var x=0;x<128;x++)
            {var q=new Vector2(Mathf.Max(32-x,0,x-95),Mathf.Max(32-y,0,y-95));texture.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(32-q.magnitude)));}
            texture.Apply();var sprite=Sprite.Create(texture,new Rect(0,0,128,128),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(32,32,32,32));
            sprite.name="RoundedPanel";AssetDatabase.CreateAsset(sprite,path);AssetDatabase.AddObjectToAsset(texture,sprite);return sprite;
        }
        private static RectTransform Panel(string name,Transform parent,Color color)
        {
            var existing=parent.Find(name);var go=existing==null?new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image)):existing.gameObject;
            go.transform.SetParent(parent,false);var image=go.GetComponent<UnityEngine.UI.Image>();image.color=color;image.sprite=rounded;image.type=UnityEngine.UI.Image.Type.Sliced;image.pixelsPerUnitMultiplier=2;
            return(RectTransform)go.transform;
        }
        private static TMP_Text Text(string name,Transform parent,string value,float size,bool bold)
        {
            var existing=parent.Find(name);var go=existing==null?new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)):existing.gameObject;go.transform.SetParent(parent,false);
            var text=go.GetComponent<TMP_Text>();text.font=bold?semibold:regular;text.fontSharedMaterial=text.font.material;text.fontSize=size;text.color=Ink;text.text=value;text.raycastTarget=false;text.overflowMode=TextOverflowModes.Truncate;return text;
        }
        private static UnityEngine.UI.Button Button(Transform parent,string name,string label,Rect rect,UnityEngine.Events.UnityAction action,bool primary)
        {
            var root=Panel(name,parent,Pale);At(root,Vector2.zero,rect.position,rect.size);
            var button=Ensure<UnityEngine.UI.Button>(root.gameObject);button.targetGraphic=root.GetComponent<UnityEngine.UI.Image>();
            while(button.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(button.onClick,0);
            UnityEventTools.AddPersistentListener(button.onClick,action);
            var text=Text("Label",root,label,17,true);Stretch(text.rectTransform);text.alignment=TextAlignmentOptions.Center;StyleButton(button,primary);return button;
        }
        private static void StyleButton(UnityEngine.UI.Button button,bool primary)
        {
            var image=button.GetComponent<UnityEngine.UI.Image>();if(image!=null)image.color=primary?Blue:Pale;
            foreach(var label in button.GetComponentsInChildren<TMP_Text>(true))label.color=primary?Color.white:Blue;
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=Hex("DCE9FA");colors.selectedColor=Hex("DCE9FA");colors.pressedColor=Hex("B5CEF1");colors.disabledColor=new Color(.8f,.82f,.86f,.75f);button.colors=colors;
        }
        private static UnityEngine.UI.Slider Slider(Transform parent,string name,Rect rect)
        {
            var root=Panel(name,parent,Border);At(root,Vector2.zero,rect.position,rect.size);
            var slider=Ensure<UnityEngine.UI.Slider>(root.gameObject);
            var handle=Panel("Handle",root,Blue);At(handle,new Vector2(.5f,.5f),new Vector2(-8,-12),new Vector2(16,24));slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<UnityEngine.UI.Image>();return slider;
        }
        private static void Place(Transform parent,string name,float x,float y,float w,float h,float size=0)
        {var t=Find(name,parent);At((RectTransform)t,Vector2.zero,new Vector2(x,y),new Vector2(w,h));if(size>0&&t.TryGetComponent<TMP_Text>(out var text))text.fontSize=size;}
        private static void At(Transform target,Vector2 anchor,Vector2 offset,Vector2 size)
        {var rect=(RectTransform)target;rect.anchorMin=rect.anchorMax=anchor;rect.pivot=Vector2.zero;rect.anchoredPosition=offset;rect.sizeDelta=size;}
        private static void Anchored(Transform target,Vector2 min,Vector2 max,Vector2 low,Vector2 high)
        {var rect=(RectTransform)target;rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=low;rect.offsetMax=high;}
        private static void Stretch(RectTransform rect)=>Anchored(rect,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        private static void SetTransform(string name,Vector3 position,Vector3 scale){var t=Find(name);t.position=position;t.localScale=scale;}
        private static Transform FindOrCreate(string name,Transform parent)
        {var t=parent==null?Find(name):parent.Find(name);if(t!=null)return t;t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
        private static Transform Find(string name,Transform parent=null)
        {if(parent!=null){var direct=parent.Find(name);if(direct!=null)return direct;return parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);}
            return Resources.FindObjectsOfTypeAll<Transform>().FirstOrDefault(t=>t.gameObject.scene==SceneManager.GetActiveScene()&&t.name==name);}
        private static Color Hex(string value){ColorUtility.TryParseHtmlString("#"+value,out var result);return result;}
        private static T Ensure<T>(GameObject target) where T:Component {var component=target.GetComponent<T>();return component!=null?component:target.AddComponent<T>();}
    }
}
