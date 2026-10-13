using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Team5.qPCR;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using Object = UnityEngine.Object;

namespace Team5.qPCR.Editor
{
    /// <summary>
    /// Adds the narrated, object-driven VR lesson to the existing realistic laboratory.
    /// Generated objects are marked and replaced so repeated runs remain idempotent.
    /// </summary>
    public static class Team5InteractiveVrUpgrade
    {
        public const string ScenePath = "Assets/Team5/Scenes/Team5_qPCR_RealisticLab.unity";
        private const string RootPath = "Assets/Team5";
        private const string GeneratedRootName = "INTERACTIVE_VR_LESSON";

        private static readonly Color Navy = Hex("143361");
        private static readonly Color Blue = Hex("1D5FD0");
        private static readonly Color PaleBlue = Hex("EEF5FF");
        private static readonly Color Border = Hex("CADBED");
        private static readonly Color Green = Hex("299B59");
        private static readonly Color Amber = Hex("D88B18");
        private static readonly Color Red = Hex("D93643");
        private static TMP_FontAsset regular;
        private static TMP_FontAsset semibold;
        private static Sprite rounded;
        private static Material cueMaterial;
        private static Material gloveMaterial;

        [MenuItem("Team 5/Apply complete interactive VR upgrade")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Exit Play mode before applying the interactive VR upgrade.");

            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (scene.isDirty && !string.IsNullOrEmpty(scene.path))
                    throw new InvalidOperationException("Save the current scene before opening the Team 5 scene.");
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            BackupScene(scene);
            LoadStyleAssets();

            CleanupGeneratedArtifacts();

            var generated = new GameObject(GeneratedRootName).transform;
            var workflow = Require<WorkflowController>();
            var plate = Require<PlateController>();
            var instrument = Require<InstrumentController>();
            var protocol = Require<ProtocolSetupController>();
            var results = Require<ResultsController>();
            var shell = Require<LabShellController>();

            DisableLegacyProgressButtons();

            var lesson = generated.gameObject.AddComponent<GuidedLessonController>();
            var narration = generated.gameObject.AddComponent<NarrationController>();
            var narrationSource = generated.gameObject.AddComponent<AudioSource>();
            narrationSource.playOnAwake = false;
            narrationSource.loop = false;
            narrationSource.spatialBlend = 0f;
            narrationSource.volume = .82f;
            var guidance = generated.gameObject.AddComponent<GuidanceCueController>();
            var launcher = generated.gameObject.AddComponent<PresentationLauncherController>();

            var desktopCanvas = RequireTransform("COMPACT_DESKTOP_AND_XR_UI");
            var xrCanvas = RequireTransform("XR_WORLD_SPACE_LEARNING_UI");
            var desktopHud = BuildLessonHud(desktopCanvas, false, lesson, narration, launcher);
            var xrHud = BuildLessonHud(xrCanvas, true, lesson, narration, launcher);

            var descriptors = BuildDescriptors(generated, lesson, instrument, plate);
            var cueArrow = BuildCueArrow(generated);
            var plateSocket = BuildPlateInteraction(generated, lesson, workflow, plate, instrument, descriptors);
            lesson.SetPlateSocket(plateSocket);

            var steps = BuildSteps();
            lesson.Configure(workflow, plate, instrument, protocol, results, narration, guidance, steps,
                new[] { desktopHud.Mode, xrHud.Mode }, new[] { desktopHud.StepTitle, xrHud.StepTitle },
                new[] { desktopHud.Target, xrHud.Target }, new[] { desktopHud.Instruction, xrHud.Instruction },
                new[] { desktopHud.Feedback, xrHud.Feedback }, new[] { desktopHud.Timer, xrHud.Timer },
                new[] { desktopHud.Report, xrHud.Report }, new[] { desktopHud.ModePanel, xrHud.ModePanel },
                new[] { desktopHud.CompletionPanel, xrHud.CompletionPanel });

            var desktopProtocol = RequireTransform("Machine_Protocol_Touchscreen").gameObject;
            var xrProtocol = RequireTransform("XR_Protocol_Touchscreen").gameObject;
            var desktopResults = FindResultPanel(desktopCanvas).gameObject;
            var xrResults = FindResultPanel(xrCanvas).gameObject;
            lesson.SetContextPanels(new[] { desktopProtocol, xrProtocol }, new[] { desktopResults, xrResults });
            AddProtocolValidationButton(desktopProtocol.transform, lesson, false, descriptors);
            AddProtocolValidationButton(xrProtocol.transform, lesson, true, descriptors);
            AddResultsInterpretationButtons(desktopResults.transform, lesson, false, descriptors);
            AddResultsInterpretationButtons(xrResults.transform, lesson, true, descriptors);
            guidance.Configure(cueArrow, desktopHud.Hint, descriptors.ToArray());

            var cues = BuildNarrationCues(steps);
            narration.Configure(narrationSource,
                new[] { desktopHud.CaptionTitle, xrHud.CaptionTitle },
                new[] { desktopHud.Caption, xrHud.Caption },
                new[] { desktopHud.Volume, xrHud.Volume },
                new[] { desktopHud.Mute, xrHud.Mute },
                new[] { desktopHud.Replay, xrHud.Replay }, cues);

            launcher.Configure(shell, lesson, new[] { desktopHud.SimulatorHelp, xrHud.SimulatorHelp },
                new[] { desktopHud.PreviewStatus, xrHud.PreviewStatus },
                new[] { desktopHud.DesktopPreview, xrHud.DesktopPreview },
                new[] { desktopHud.XrPreview, xrHud.XrPreview });
            WireHudButtons(desktopHud, lesson, narration, launcher);
            WireHudButtons(xrHud, lesson, narration, launcher);

            AddHandsAndControllerFallback();
            EnsureXrCameraIsMain();
            EnsureModeSpecificAudioListeners();

            foreach (var item in new Object[] { lesson, narration, guidance, launcher, shell, plate, instrument, protocol })
                EditorUtility.SetDirty(item);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Debug.Log("[Team5 Interactive VR] Narrated Guided/Assessment lesson, physical controls, plate socket, labels, haptics and simulator launcher are ready.");
        }

        public static void ScheduleApply()
        {
            EditorApplication.delayCall += Apply;
        }

        private static void BackupScene(Scene scene)
        {
            var folder = Path.GetFullPath("../deliverables/Team5-qPCR/Backups/2026-10-06-interactive-vr");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "Team5_qPCR_RealisticLab-before-interactive.unity");
            if (!File.Exists(path)) EditorSceneManager.SaveScene(scene, path, true);
        }

        private static void LoadStyleAssets()
        {
            regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RootPath + "/UI/Fonts/SourceSans3-Regular SDF.asset");
            semibold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RootPath + "/UI/Fonts/SourceSans3-Semibold SDF.asset");
            rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RootPath + "/UI/RoundedPanel.asset");
            if (regular == null || semibold == null || rounded == null)
                throw new InvalidOperationException("The Source Sans 3 fonts and rounded UI sprite are required.");
            cueMaterial = EnsureMaterial("M_LessonCue", Blue, true);
            gloveMaterial = EnsureMaterial("M_BlueNitrileGlove", Hex("2C6FD6"), false);
        }

        private static void CleanupGeneratedArtifacts()
        {
            var markers = Object.FindObjectsByType<InteractiveVrGeneratedArtifact>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(marker => marker != null)
                .ToArray();
            foreach (var marker in markers.Where(candidate =>
                         !candidate.GetComponentsInParent<InteractiveVrGeneratedArtifact>(true)
                             .Any(parent => parent != candidate)))
            {
                if (marker != null) Object.DestroyImmediate(marker.gameObject);
            }

            var legacyNames = new HashSet<string>
            {
                GeneratedRootName,
                "DESKTOP_INTERACTIVE_LESSON_UI",
                "XR_INTERACTIVE_LESSON_UI",
                "PHYSICAL_MACHINE_CONTROLS",
                "A1_ORIENTATION_SENSITIVE_PLATE_SOCKET",
                "Seat_Plate_Guidance_Target",
                "Tour_PreparedPlate_Target",
                "Tour_OpticalSeal_Target",
                "Tour_Instrument_Target",
                "Tour_Touchscreen_Target",
                "Tour_Drawer_Target",
                "Tour_ThermalBlock_Target",
                "Inspect_PlateId_Target",
                "Inspect_Seal_Target",
                "Inspect_Bubbles_Target",
                "Inspect_A1_Target",
                "Validate_Protocol",
                "Controls_Pass",
                "Controls_Fail",
                "Left_Blue_Gloved_Hand",
                "Right_Blue_Gloved_Hand",
                "TRACKED_HAND_INTERACTION_GROUPS"
            };
            foreach (var target in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                         .Where(item => item != null && legacyNames.Contains(item.name))
                         .OrderByDescending(item => GetDepth(item))
                         .ToArray())
            {
                if (target != null) Object.DestroyImmediate(target.gameObject);
            }

            foreach (var descriptor in Object.FindObjectsByType<LabObjectDescriptor>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None)
                         .Where(item => item != null &&
                                        (item.Action == TrainingAction.ValidateProtocol ||
                                         item.Action == TrainingAction.InterpretControlsPassed))
                         .ToArray())
            {
                Object.DestroyImmediate(descriptor);
            }

            foreach (var panelName in new[]
                     {
                         "Machine_Protocol_Touchscreen", "XR_Protocol_Touchscreen", "Dedicated_Results_Dialogue"
                     })
            {
                foreach (var panel in FindAll(panelName))
                {
                    foreach (var label in panel.GetComponentsInChildren<Transform>(true)
                                 .Where(item => item.name == "Floating_Label").ToArray())
                        Object.DestroyImmediate(label.gameObject);
                }
            }
        }

        private static int GetDepth(Transform target)
        {
            var depth = 0;
            while (target != null)
            {
                depth++;
                target = target.parent;
            }
            return depth;
        }

        private static T MarkGenerated<T>(T component) where T : Component
        {
            Ensure<InteractiveVrGeneratedArtifact>(component.gameObject);
            return component;
        }

        private static void DisableLegacyProgressButtons()
        {
            foreach (var name in new[] { "Reusable_Mentor_Dialogue", "XR_Mentor_Card" })
            {
                var target = Find(name);
                if (target == null) continue;
                var controller = target.GetComponent<MentorPanelController>();
                if (controller != null) controller.enabled = true;
                target.gameObject.SetActive(true);
                var group = Ensure<CanvasGroup>(target.gameObject);
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
        }

        private static List<LabObjectDescriptor> BuildDescriptors(Transform root, GuidedLessonController lesson,
            InstrumentController instrument, PlateController plate)
        {
            var descriptors = new List<LabObjectDescriptor>();
            var targets = new GameObject("LESSON_TARGETS_AND_LABELS").transform;
            targets.SetParent(root, false);

            var coat = Find("Refined_Coat_0") ?? Find("PPE_AND_LAB_COAT_AREA");
            descriptors.Add(DescriptorFromBounds(targets, coat, "Lab coat", "Protects clothing and reduces contamination.", TrainingAction.TourLabCoat, lesson));

            var gloves = BuildGloveDisplay(targets);
            descriptors.Add(DescriptorFromBounds(targets, gloves, "Blue nitrile gloves", "Protect the learner and the prepared reaction plate.", TrainingAction.TourGloves, lesson));

            descriptors.Add(DescriptorFromBounds(targets, Find("Refined_HandwashingSink") ?? Find("Handwashing_Sink"),
                "Handwashing sink", "Used before and after laboratory work.", TrainingAction.TourSink, lesson));

            var plateRoot = plate.transform;
            descriptors.Add(CreateLocalDescriptor(plateRoot, "Tour_PreparedPlate_Target", new Vector3(0f, .018f, -.035f),
                new Vector3(.085f, .012f, .018f), "Prepared qPCR plate", "Team 4 handed over a filled and sealed 96-well plate.", TrainingAction.TourPreparedPlate, lesson, true));
            descriptors.Add(CreateLocalDescriptor(plateRoot, "Tour_OpticalSeal_Target", new Vector3(.025f, .021f, .012f),
                new Vector3(.055f, .009f, .045f), "Optical seal", "The clear seal prevents evaporation while fluorescence is measured.", TrainingAction.TourOpticalSeal, lesson, true));

            var instrumentRoot = instrument.transform;
            descriptors.Add(CreateLocalDescriptor(instrumentRoot, "Tour_Instrument_Target", new Vector3(0f, .39f, .03f),
                new Vector3(.36f, .14f, .28f), "Real-time PCR machine", "Controls temperature cycles and measures fluorescence.", TrainingAction.TourInstrument, lesson, true));
            descriptors.Add(CreateLocalDescriptor(instrumentRoot, "Tour_Touchscreen_Target", new Vector3(0f, .31f, -.246f),
                new Vector3(.22f, .13f, .018f), "Touchscreen", "Where the qPCR protocol is reviewed and validated.", TrainingAction.TourTouchscreen, lesson, true));

            var drawer = RequireTransform("Motorized_Plate_Drawer");
            descriptors.Add(CreateLocalDescriptor(drawer, "Tour_Drawer_Target", Vector3.zero,
                new Vector3(.17f, .035f, .14f), "Motorized drawer", "Moves the plate into the thermal block.", TrainingAction.TourDrawer, lesson, true));
            descriptors.Add(CreateLocalDescriptor(drawer, "Tour_ThermalBlock_Target", new Vector3(0f, .027f, 0f),
                new Vector3(.135f, .018f, .082f), "Thermal block", "Raises and lowers the plate temperature during each cycle.", TrainingAction.TourThermalBlock, lesson, true));

            descriptors.Add(DescriptorFromBounds(targets, Find("Computer_Monitor"), "Results monitor",
                "Displays amplification curves, controls and Cq values.", TrainingAction.TourMonitor, lesson));
            descriptors.Add(DescriptorFromBounds(targets, Find("Refined_Centrifuge") ?? Find("Plate_Centrifuge"), "Plate centrifuge",
                "Brief spinning can collect liquid and remove droplets from the seal.", TrainingAction.TourCentrifuge, lesson));
            descriptors.Add(DescriptorFromBounds(targets, Find("Biohazard_Waste"), "Waste containers",
                "Laboratory waste is separated into the correct labelled container.", TrainingAction.TourWaste, lesson));

            // Four separate, physically selectable inspection checkpoints.
            descriptors.Add(CreateLocalDescriptor(plateRoot, "Inspect_PlateId_Target", new Vector3(0f, .026f, -.041f),
                new Vector3(.073f, .012f, .014f), "Plate ID · T4-QPCR-2026-05", "Confirms that the correct Team 4 plate was handed over.", TrainingAction.InspectPlateId, lesson, true));
            descriptors.Add(CreateLocalDescriptor(plateRoot, "Inspect_Seal_Target", new Vector3(.021f, .027f, .009f),
                new Vector3(.045f, .012f, .035f), "Optical seal · intact", "An intact seal prevents evaporation and cross-contamination.", TrainingAction.InspectOpticalSeal, lesson, true));
            descriptors.Add(CreateLocalDescriptor(plateRoot, "Inspect_Bubbles_Target", new Vector3(-.020f, .027f, -.005f),
                new Vector3(.035f, .012f, .032f), "Bubble check · clear", "Large bubbles can disturb fluorescence readings.", TrainingAction.InspectBubbles, lesson, true));
            descriptors.Add(CreateLocalDescriptor(plateRoot, "Inspect_A1_Target", new Vector3(-.059f, .029f, .038f),
                new Vector3(.016f, .015f, .016f), "A1 orientation marker", "A1 is the reference corner used to load the plate correctly.", TrainingAction.InspectA1Marker, lesson, true));

            BuildPhysicalControls(instrumentRoot, lesson, descriptors);
            return descriptors;
        }

        private static void BuildPhysicalControls(Transform instrument, GuidedLessonController lesson, List<LabObjectDescriptor> descriptors)
        {
            var controls = MarkGenerated(new GameObject("PHYSICAL_MACHINE_CONTROLS").transform);
            controls.SetParent(instrument, false);
            controls.localPosition = Vector3.zero;
            controls.localRotation = Quaternion.identity;
            controls.localScale = Vector3.one;
            descriptors.Add(PhysicalButton(controls, "Power_Button", new Vector3(.174f, .30f, -.253f), .030f,
                Green, "Power", "Turns on the qPCR instrument.", TrainingAction.PowerOnInstrument, lesson));
            descriptors.Add(PhysicalButton(controls, "Drawer_Open_Button", new Vector3(-.066f, .135f, -.254f), .026f,
                Blue, "Open drawer", "Opens the motorized plate drawer.", TrainingAction.OpenDrawer, lesson));
            descriptors.Add(PhysicalButton(controls, "Drawer_Close_Button", new Vector3(0f, .135f, -.254f), .026f,
                Amber, "Close drawer", "Closes the drawer only after the plate is seated.", TrainingAction.CloseDrawer, lesson));
            descriptors.Add(PhysicalButton(controls, "Start_Run_Button", new Vector3(.066f, .135f, -.254f), .026f,
                Green, "Start run", "Starts the validated 35-cycle qPCR run.", TrainingAction.StartRun, lesson));
        }

        private static LabObjectDescriptor PhysicalButton(Transform parent, string name, Vector3 localPosition, float size,
            Color color, string displayName, string description, TrainingAction action, GuidedLessonController lesson)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            target.name = name;
            target.transform.SetParent(parent, false);
            target.transform.localPosition = localPosition;
            target.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            target.transform.localScale = new Vector3(size, .006f, size);
            var material = EnsureMaterial("M_" + name, color, true);
            target.GetComponent<Renderer>().sharedMaterial = material;
            var interactable = target.AddComponent<XRSimpleInteractable>();
            var label = BuildWorldLabel(target.transform, displayName, Vector3.up * .052f);
            var outline = BuildCircleOutline(target.transform, size * .62f);
            var descriptor = target.AddComponent<LabObjectDescriptor>();
            descriptor.Configure(displayName, description, action, lesson, interactable, outline, target.transform,
                label.Group, label.Text, false);
            target.AddComponent<PhysicalControlInteractable>().Configure(action, lesson, interactable, target.transform, descriptor);
            return descriptor;
        }

        private static OrientationAwarePlateSocket BuildPlateInteraction(Transform generated, GuidedLessonController lesson,
            WorkflowController workflow, PlateController plate, InstrumentController instrument, List<LabObjectDescriptor> descriptors)
        {
            var plateObject = plate.gameObject;
            var rigidbody = Ensure<Rigidbody>(plateObject);
            rigidbody.mass = .04f;
            rigidbody.useGravity = true;
            rigidbody.isKinematic = true;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var grab = Ensure<XRGrabInteractable>(plateObject);
            var plateCollider = Ensure<BoxCollider>(plateObject);
            plateCollider.center = new Vector3(0,.008f,0);
            plateCollider.size = new Vector3(.12776f,.018f,.08548f);
            plateCollider.enabled = true;
            grab.colliders.Clear();
            grab.colliders.Add(plateCollider);
            grab.throwOnDetach = false;
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.useDynamicAttach = true;
            grab.enabled = false;
            var earlyAttemptBlocker = Ensure<XRSimpleInteractable>(plateObject);
            earlyAttemptBlocker.enabled = true;
            earlyAttemptBlocker.colliders.Clear();
            earlyAttemptBlocker.colliders.Add(plateCollider);
            var plateFeedback = descriptors.FirstOrDefault(item => item.Action == TrainingAction.TourPreparedPlate);
            Ensure<LessonPlateGrabGate>(plateObject).Configure(lesson, workflow, grab, earlyAttemptBlocker, plateFeedback);
            Ensure<PlateDropRecovery>(plateObject).Configure(RequireTransform("Prepared_Plate_Home_Anchor"), instrument, grab, -.35f);

            var drawer = RequireTransform("Motorized_Plate_Drawer");
            var anchor = RequireTransform("A1_ALIGNED_PLATE_ANCHOR");
            var socketObject = new GameObject("A1_ORIENTATION_SENSITIVE_PLATE_SOCKET");
            MarkGenerated(socketObject.transform);
            socketObject.transform.SetParent(drawer, false);
            socketObject.transform.localPosition = anchor.localPosition;
            socketObject.transform.localRotation = anchor.localRotation;
            var trigger = socketObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(.145f, .055f, .105f);
            var descriptor = CreateLocalDescriptor(drawer, "Seat_Plate_Guidance_Target", anchor.localPosition + Vector3.up * .025f,
                new Vector3(.145f, .035f, .105f), "Plate socket · align A1", "The plate only snaps in when A1 faces the matching corner.", TrainingAction.SeatPlate, lesson, false);
            descriptors.Add(descriptor);
            var socket = socketObject.AddComponent<OrientationAwarePlateSocket>();
            socket.Configure(plate, instrument, lesson, anchor, descriptor);
            return socket;
        }

        private static void AddProtocolValidationButton(Transform panel, GuidedLessonController lesson, bool xr,
            List<LabObjectDescriptor> descriptors)
        {
            var button = UiButton("Validate_Protocol", panel, "Validate protocol", Blue);
            MarkGenerated(button.transform);
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(.68f, .015f);
            rect.anchorMax = new Vector2(.96f, xr ? .085f : .075f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            button.gameObject.AddComponent<LessonUiActionButton>().Configure(lesson, TrainingAction.ValidateProtocol, button);

            var descriptor = CreatePanelDescriptor(panel, "Protocol_Validation_Target", "Protocol validation",
                "All required values must match the teaching protocol.", TrainingAction.ValidateProtocol, lesson);
            descriptors.Add(descriptor);
        }

        private static void AddResultsInterpretationButtons(Transform panel, GuidedLessonController lesson, bool xr,
            List<LabObjectDescriptor> descriptors)
        {
            var pass = UiButton("Controls_Pass", panel, "Positive control amplified · NTC stayed flat", Green);
            MarkGenerated(pass.transform);
            var passRect = pass.GetComponent<RectTransform>();
            passRect.anchorMin = new Vector2(.04f, .008f);
            passRect.anchorMax = new Vector2(.70f, xr ? .065f : .055f);
            passRect.offsetMin = passRect.offsetMax = Vector2.zero;
            pass.gameObject.AddComponent<LessonUiActionButton>().Configure(lesson, TrainingAction.InterpretControlsPassed, pass);

            var fail = UiButton("Controls_Fail", panel, "Controls failed", Red);
            MarkGenerated(fail.transform);
            var failRect = fail.GetComponent<RectTransform>();
            failRect.anchorMin = new Vector2(.72f, .008f);
            failRect.anchorMax = new Vector2(.96f, xr ? .065f : .055f);
            failRect.offsetMin = failRect.offsetMax = Vector2.zero;
            fail.gameObject.AddComponent<LessonUiActionButton>().Configure(lesson, TrainingAction.InterpretControlsFailed, fail);

            var descriptor = CreatePanelDescriptor(panel, "Control_Interpretation_Target", "Interpret the controls",
                "The positive control must amplify and the NTC must remain flat.",
                TrainingAction.InterpretControlsPassed, lesson);
            descriptors.Add(descriptor);
        }

        private static HudReferences BuildLessonHud(Transform canvas, bool xr, GuidedLessonController lesson,
            NarrationController narration, PresentationLauncherController launcher)
        {
            var root = UiPanel(xr ? "XR_INTERACTIVE_LESSON_UI" : "DESKTOP_INTERACTIVE_LESSON_UI", canvas, Color.clear);
            MarkGenerated(root);
            Stretch(root);
            root.GetComponent<Image>().raycastTarget = false;

            var card = UiPanel("Lesson_Instruction_Card", root, Color.white);
            var cardRect = (RectTransform)card;
            if (xr)
            {
                cardRect.anchorMin = new Vector2(.01f, .03f);
                cardRect.anchorMax = new Vector2(.45f, .56f);
            }
            else
            {
                cardRect.anchorMin = new Vector2(0f, 0f);
                cardRect.anchorMax = new Vector2(0f, 0f);
                cardRect.anchoredPosition = new Vector2(276f, 180f);
                cardRect.sizeDelta = new Vector2(520f, 320f);
            }
            AddOutline(card.gameObject);

            var mode = UiText("Lesson_Mode", card, "Choose a mode", xr ? 19 : 15, true, Blue);
            SetRect(mode.rectTransform, .05f, .91f, .48f, .98f);
            var timer = UiText("Session_Timer", card, "00:00   Mistakes 0   Hints 0", xr ? 18 : 14, false, Hex("5B6C82"));
            SetRect(timer.rectTransform, .48f, .91f, .96f, .98f, TextAlignmentOptions.Right);
            var title = UiText("Lesson_Step_Title", card, "IGH Genomics Training Lab", xr ? 30 : 26, true, Navy);
            SetRect(title.rectTransform, .05f, .76f, .96f, .91f);
            var target = UiText("Current_Target", card, "Current target: Choose a lesson mode", xr ? 19 : 15, true, Blue);
            target.textWrappingMode = TextWrappingModes.Normal;
            SetRect(target.rectTransform, .05f, .68f, .96f, .76f, TextAlignmentOptions.TopLeft);
            var instruction = UiText("Lesson_Instruction", card,
                "Choose Guided Training for narration and cues, or Assessment Mode for minimal help.", xr ? 21 : 17, false, Navy);
            instruction.textWrappingMode = TextWrappingModes.Normal;
            SetRect(instruction.rectTransform, .05f, .46f, .96f, .68f, TextAlignmentOptions.TopLeft);
            var captionTitle = UiText("Caption_Title", card, "Narration", xr ? 18 : 14, true, Blue);
            SetRect(captionTitle.rectTransform, .05f, .38f, .30f, .46f);
            var caption = UiText("Narration_Caption", card, "Temporary computer narration is matched to this caption.", xr ? 18 : 14, false, Hex("4C6078"));
            caption.textWrappingMode = TextWrappingModes.Normal;
            SetRect(caption.rectTransform, .28f, .29f, .96f, .46f, TextAlignmentOptions.TopLeft);
            var hint = UiText("Timed_Hint", card, "", xr ? 18 : 14, true, Amber);
            hint.textWrappingMode = TextWrappingModes.Normal;
            SetRect(hint.rectTransform, .05f, .21f, .96f, .29f);
            var feedback = UiText("Action_Feedback", card, "", xr ? 18 : 14, true, Red);
            feedback.textWrappingMode = TextWrappingModes.Normal;
            SetRect(feedback.rectTransform, .05f, .13f, .96f, .21f);

            var help = UiButton("Help", card, "Help", PaleBlue);
            SetRect((RectTransform)help.transform, .05f, .02f, .19f, .12f);
            var replay = UiButton("Replay", card, "Replay", PaleBlue);
            SetRect((RectTransform)replay.transform, .20f, .02f, .36f, .12f);
            var mute = UiToggle("Mute", card, "Mute");
            SetRect((RectTransform)mute.transform, .37f, .02f, .54f, .12f);
            var reset = UiButton("Reset", card, "Reset lesson", PaleBlue);
            SetRect((RectTransform)reset.transform, .55f, .02f, .75f, .12f);
            var volume = UiSlider("Narration_Volume", card);
            SetRect((RectTransform)volume.transform, .77f, .045f, .96f, .105f);
            volume.minValue = 0f;
            volume.maxValue = 1f;
            volume.value = .82f;

            var modePanel = UiPanel("Lesson_Mode_Selection", root, Color.white);
            if (xr) SetRect((RectTransform)modePanel, .25f, .53f, .75f, .83f);
            else
            {
                var modeRect = (RectTransform)modePanel;
                modeRect.anchorMin = modeRect.anchorMax = new Vector2(.5f, .5f);
                modeRect.sizeDelta = new Vector2(620f, 260f);
                modeRect.anchoredPosition = new Vector2(0f, 40f);
            }
            AddOutline(modePanel.gameObject);
            Ensure<CanvasGroup>(modePanel.gameObject);
            var welcome = UiText("Mode_Heading", modePanel, "IGH Genomics Training Lab", xr ? 34 : 30, true, Navy);
            SetRect(welcome.rectTransform, .06f, .69f, .94f, .94f, TextAlignmentOptions.Center);
            var intro = UiText("Mode_Description", modePanel,
                "Load and run Team 4's prepared qPCR plate. Every scientific step is completed on the real laboratory object.", xr ? 21 : 17, false, Hex("4C6078"));
            intro.textWrappingMode = TextWrappingModes.Normal;
            SetRect(intro.rectTransform, .08f, .40f, .92f, .70f, TextAlignmentOptions.Center);
            var guided = UiButton("Start_Guided", modePanel, "Guided Training", Blue);
            SetRect((RectTransform)guided.transform, .08f, .12f, .47f, .34f);
            var assessment = UiButton("Start_Assessment", modePanel, "Assessment Mode", PaleBlue);
            SetRect((RectTransform)assessment.transform, .53f, .12f, .92f, .34f);

            var completion = UiPanel("Lesson_Completion_Report", root, Color.white);
            if (xr) SetRect((RectTransform)completion, .29f, .54f, .71f, .84f);
            else
            {
                var finishRect = (RectTransform)completion;
                finishRect.anchorMin = finishRect.anchorMax = new Vector2(.5f, .5f);
                finishRect.sizeDelta = new Vector2(520f, 280f);
            }
            AddOutline(completion.gameObject);
            var completeTitle = UiText("Completion_Heading", completion, "Lesson complete", xr ? 34 : 30, true, Green);
            SetRect(completeTitle.rectTransform, .07f, .72f, .93f, .93f, TextAlignmentOptions.Center);
            var report = UiText("Session_Report", completion, "", xr ? 22 : 18, false, Navy);
            SetRect(report.rectTransform, .10f, .26f, .90f, .72f, TextAlignmentOptions.Center);
            var again = UiButton("Reset_After_Completion", completion, "Practise again", Blue);
            SetRect((RectTransform)again.transform, .28f, .08f, .72f, .24f);
            completion.gameObject.SetActive(false);

            var launcherPanel = UiPanel("Presentation_Launcher", root, Color.white);
            if (xr) SetRect((RectTransform)launcherPanel, .47f, .86f, .99f, .98f);
            else SetRect((RectTransform)launcherPanel, .48f, .86f, .99f, .98f);
            AddOutline(launcherPanel.gameObject);
            var desktop = UiButton("Desktop_Preview", launcherPanel, "Desktop Preview", PaleBlue);
            SetRect((RectTransform)desktop.transform, .015f, .44f, .23f, .92f);
            var simulator = UiButton("XR_Simulator_Preview", launcherPanel, "XR Simulator Preview", Blue);
            SetRect((RectTransform)simulator.transform, .24f, .44f, .52f, .92f);
            var resetLauncher = UiButton("Launcher_Reset", launcherPanel, "Reset Lesson", PaleBlue);
            SetRect((RectTransform)resetLauncher.transform, .53f, .44f, .72f, .92f);
            var controls = UiButton("Show_Simulator_Controls", launcherPanel, "Simulator Controls", PaleBlue);
            SetRect((RectTransform)controls.transform, .73f, .44f, .985f, .92f);
            var previewStatus = UiText("Preview_Status", launcherPanel,
                xr ? "XR Simulator Preview · XR Origin camera and simulated blue-gloved hands" : "Desktop Preview · use this mode for editing and fallback demos",
                xr ? 17 : 13, false, Hex("4C6078"));
            SetRect(previewStatus.rectTransform, .02f, .04f, .98f, .41f, TextAlignmentOptions.Center);

            var simulatorHelp = UiPanel("Simulator_Control_Guide", root, Color.white);
            if (xr) SetRect((RectTransform)simulatorHelp, .56f, .16f, .985f, .72f);
            else
            {
                var helpRect = (RectTransform)simulatorHelp;
                helpRect.anchorMin = helpRect.anchorMax = new Vector2(1f, .5f);
                helpRect.pivot = new Vector2(1f, .5f);
                helpRect.anchoredPosition = new Vector2(-22f, 0f);
                helpRect.sizeDelta = new Vector2(450f, 470f);
            }
            AddOutline(simulatorHelp.gameObject);
            var helpTitle = UiText("Simulator_Guide_Title", simulatorHelp, "XR Simulator controls", xr ? 30 : 26, true, Navy);
            SetRect(helpTitle.rectTransform, .07f, .82f, .93f, .95f);
            var helpText = UiText("Simulator_Guide_Text", simulatorHelp,
                "WASD/QE       Move selected simulated device\nH             Manipulate the head\n[ and ]       Select left/right controller\nRight mouse   Rotate\nG             Select / grip 3D objects\nT             UI trigger / activate\nTab           Cycle controllers/hands\nK             Grab hand pose\nM             Pinch hand pose\nN             Poke hand pose\nO             Open hand\nP             Fist\nR             Reset simulator pose",
                xr ? 20 : 17, false, Navy);
            helpText.textWrappingMode = TextWrappingModes.NoWrap;
            SetRect(helpText.rectTransform, .08f, .12f, .92f, .80f, TextAlignmentOptions.TopLeft);
            var closeHelp = UiButton("Hide_Simulator_Controls", simulatorHelp, "Close", PaleBlue);
            SetRect((RectTransform)closeHelp.transform, .66f, .035f, .92f, .12f);
            simulatorHelp.gameObject.SetActive(false);

            return new HudReferences
            {
                Root = root.gameObject, ModePanel = modePanel.gameObject, CompletionPanel = completion.gameObject,
                Mode = mode, StepTitle = title, Target = target, Instruction = instruction, Feedback = feedback,
                Timer = timer, Report = report,
                CaptionTitle = captionTitle, Caption = caption, Hint = hint, Volume = volume, Mute = mute,
                Replay = replay, Help = help, Reset = reset, StartGuided = guided, StartAssessment = assessment,
                ResetAfterComplete = again, DesktopPreview = desktop, XrPreview = simulator,
                LauncherReset = resetLauncher, ShowControls = controls, HideControls = closeHelp,
                SimulatorHelp = simulatorHelp.gameObject, PreviewStatus = previewStatus
            };
        }

        private static void WireHudButtons(HudReferences hud, GuidedLessonController lesson,
            NarrationController narration, PresentationLauncherController launcher)
        {
            AddListener(hud.StartGuided, lesson.StartGuidedTraining);
            AddListener(hud.StartAssessment, lesson.StartAssessment);
            AddListener(hud.Help, lesson.RequestHelp);
            AddListener(hud.Reset, lesson.ResetLesson);
            AddListener(hud.ResetAfterComplete, lesson.ResetLesson);
            AddListener(hud.DesktopPreview, launcher.DesktopPreview);
            AddListener(hud.XrPreview, launcher.XrSimulatorPreview);
            AddListener(hud.LauncherReset, launcher.ResetLesson);
            AddListener(hud.ShowControls, launcher.ShowSimulatorControls);
            AddListener(hud.HideControls, launcher.HideSimulatorControls);
            AddListener(hud.Replay, narration.Replay);
            hud.Mute.onValueChanged.RemoveAllListeners();
            hud.Volume.onValueChanged.RemoveAllListeners();
        }

        private static LessonStepDefinition[] BuildSteps() => new[]
        {
            Step(TrainingAction.TourLabCoat, "Lab coat", "Welcome to IGH Genomics", "Point at and select the lab coat.", "The lab coat is part of personal protective equipment."),
            Step(TrainingAction.TourGloves, "Blue nitrile gloves", "Protect your hands", "Select the blue nitrile gloves.", "Clean gloves reduce contamination and protect the learner."),
            Step(TrainingAction.TourSink, "Handwashing sink", "Handwashing area", "Select the laboratory sink.", "Hand hygiene comes before and after laboratory work."),
            Step(TrainingAction.TourPreparedPlate, "Prepared qPCR plate", "Team 4 handoff", "Select the prepared qPCR plate.", "Team 5 receives a filled, mapped and sealed plate; it does not remix the reactions."),
            Step(TrainingAction.TourOpticalSeal, "Optical seal", "Optical seal", "Select the transparent seal on top of the plate.", "The instrument reads fluorescence through this seal."),
            Step(TrainingAction.TourInstrument, "Real-time PCR machine", "qPCR instrument", "Select the real-time PCR machine.", "It changes temperature and measures fluorescence during the run."),
            Step(TrainingAction.TourTouchscreen, "Instrument touchscreen", "Instrument touchscreen", "Select the touchscreen.", "This is where the protocol is checked before loading."),
            Step(TrainingAction.TourDrawer, "Motorized drawer", "Motorized drawer", "Select the plate drawer.", "The drawer carries the plate into the instrument."),
            Step(TrainingAction.TourThermalBlock, "Thermal block", "Thermal block", "Select the thermal block inside the drawer.", "The block performs denaturation, annealing and extension temperatures."),
            Step(TrainingAction.TourMonitor, "Results monitor", "Results monitor", "Select the monitor.", "Amplification curves and control results appear here."),
            Step(TrainingAction.TourCentrifuge, "Plate centrifuge", "Plate centrifuge", "Select the plate centrifuge.", "It is background equipment in Team 5, but explains how liquid is collected before handoff."),
            Step(TrainingAction.TourWaste, "Biohazard waste container", "Waste containers", "Select the biohazard waste container to finish the lab tour.", "Correct waste separation is part of safe laboratory work."),
            Step(TrainingAction.PowerOnInstrument, "Power button", "Power on", "Physically press the green Power button on the machine.", "The instrument must be powered before its controls unlock."),
            Step(TrainingAction.ValidateProtocol, "Validate protocol button", "Configure the qPCR run", "Review the touchscreen values, correct any red field, then press Validate protocol.", "This teaching programme uses 35 cycles and fluorescence acquisition at 60 degrees Celsius."),
            Step(TrainingAction.InspectPlateId, "Plate ID", "Check the plate ID", "Select the plate ID label.", "The ID confirms the plate belongs to this run."),
            Step(TrainingAction.InspectOpticalSeal, "Optical seal checkpoint", "Inspect the seal", "Select the optical seal checkpoint.", "The seal must be intact and firmly attached."),
            Step(TrainingAction.InspectBubbles, "Bubble-check area", "Inspect for bubbles", "Select the bubble-check area.", "Large bubbles can disturb the fluorescence reading."),
            Step(TrainingAction.InspectA1Marker, "A1 marker", "Find A1", "Select the A1 marker at the reference corner.", "A1 tells you which way the plate must face."),
            Step(TrainingAction.OpenDrawer, "Drawer-open button", "Open the drawer", "Press the blue drawer-open button.", "The motorized drawer exposes the loading socket."),
            Step(TrainingAction.SeatPlate, "Prepared qPCR plate", "Load the plate", "Pinch or grip the plate, align A1, and place it in the socket.", "Wrongly rotated plates are rejected and cannot snap into place."),
            Step(TrainingAction.CloseDrawer, "Drawer-close button", "Close the drawer", "Press the amber drawer-close button.", "The drawer only closes after the plate is correctly seated."),
            Step(TrainingAction.StartRun, "Start button", "Start amplification", "Press the green Start button.", "Final scientific validation runs before the 35-cycle simulation begins."),
            Step(TrainingAction.ObserveAmplification, "Amplification display", "Observe 35 cycles", "Watch the temperature, cycle counter and amplification curves build.", "The educational run is compressed to about 30 seconds."),
            Step(TrainingAction.InterpretControlsPassed, "Control interpretation", "Interpret the controls", "Choose the statement that correctly describes the positive control and NTC.", "The positive control must amplify. The no-template control must stay flat."),
        };

        private static LessonStepDefinition Step(TrainingAction action, string target, string title, string instruction, string explanation) =>
            new LessonStepDefinition(action, target, title, instruction, explanation);

        private static NarrationCue[] BuildNarrationCues(IEnumerable<LessonStepDefinition> steps)
        {
            var list = steps.Select(step => new NarrationCue
            {
                Key = step.NarrationKey,
                Caption = step.Instruction + " " + step.Explanation,
                Clip = AssetDatabase.LoadAssetAtPath<AudioClip>(RootPath + "/Audio/Narration/" + step.NarrationKey + ".wav")
            }).ToList();
            list.Add(new NarrationCue
            {
                Key = "Complete",
                Caption = "Lesson complete. Review your time, mistakes and requested hints.",
                Clip = AssetDatabase.LoadAssetAtPath<AudioClip>(RootPath + "/Audio/Narration/Complete.wav")
            });
            return list.ToArray();
        }

        private static void AddHandsAndControllerFallback()
        {
            AddBlueGlovedControllerHands();

            const string prefabPath =
                "Assets/Samples/XR Interaction Toolkit/3.3.2/Hands Interaction Demo/Prefabs/XR Origin Hands (XR Rig).prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                throw new InvalidOperationException("Import the XRI Hands Interaction Demo sample before building the hand rig.");

            var origin = RequireTransform("XR_ORIGIN_LEFT_RIGHT_CONTROLLERS");
            var generatedHands = MarkGenerated(new GameObject("TRACKED_HAND_INTERACTION_GROUPS").transform);
            generatedHands.SetParent(origin, false);

            var sourceLeftHand = FindIn(prefab.transform, "Left Hand");
            var sourceRightHand = FindIn(prefab.transform, "Right Hand");
            if (sourceLeftHand == null || sourceRightHand == null)
                throw new InvalidOperationException("The XRI hands sample does not contain both hand interactor groups.");

            var sourceActions = prefab.GetComponent<InputActionManager>();
            var destinationActions = Ensure<InputActionManager>(origin.gameObject);
            if (sourceActions != null)
            {
                foreach (var actionAsset in sourceActions.actionAssets)
                    if (actionAsset != null && !destinationActions.actionAssets.Contains(actionAsset))
                        destinationActions.actionAssets.Add(actionAsset);
            }

            var leftHand = Object.Instantiate(sourceLeftHand.gameObject, generatedHands).transform;
            var rightHand = Object.Instantiate(sourceRightHand.gameObject, generatedHands).transform;
            leftHand.name = "Left Tracked Hand";
            rightHand.name = "Right Tracked Hand";

            foreach (var renderer in generatedHands.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                // Preserve the hand sample shader and its affordance properties (including
                // _RimPower), then tint a copied material blue. Replacing it with a plain URP/Lit
                // material causes the hand affordance provider to log an error every frame.
                for (var index = 0; index < materials.Length; index++)
                    materials[index] = EnsureTrackedHandMaterial(materials[index], index);
                renderer.sharedMaterials = materials;
            }

            var modality = Ensure<XRInputModalityManager>(origin.gameObject);
            modality.leftHand = leftHand.gameObject;
            modality.rightHand = rightHand.gameObject;
            modality.leftController = FindIn(origin, "Left Controller")?.gameObject;
            modality.rightController = FindIn(origin, "Right Controller")?.gameObject;
            EditorUtility.SetDirty(modality);
            EditorUtility.SetDirty(destinationActions);
        }

        private static void AddBlueGlovedControllerHands()
        {
            var origin = RequireTransform("XR_ORIGIN_LEFT_RIGHT_CONTROLLERS");
            foreach (var side in new[] { "Left Controller", "Right Controller" })
            {
                var controller = FindIn(origin, side);
                if (controller == null) continue;
                var prior = FindIn(controller, side.StartsWith("Left") ? "Left_Blue_Gloved_Hand" : "Right_Blue_Gloved_Hand");
                if (prior != null) Object.DestroyImmediate(prior.gameObject);
                var hand = new GameObject(side.StartsWith("Left") ? "Left_Blue_Gloved_Hand" : "Right_Blue_Gloved_Hand").transform;
                MarkGenerated(hand);
                hand.SetParent(controller, false);
                hand.localPosition = new Vector3(0f, -.015f, .055f);
                hand.localRotation = Quaternion.Euler(8f, 0f, 0f);
                CreatePrimitive(PrimitiveType.Sphere, "Palm", hand, Vector3.zero, new Vector3(.075f, .035f, .095f), gloveMaterial);
                var sign = side.StartsWith("Left") ? -1f : 1f;
                for (var i = 0; i < 4; i++)
                {
                    var finger = CreatePrimitive(PrimitiveType.Capsule, "Finger_" + i, hand,
                        new Vector3((i - 1.5f) * .017f, .005f, .075f), new Vector3(.012f, .035f + i * .002f, .012f), gloveMaterial);
                    finger.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
                var thumb = CreatePrimitive(PrimitiveType.Capsule, "Thumb", hand, new Vector3(sign * .045f, -.006f, .020f),
                    new Vector3(.015f, .030f, .015f), gloveMaterial);
                thumb.transform.localRotation = Quaternion.Euler(70f, 0f, sign * 38f);
            }
        }

        private static Material EnsureTrackedHandMaterial(Material source, int index)
        {
            if (source == null) return gloveMaterial;
            var safeName = new string(source.name.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
            var path = RootPath + "/Materials/InteractiveVR/M_BlueTrackedHand_" + safeName + "_" + index + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source) { name = "M_BlueTrackedHand_" + safeName };
                AssetDatabase.CreateAsset(material, path);
            }
            var blue = Hex("2C6FD6");
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", blue);
            if (material.HasProperty("_Color")) material.SetColor("_Color", blue);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureXrCameraIsMain()
        {
            var origin = RequireTransform("XR_ORIGIN_LEFT_RIGHT_CONTROLLERS");
            var xrCamera = origin.GetComponentsInChildren<Camera>(true).FirstOrDefault();
            if (xrCamera == null) throw new InvalidOperationException("XR Origin does not contain a camera.");
            xrCamera.tag = "MainCamera";
            xrCamera.nearClipPlane = .05f;
            var desktop = Find("Desktop_Overview_Camera")?.GetComponent<Camera>();
            if (desktop != null) desktop.tag = "MainCamera";
        }

        private static void EnsureModeSpecificAudioListeners()
        {
            var origin = RequireTransform("XR_ORIGIN_LEFT_RIGHT_CONTROLLERS");
            var xrCamera = origin.GetComponentsInChildren<Camera>(true).FirstOrDefault();
            var desktopCamera = Find("Desktop_Overview_Camera")?.GetComponent<Camera>();
            if (desktopCamera != null && desktopCamera.GetComponent<AudioListener>() == null)
                desktopCamera.gameObject.AddComponent<AudioListener>();
            if (xrCamera != null && xrCamera.GetComponent<AudioListener>() == null)
                xrCamera.gameObject.AddComponent<AudioListener>();
        }

        [MenuItem("Team 5/Apply Mode Audio Listeners Only")]
        public static void ApplyModeAudioListenersOnly()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EnsureModeSpecificAudioListeners();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Team 5 Interactive VR] Desktop and XR audio listeners verified.");
        }

        [MenuItem("Team 5/Fix Plate Grab Collider Registration")]
        public static void ApplyPlateGrabColliderOnly()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var plate = Object.FindFirstObjectByType<PlateController>(FindObjectsInactive.Include);
            if (plate == null) throw new InvalidOperationException("PlateController was not found.");
            var grab = plate.GetComponent<XRGrabInteractable>();
            if (grab == null) throw new InvalidOperationException("The prepared plate does not have XRGrabInteractable.");
            var plateCollider = plate.GetComponent<Collider>() ?? plate.gameObject.AddComponent<BoxCollider>();
            grab.colliders.Clear();
            grab.colliders.Add(plateCollider);
            EditorUtility.SetDirty(grab);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Team 5 Interactive VR] Plate grab now uses only its root collider; inspection proxies remain independent interactables.");
        }

        private static LabObjectDescriptor DescriptorFromBounds(Transform parent, Transform target, string displayName,
            string description, TrainingAction action, GuidedLessonController lesson)
        {
            if (target == null) throw new InvalidOperationException("Missing lab target for " + displayName);
            var renderers = target.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
            var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(target.position, Vector3.one * .2f);
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var size = Vector3.Min(bounds.size + Vector3.one * .025f, new Vector3(1.4f, 2.2f, 1.4f));
            size = Vector3.Max(size, Vector3.one * .08f);
            var proxy = new GameObject(action + "_Target");
            proxy.transform.SetParent(parent, false);
            proxy.transform.SetPositionAndRotation(bounds.center, Quaternion.identity);
            var collider = proxy.AddComponent<BoxCollider>();
            collider.size = size;
            var interactable = proxy.AddComponent<XRSimpleInteractable>();
            var line = BuildBoxOutline(proxy.transform, size);
            var label = BuildWorldLabel(proxy.transform, displayName, new Vector3(0f, size.y * .58f + .07f, 0f));
            var descriptor = proxy.AddComponent<LabObjectDescriptor>();
            descriptor.Configure(displayName, description, action, lesson, interactable, line, proxy.transform, label.Group, label.Text);
            return descriptor;
        }

        private static LabObjectDescriptor CreateLocalDescriptor(Transform parent, string name, Vector3 localPosition,
            Vector3 size, string displayName, string description, TrainingAction action, GuidedLessonController lesson, bool actionable)
        {
            var proxy = new GameObject(name);
            MarkGenerated(proxy.transform);
            proxy.transform.SetParent(parent, false);
            proxy.transform.localPosition = localPosition;
            proxy.transform.localRotation = Quaternion.identity;
            var collider = proxy.AddComponent<BoxCollider>();
            collider.size = size;
            var interactable = actionable ? proxy.AddComponent<XRSimpleInteractable>() : null;
            var line = BuildBoxOutline(proxy.transform, size);
            var label = BuildWorldLabel(proxy.transform, displayName, new Vector3(0f, size.y * .65f + .035f, 0f));
            var descriptor = proxy.AddComponent<LabObjectDescriptor>();
            descriptor.Configure(displayName, description, action, lesson, interactable, line, proxy.transform,
                label.Group, label.Text, actionable);
            return descriptor;
        }

        private static LabObjectDescriptor CreatePanelDescriptor(Transform panel, string name, string displayName,
            string description, TrainingAction action, GuidedLessonController lesson)
        {
            var proxy = MarkGenerated(new GameObject(name).transform);
            proxy.SetParent(panel, false);
            var label = BuildWorldLabel(proxy, displayName, Vector3.zero);
            var descriptor = proxy.gameObject.AddComponent<LabObjectDescriptor>();
            descriptor.Configure(displayName, description, action, lesson, null, null, panel,
                label.Group, label.Text, false);
            return descriptor;
        }

        private static Transform BuildGloveDisplay(Transform parent)
        {
            var root = new GameObject("Blue_Nitrile_Glove_Display").transform;
            root.SetParent(parent, false);
            root.position = new Vector3(4.45f, 1.13f, -3.54f);
            CreatePrimitive(PrimitiveType.Cube, "Glove_Box", root, Vector3.zero, new Vector3(.24f, .10f, .14f), EnsureMaterial("M_GloveBox", Color.white, false));
            var hand = CreatePrimitive(PrimitiveType.Sphere, "Glove", root, new Vector3(0f, .075f, -.01f), new Vector3(.12f, .025f, .08f), gloveMaterial);
            hand.transform.localRotation = Quaternion.Euler(0f, 15f, 0f);
            return root;
        }

        private static Transform BuildCueArrow(Transform root)
        {
            var arrow = new GameObject("GUIDANCE_ARROW").transform;
            arrow.SetParent(root, false);
            var shaft = CreatePrimitive(PrimitiveType.Cylinder, "Shaft", arrow, new Vector3(0f, .07f, 0f), new Vector3(.018f, .07f, .018f), cueMaterial);
            var head = CreatePrimitive(PrimitiveType.Cylinder, "Arrowhead", arrow, Vector3.zero, new Vector3(.055f, .05f, .055f), cueMaterial);
            head.transform.localRotation = Quaternion.identity;
            arrow.gameObject.SetActive(false);
            return arrow;
        }

        private static LineRenderer BuildBoxOutline(Transform parent, Vector3 size)
        {
            var go = new GameObject("Guidance_Outline");
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 4;
            line.widthMultiplier = .006f;
            line.sharedMaterial = cueMaterial;
            var x = size.x * .52f;
            var z = size.z * .52f;
            var y = size.y * .52f;
            line.SetPositions(new[] { new Vector3(-x, y, -z), new Vector3(x, y, -z), new Vector3(x, y, z), new Vector3(-x, y, z) });
            line.enabled = false;
            return line;
        }

        private static LineRenderer BuildCircleOutline(Transform parent, float radius)
        {
            var go = new GameObject("Guidance_Outline");
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 24;
            line.widthMultiplier = .004f;
            line.sharedMaterial = cueMaterial;
            for (var i = 0; i < line.positionCount; i++)
            {
                var a = i / (float)line.positionCount * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, .52f, Mathf.Sin(a) * radius));
            }
            line.enabled = false;
            return line;
        }

        private static WorldLabel BuildWorldLabel(Transform parent, string value, Vector3 localPosition)
        {
            var root = new GameObject("Floating_Label", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one * .0012f;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 70;
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(250f, 56f);
            var panel = UiPanel("Label_Background", root.transform, Color.white);
            Stretch(panel);
            AddOutline(panel.gameObject);
            var text = UiText("Label_Text", panel, value, 19f, true, Navy);
            Stretch(text.rectTransform, new Vector2(14f, 7f), new Vector2(-14f, -7f));
            text.alignment = TextAlignmentOptions.Center;
            var group = root.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            root.SetActive(false);
            return new WorldLabel { Group = group, Text = text };
        }

        private static Material EnsureMaterial(string name, Color color, bool emission)
        {
            var folder = RootPath + "/Materials/InteractiveVR";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(Path.GetFullPath(folder));
                AssetDatabase.Refresh();
            }
            var path = folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .32f);
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * .45f);
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material material)
        {
            var target = GameObject.CreatePrimitive(type);
            target.name = name;
            target.transform.SetParent(parent, false);
            target.transform.localPosition = localPosition;
            target.transform.localScale = localScale;
            target.GetComponent<Renderer>().sharedMaterial = material;
            var collider = target.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            return target;
        }

        private static RectTransform FindResultPanel(Transform canvas)
        {
            var panels = canvas.GetComponentsInChildren<RectTransform>(true).Where(item => item.name == "Dedicated_Results_Dialogue").ToArray();
            if (panels.Length == 0) throw new InvalidOperationException("Missing results panel under " + canvas.name);
            return panels[0];
        }

        private static RectTransform UiPanel(string name, Transform parent, Color color)
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            target.transform.SetParent(parent, false);
            var image = target.GetComponent<Image>();
            image.color = color;
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            return target.GetComponent<RectTransform>();
        }

        private static TMP_Text UiText(string name, Transform parent, string value, float size, bool bold, Color color)
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            target.transform.SetParent(parent, false);
            var text = target.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = bold ? semibold : regular;
            text.fontSize = size;
            text.fontStyle = FontStyles.Normal;
            text.color = color;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Left;
            return text;
        }

        private static Button UiButton(string name, Transform parent, string label, Color color)
        {
            var panel = UiPanel(name, parent, color);
            var button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = panel.GetComponent<Image>();
            var text = UiText("Label", panel, label, 16f, true, color == Blue || color == Green || color == Red ? Color.white : Navy);
            Stretch(text.rectTransform, new Vector2(8f, 3f), new Vector2(-8f, -3f));
            text.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static Toggle UiToggle(string name, Transform parent, string label)
        {
            var panel = UiPanel(name, parent, PaleBlue);
            var toggle = panel.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = panel.GetComponent<Image>();
            var check = UiPanel("Checkmark", panel, Blue);
            SetRect(check, .08f, .25f, .25f, .75f);
            toggle.graphic = check.GetComponent<Image>();
            var text = UiText("Label", panel, label, 16f, true, Navy);
            SetRect(text.rectTransform, .29f, 0f, .96f, 1f, TextAlignmentOptions.Center);
            return toggle;
        }

        private static Slider UiSlider(string name, Transform parent)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            var background = UiPanel("Background", root.transform, Border);
            Stretch(background);
            var fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
            fillArea.SetParent(root.transform, false);
            Stretch(fillArea);
            var fill = UiPanel("Fill", fillArea, Blue);
            Stretch(fill);
            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)).GetComponent<RectTransform>();
            handleArea.SetParent(root.transform, false);
            Stretch(handleArea);
            var handle = UiPanel("Handle", handleArea, Blue);
            handle.sizeDelta = new Vector2(18f, 28f);
            var slider = root.GetComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            return slider;
        }

        private static void AddOutline(GameObject target)
        {
            var outline = target.AddComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
        {
            button.onClick.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(button.onClick, action);
        }

        private static void SetRect(RectTransform rect, float xMin, float yMin, float xMax, float yMax,
            TextAlignmentOptions? alignment = null)
        {
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            if (alignment.HasValue && rect.TryGetComponent<TMP_Text>(out var text)) text.alignment = alignment.Value;
        }

        private static void Stretch(RectTransform rect) => Stretch(rect, Vector2.zero, Vector2.zero);
        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static Transform FindOrCreate(string name, Transform parent)
        {
            var found = parent == null ? Find(name) : FindIn(parent, name);
            if (found != null) return found;
            var target = new GameObject(name).transform;
            if (parent != null) target.SetParent(parent, false);
            return target;
        }

        private static Transform Find(string name) => Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(item => item.name == name);

        private static Transform[] FindAll(string name) => Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(item => item.name == name)
            .ToArray();

        private static Transform FindIn(Transform root, string name) => root == null ? null : root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(item => item.name == name);

        private static Transform RequireTransform(string name) => Find(name) ?? throw new InvalidOperationException("Missing scene object: " + name);

        private static T Require<T>() where T : Object => Object.FindFirstObjectByType<T>(FindObjectsInactive.Include) ??
            throw new InvalidOperationException("Missing scene component: " + typeof(T).Name);

        private static T Ensure<T>(GameObject target) where T : Component => target.TryGetComponent<T>(out var component) ? component : target.AddComponent<T>();

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var result);
            return result;
        }

        private sealed class HudReferences
        {
            public GameObject Root, ModePanel, CompletionPanel, SimulatorHelp;
            public TMP_Text Mode, StepTitle, Target, Instruction, Feedback, Timer, Report, CaptionTitle, Caption, Hint, PreviewStatus;
            public Slider Volume;
            public Toggle Mute;
            public Button Replay, Help, Reset, StartGuided, StartAssessment, ResetAfterComplete;
            public Button DesktopPreview, XrPreview, LauncherReset, ShowControls, HideControls;
        }

        private sealed class WorldLabel
        {
            public CanvasGroup Group;
            public TMP_Text Text;
        }
    }
}
