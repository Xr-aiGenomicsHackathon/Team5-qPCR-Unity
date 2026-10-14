#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Team5.qPCR.Editor
{
    public static class QpcrLessonSceneInstaller
    {
        private const string PlatePath = "/40_QPCR_STATION/TEAM4_HANDOFF_PLATE_STATION/Prepared_Plate_Home_Anchor/T4_QPCR_2026_05_PREPARED_PLATE";
        private const string InstrumentPath = "/40_QPCR_STATION/TEAM5_REAL_TIME_QPCR_INSTRUMENT";

        [MenuItem("Team 5/qPCR Lesson/Install Or Refresh In DemoScene")]
        public static void Install()
        {
            if (SceneManager.GetActiveScene().name != "DemoScene")
                throw new InvalidOperationException("Open DemoScene before installing the qPCR lesson.");

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Install VR-first qPCR lesson");
            var undoGroup = Undo.GetCurrentGroup();

            var lessonRoot = Require("/50_LESSON_AND_UI/QPCR_LESSON_SYSTEM");
            var uiRoot = Require("/50_LESSON_AND_UI/QPCR_STATION_UI");
            var audioRoot = Require("/50_LESSON_AND_UI/QPCR_AUDIO");
            var instrument = Require(InstrumentPath);
            var plate = Require(PlatePath);
            var homeAnchor = plate.transform.parent;

            ConfigurePlateFoundation(plate, homeAnchor);
            DisableOldLessonFoundation();

            var lesson = Ensure<QpcrLessonController>(lessonRoot);
            var inspection = Ensure<PlateInspectionController>(plate);
            var hotspots = ConfigureInspectionHotspots(plate, inspection);
            inspection.Configure(lesson, plate.GetComponent<XRGrabInteractable>(), homeAnchor,
                plate.GetComponent<Rigidbody>(), hotspots);

            audioRoot.transform.position = instrument.transform.position;
            var machineAudio = Ensure<QpcrMachineAudio>(audioRoot);
            var audioSources = audioRoot.GetComponents<AudioSource>().ToList();
            while (audioSources.Count < 3)
                audioSources.Add(Undo.AddComponent<AudioSource>(audioRoot));
            machineAudio.Configure(audioSources[0], audioSources[1]);

            var instructionUi = BuildInstructionCanvas(uiRoot.transform);
            var narration = Ensure<NarrationController>(audioRoot);
            narration.Configure(audioSources[2], Array.Empty<TMP_Text>(), Array.Empty<TMP_Text>(),
                Array.Empty<Slider>(), Array.Empty<Toggle>(), new[] { instructionUi.ReplayButton }, BuildNarrationCues());

            var lessonUi = Ensure<QpcrLessonUI>(instructionUi.Canvas.gameObject);
            lessonUi.Configure(lesson, inspection, narration, instructionUi.Step, instructionUi.Title,
                instructionUi.Target, instructionUi.Instruction, instructionUi.Explanation,
                instructionUi.ActionHint, instructionUi.Feedback, instructionUi.Progress,
                instructionUi.StartButton, instructionUi.ConfirmButton, instructionUi.ConfirmLabel,
                instructionUi.ResetButton);

            var screen = BuildInstrumentTouchscreen(instrument.transform);
            var validator = Ensure<QpcrProtocolValidator>(lessonRoot);
            var touchscreen = Ensure<QpcrTouchscreenController>(screen.Canvas.gameObject);
            touchscreen.Configure(lesson, validator, machineAudio, screen.WelcomePanel, screen.ProtocolPanel,
                screen.RunPanel, screen.ResultsPanel, screen.Fields, screen.ActiveInput, screen.Validation,
                screen.ValidateButton, screen.StartRunButton);

            WireProtocolButtons(screen, touchscreen);

            var powerButton = Require(InstrumentPath + "/PHYSICAL_MACHINE_CONTROLS/Power_Button");
            var openButton = Require(InstrumentPath + "/PHYSICAL_MACHINE_CONTROLS/Drawer_Open_Button");
            var closeButton = Require(InstrumentPath + "/PHYSICAL_MACHINE_CONTROLS/Drawer_Close_Button");
            var startButton = Require(InstrumentPath + "/PHYSICAL_MACHINE_CONTROLS/Start_Run_Button");
            DisableOldControl(powerButton);
            DisableOldControl(openButton);
            DisableOldControl(closeButton);
            DisableOldControl(startButton);

            var power = Ensure<QpcrPowerController>(powerButton);
            power.Configure(lesson, powerButton.GetComponent<XRSimpleInteractable>(),
                Require(InstrumentPath + "/Power_Status_LED").GetComponent<Renderer>(), screen.Canvas.gameObject, machineAudio);

            var drawerTransform = Require(InstrumentPath + "/Motorized_Plate_Drawer").transform;
            drawerTransform.localPosition = new Vector3(0f, .11f, -.21f);
            var drawer = Ensure<QpcrDrawerController>(drawerTransform.gameObject);
            drawer.Configure(lesson, power, drawerTransform, openButton.GetComponent<XRSimpleInteractable>(),
                closeButton.GetComponent<XRSimpleInteractable>(), machineAudio);

            var socket = BuildPlateSocket(drawerTransform, lesson, drawer, plate, machineAudio);
            drawer.SetPlateSocket(socket);

            var run = Ensure<QpcrRunController>(lessonRoot);
            run.Configure(lesson, touchscreen, machineAudio, screen.AmplificationGraph, screen.MeltCurve,
                screen.CycleText, screen.StageText, screen.TemperatureText, screen.ProgressFill,
                startButton.GetComponent<XRSimpleInteractable>());

            var results = Ensure<ResultsLessonController>(lessonRoot);
            results.Configure(lesson, machineAudio, screen.Interpretation, screen.ValidControlsButton,
                screen.InvalidControlsButton);

            BuildGuidancePointer(uiRoot.transform, lesson, hotspots, homeAnchor, powerButton.transform,
                openButton.transform, socket.transform, closeButton.transform, screen);

            EditorUtility.SetDirty(lessonRoot);
            EditorUtility.SetDirty(uiRoot);
            EditorUtility.SetDirty(audioRoot);
            EditorUtility.SetDirty(instrument);
            EditorUtility.SetDirty(plate);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("Team 5 VR-first qPCR lesson installed in DemoScene.");
        }

        private static void ConfigurePlateFoundation(GameObject plate, Transform homeAnchor)
        {
            Undo.RecordObject(homeAnchor, "Correct prepared plate home anchor");
            homeAnchor.localPosition = new Vector3(.39f, 0f, -.08f);
            homeAnchor.localRotation = Quaternion.identity;
            homeAnchor.localScale = Vector3.one;

            Undo.RecordObject(plate.transform, "Reset prepared plate transform");
            plate.transform.localPosition = Vector3.zero;
            plate.transform.localRotation = Quaternion.identity;
            plate.transform.localScale = Vector3.one;

            var body = Ensure<Rigidbody>(plate);
            body.mass = .04f;
            body.linearDamping = 8f;
            body.angularDamping = 8f;
            body.useGravity = true;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.constraints = RigidbodyConstraints.None;

            var collider = Ensure<BoxCollider>(plate);
            collider.center = new Vector3(0f, .01f, 0f);
            collider.size = new Vector3(.13f, .02f, .09f);
            collider.isTrigger = false;
            collider.enabled = true;

            var grab = Ensure<XRGrabInteractable>(plate);
            grab.colliders.Clear();
            grab.colliders.Add(collider);
            grab.selectMode = InteractableSelectMode.Single;
            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            grab.trackPosition = true;
            grab.trackRotation = true;
            grab.trackScale = false;
            grab.throwOnDetach = false;
            grab.forceGravityOnDetach = false;
            grab.useDynamicAttach = true;
            grab.matchAttachPosition = true;
            grab.matchAttachRotation = true;
            grab.snapToColliderVolume = true;
            grab.enabled = true;

            SetEnabled<PreparedPlateInteraction>(plate, false);
            SetEnabled<LessonPlateGrabGate>(plate, false);
            SetEnabled<XRSimpleInteractable>(plate, false);

            var recovery = Ensure<PlateDropRecovery>(plate);
            recovery.Configure(homeAnchor, Require(InstrumentPath).GetComponent<InstrumentController>(), grab, -.15f);
        }

        private static PlateInspectionHotspot[] ConfigureInspectionHotspots(GameObject plate,
            PlateInspectionController inspection)
        {
            var definitions = new[]
            {
                ("Inspect_PlateId_Target", PlateInspectionKind.PlateId, new Vector3(0f,.03f,-.04f), new Vector3(.07f,.01f,.01f)),
                ("Inspect_A1_Target", PlateInspectionKind.A1Marker, new Vector3(-.06f,.03f,.04f), new Vector3(.02f,.02f,.02f)),
                ("Inspect_Seal_Target", PlateInspectionKind.OpticalSeal, new Vector3(.02f,.03f,.01f), new Vector3(.05f,.01f,.04f)),
                ("Inspect_Bubbles_Target", PlateInspectionKind.Bubbles, new Vector3(-.02f,.03f,-.01f), new Vector3(.04f,.01f,.03f))
            };

            var hotspots = new List<PlateInspectionHotspot>();
            foreach (var definition in definitions)
            {
                var targetTransform = FindDescendant(plate.transform, definition.Item1);
                if (targetTransform == null)
                    throw new InvalidOperationException($"Missing plate target: {definition.Item1}");
                targetTransform.localPosition = definition.Item3;
                targetTransform.localRotation = Quaternion.identity;
                targetTransform.localScale = Vector3.one;
                var target = targetTransform.gameObject;
                var collider = Ensure<BoxCollider>(target);
                collider.center = Vector3.zero;
                collider.size = definition.Item4;
                collider.isTrigger = false;
                var interactable = Ensure<XRSimpleInteractable>(target);
                interactable.colliders.Clear();
                interactable.colliders.Add(collider);
                SetEnabled<LabObjectDescriptor>(target, false);

                var renderers = target.GetComponentsInChildren<Renderer>(true);
                var label = FindDescendant(targetTransform, "Floating_Label")?.gameObject;
                var hotspot = Ensure<PlateInspectionHotspot>(target);
                hotspot.Configure(definition.Item2, inspection, interactable, collider, renderers, label);
                hotspots.Add(hotspot);
            }
            return hotspots.ToArray();
        }

        private static void DisableOldLessonFoundation()
        {
            foreach (var component in UnityEngine.Object.FindObjectsByType<GuidedLessonController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                component.enabled = false;
            foreach (var component in UnityEngine.Object.FindObjectsByType<WorkflowController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                component.enabled = false;
            foreach (var component in UnityEngine.Object.FindObjectsByType<OrientationAwarePlateSocket>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                component.enabled = false;
        }

        private static void DisableOldControl(GameObject control)
        {
            SetEnabled<PhysicalControlInteractable>(control, false);
            SetEnabled<LabObjectDescriptor>(control, false);
            var interactable = Ensure<XRSimpleInteractable>(control);
            interactable.enabled = true;
        }

        private static QpcrPlateSocket BuildPlateSocket(Transform drawer, QpcrLessonController lesson,
            QpcrDrawerController drawerController, GameObject plate, QpcrMachineAudio audio)
        {
            DestroyChild(drawer, "A1_PLATE_SOCKET_V2");
            var socketObject = new GameObject("A1_PLATE_SOCKET_V2");
            Undo.RegisterCreatedObjectUndo(socketObject, "Create A1 plate socket");
            socketObject.transform.SetParent(drawer, false);
            socketObject.transform.localPosition = new Vector3(0f, -.01f, .08f);
            socketObject.transform.localRotation = Quaternion.identity;

            var trigger = Undo.AddComponent<BoxCollider>(socketObject);
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, .025f, 0f);
            trigger.size = new Vector3(.18f, .08f, .14f);

            var attach = new GameObject("Plate_Attach").transform;
            Undo.RegisterCreatedObjectUndo(attach.gameObject, "Create plate attach");
            attach.SetParent(socketObject.transform, false);
            attach.localPosition = Vector3.zero;
            attach.localRotation = Quaternion.identity;

            var guide = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(guide, "Create socket guide");
            guide.name = "A1_Alignment_Guide";
            guide.transform.SetParent(socketObject.transform, false);
            guide.transform.localPosition = new Vector3(0f, .004f, 0f);
            guide.transform.localScale = new Vector3(.145f, .004f, .105f);
            UnityEngine.Object.DestroyImmediate(guide.GetComponent<Collider>());
            var guideRenderer = guide.GetComponent<Renderer>();

            var socket = Undo.AddComponent<QpcrPlateSocket>(socketObject);
            socket.Configure(lesson, drawerController, plate.GetComponent<XRGrabInteractable>(), plate.transform,
                plate.GetComponent<Rigidbody>(), attach, guideRenderer, audio);
            return socket;
        }

        private static QpcrGuidancePointer BuildGuidancePointer(Transform parent, QpcrLessonController lesson,
            PlateInspectionHotspot[] hotspots, Transform homeAnchor, Transform powerButton,
            Transform openButton, Transform socket, Transform closeButton, TouchscreenUi screen)
        {
            DestroyChild(parent, "QPCR_GUIDANCE_POINTER");

            var root = new GameObject("QPCR_GUIDANCE_POINTER");
            Undo.RegisterCreatedObjectUndo(root, "Create qPCR guidance pointer");
            root.transform.SetParent(parent, false);

            var cueMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Team5/Materials/InteractiveVR/M_LessonCue.mat");
            if (cueMaterial == null)
                cueMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/Team5/Materials/RealisticLab/M_SelectionBlue.mat");

            var beamObject = new GameObject("Pointer_Beam");
            Undo.RegisterCreatedObjectUndo(beamObject, "Create pointer beam");
            beamObject.transform.SetParent(root.transform, false);
            var beam = Undo.AddComponent<LineRenderer>(beamObject);
            beam.useWorldSpace = true;
            beam.positionCount = 2;
            beam.startWidth = .008f;
            beam.endWidth = .004f;
            beam.numCapVertices = 6;
            beam.alignment = LineAlignment.View;
            beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beam.receiveShadows = false;
            beam.startColor = new Color(.12f, .42f, .92f, 1f);
            beam.endColor = new Color(.12f, .72f, 1f, 1f);
            if (cueMaterial != null)
                beam.sharedMaterial = cueMaterial;

            var markerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(markerObject, "Create pointer target marker");
            markerObject.name = "Pointer_Target";
            markerObject.transform.SetParent(root.transform, false);
            markerObject.transform.localScale = Vector3.one * .028f;
            UnityEngine.Object.DestroyImmediate(markerObject.GetComponent<Collider>());
            if (cueMaterial != null)
                markerObject.GetComponent<Renderer>().sharedMaterial = cueMaterial;

            Transform Hotspot(PlateInspectionKind kind) =>
                hotspots.First(item => item != null && item.Kind == kind).transform;

            var pointer = Undo.AddComponent<QpcrGuidancePointer>(root);
            pointer.Configure(lesson, beam, markerObject.transform, new[]
            {
                new QpcrPointerTarget { State = QpcrLessonState.InspectPlateId, Target = Hotspot(PlateInspectionKind.PlateId) },
                new QpcrPointerTarget { State = QpcrLessonState.InspectA1Marker, Target = Hotspot(PlateInspectionKind.A1Marker) },
                new QpcrPointerTarget { State = QpcrLessonState.InspectOpticalSeal, Target = Hotspot(PlateInspectionKind.OpticalSeal) },
                new QpcrPointerTarget { State = QpcrLessonState.InspectBubbles, Target = Hotspot(PlateInspectionKind.Bubbles) },
                new QpcrPointerTarget { State = QpcrLessonState.ReturnPlate, Target = homeAnchor },
                new QpcrPointerTarget { State = QpcrLessonState.PowerOn, Target = powerButton },
                new QpcrPointerTarget { State = QpcrLessonState.OpenDrawer, Target = openButton },
                new QpcrPointerTarget { State = QpcrLessonState.LoadPlate, Target = socket },
                new QpcrPointerTarget { State = QpcrLessonState.CloseDrawer, Target = closeButton },
                new QpcrPointerTarget { State = QpcrLessonState.ConfigureProtocol, Target = screen.Canvas.transform },
                new QpcrPointerTarget { State = QpcrLessonState.ValidateProtocol, Target = screen.ValidateButton.transform },
                new QpcrPointerTarget { State = QpcrLessonState.StartRun, Target = screen.StartRunButton.transform },
                new QpcrPointerTarget { State = QpcrLessonState.ObserveAmplification, Target = screen.AmplificationGraph.transform },
                new QpcrPointerTarget { State = QpcrLessonState.InterpretResults, Target = screen.ResultsPanel.transform }
            });

            beamObject.SetActive(false);
            markerObject.SetActive(false);
            return pointer;
        }

        private static InstructionUi BuildInstructionCanvas(Transform parent)
        {
            DestroyChild(parent, "Plate_Instruction_Canvas");
            var canvasObject = new GameObject("Plate_Instruction_Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster), typeof(QpcrWorldSpacePanel));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create plate instruction canvas");
            canvasObject.transform.SetParent(parent, false);
            // Keep the card above the instrument, inside a comfortable VR reading
            // angle and far enough away that its edges do not clip the headset view.
            canvasObject.transform.position = new Vector3(2.60f, 1.60f, 3.08f);
            canvasObject.transform.rotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one * .00068f;
            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(660f, 430f);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingOrder = 30;
            canvasObject.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 12f;

            CreateImage(canvasRect, "Background", Vector2.zero, new Vector2(660f, 430f), new Color(.96f,.98f,1f,.97f));
            CreateImage(canvasRect, "Blue_Accent", new Vector2(-326f, 0f), new Vector2(8f, 430f),
                new Color(.12f,.42f,.92f,1f));
            var step = CreateText(canvasRect, "Lesson_Step", new Vector2(-170f, 190f), new Vector2(290f, 24f),
                14f, TextAlignmentOptions.Left, new Color(.28f,.34f,.42f,1f));
            var title = CreateText(canvasRect, "Lesson_Title", new Vector2(0f, 157f), new Vector2(590f, 42f),
                28f, TextAlignmentOptions.Center, new Color(.05f,.12f,.24f,1f));
            var target = CreateText(canvasRect, "Current_Target", new Vector2(0f, 118f), new Vector2(590f, 30f),
                21f, TextAlignmentOptions.Left, new Color(.12f,.42f,.92f,1f));
            var instruction = CreateText(canvasRect, "Persistent_Instruction", new Vector2(0f, 72f), new Vector2(590f, 54f),
                18f, TextAlignmentOptions.TopLeft, new Color(.10f,.15f,.22f,1f));
            var explanation = CreateText(canvasRect, "Scientific_Explanation", new Vector2(0f, 9f), new Vector2(590f, 62f),
                15f, TextAlignmentOptions.TopLeft, new Color(.28f,.34f,.42f,1f));
            CreateImage(canvasRect, "Action_Hint_Background", new Vector2(0f, -58f), new Vector2(590f, 42f),
                new Color(.86f,.93f,1f,1f));
            var actionHint = CreateText(canvasRect, "Action_Hint", new Vector2(0f, -58f), new Vector2(570f, 36f),
                15f, TextAlignmentOptions.Center, new Color(.08f,.27f,.55f,1f));
            var feedback = CreateText(canvasRect, "Temporary_Feedback", new Vector2(0f, -105f), new Vector2(590f, 38f),
                16f, TextAlignmentOptions.Center, new Color(.94f,.18f,.22f,1f));
            var progress = CreateText(canvasRect, "Progress", new Vector2(-205f, -184f), new Vector2(210f, 24f),
                14f, TextAlignmentOptions.Left, new Color(.18f,.26f,.38f,1f));
            var start = CreateButton(canvasRect, "Start_qPCR_Lesson", new Vector2(56f, -184f), new Vector2(200f, 42f),
                "Start qPCR Lesson", new Color(.12f,.42f,.92f,1f));
            var confirm = CreateButton(canvasRect, "Confirm_Visual_Check", new Vector2(56f, -184f), new Vector2(200f, 42f),
                "Confirm", new Color(.16f,.58f,.38f,1f));
            var confirmLabel = confirm.GetComponentInChildren<TMP_Text>();
            confirm.gameObject.SetActive(false);
            var replay = CreateButton(canvasRect, "Replay_Narration", new Vector2(210f, -184f), new Vector2(88f, 42f),
                "Replay", new Color(.22f,.38f,.58f,1f));
            var reset = CreateButton(canvasRect, "Reset_qPCR_Lesson", new Vector2(293f, -184f), new Vector2(66f, 42f),
                "Reset", new Color(.35f,.42f,.52f,1f));
            return new InstructionUi(canvas, step, title, target, instruction, explanation, actionHint,
                feedback, progress, start, confirm, confirmLabel, replay, reset);
        }

        private static TouchscreenUi BuildInstrumentTouchscreen(Transform instrument)
        {
            DestroyChild(instrument, "QPCR_TOUCHSCREEN_V2");
            var canvasObject = new GameObject("QPCR_TOUCHSCREEN_V2", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create qPCR touchscreen");
            canvasObject.transform.SetParent(instrument, false);
            canvasObject.transform.localPosition = new Vector3(0f, .34f, -.265f);
            canvasObject.transform.localRotation = Quaternion.Euler(24f, 0f, 0f);
            canvasObject.transform.localScale = Vector3.one * .00048f;
            var rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(900f, 650f);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingOrder = 25;
            canvasObject.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 14f;
            CreateImage(rect, "Screen_Background", Vector2.zero, new Vector2(900f,650f), new Color(.04f,.09f,.16f,1f));

            var welcome = CreatePanel(rect, "Welcome_Panel");
            CreateText(welcome, "Brand", new Vector2(0f, 95f), new Vector2(800f, 90f), 62f,
                TextAlignmentOptions.Center, new Color(.22f,.78f,1f,1f)).text = "T5 qPCR";
            CreateText(welcome, "Welcome", new Vector2(0f, -15f), new Vector2(760f, 110f), 28f,
                TextAlignmentOptions.Center, Color.white).text = "IGH Genomics Training Lab\nInstrument ready";

            var protocol = CreatePanel(rect, "Protocol_Panel");
            var header = CreateText(protocol, "Header", new Vector2(-235f, 285f), new Vector2(390f,44f), 27f,
                TextAlignmentOptions.Left, Color.white);
            header.text = "Configure qPCR Run";
            var activeInputBackground = CreateImage(protocol, "Active_Input_Background", new Vector2(276f, 278f),
                new Vector2(260f,48f), new Color(.93f,.96f,1f,1f));
            var activeInput = CreateText(activeInputBackground.rectTransform, "Active_Input", Vector2.zero,
                new Vector2(248f,44f), 24f, TextAlignmentOptions.Center, new Color(.18f,.26f,.38f,1f));

            var fields = new QpcrProtocolFieldView[12];
            for (var index = 0; index < fields.Length; index++)
            {
                var column = index / 6;
                var row = index % 6;
                var parameter = (QpcrProtocolParameter)index;
                var button = CreateButton(protocol, "Field_" + parameter,
                    new Vector2(-290f + column * 292f, 220f - row * 52f), new Vector2(278f, 44f), "", new Color(.91f,.95f,.98f,1f));
                var label = CreateText(button.GetComponent<RectTransform>(), "Label", new Vector2(-45f,0f), new Vector2(176f,38f),
                    15f, TextAlignmentOptions.Left, new Color(.08f,.13f,.21f,1f));
                label.text = QpcrProtocolValidator.LabelFor(parameter);
                var value = CreateText(button.GetComponent<RectTransform>(), "Value", new Vector2(83f,0f), new Vector2(80f,38f),
                    15f, TextAlignmentOptions.Right, new Color(.08f,.13f,.21f,1f));
                value.text = "—";
                fields[index] = new QpcrProtocolFieldView
                {
                    Parameter = parameter,
                    Label = label,
                    Value = value,
                    Background = button.GetComponent<Image>(),
                    SelectButton = button
                };
            }

            var keypad = CreatePanel(protocol, "Numeric_Keypad");
            keypad.anchorMin = keypad.anchorMax = new Vector2(.5f,.5f);
            keypad.sizeDelta = new Vector2(282f, 245f);
            keypad.anchoredPosition = new Vector2(292f, 38f);
            var keyLabels = new[] { "1","2","3","4","5","6","7","8","9",".","0","⌫" };
            var keyButtons = new Button[keyLabels.Length];
            for (var index = 0; index < keyLabels.Length; index++)
            {
                var col = index % 3;
                var row = index / 3;
                keyButtons[index] = CreateButton(keypad, "Key_" + keyLabels[index],
                    new Vector2(-86f + col * 86f, 84f - row * 54f), new Vector2(76f,46f), keyLabels[index],
                    new Color(.18f,.31f,.48f,1f));
            }
            var confirmKey = CreateButton(keypad, "Key_Confirm", new Vector2(0f,-132f), new Vector2(248f,42f),
                "Confirm value", new Color(.12f,.42f,.92f,1f));
            var validation = CreateText(protocol, "Validation", new Vector2(-120f,-272f), new Vector2(500f,52f), 16f,
                TextAlignmentOptions.Left, new Color(.55f,.86f,.68f,1f));
            validation.text = "Select a field and use the number pad.";
            var validateButton = CreateButton(protocol, "Validate_Protocol", new Vector2(268f,-276f), new Vector2(250f,48f),
                "Validate Protocol", new Color(.16f,.58f,.38f,1f));
            var startRunButton = CreateButton(protocol, "Start_Run", new Vector2(268f,-222f), new Vector2(250f,48f),
                "Start Run", new Color(.12f,.42f,.92f,1f));
            startRunButton.interactable = false;

            var run = CreatePanel(rect, "Run_Panel");
            var cycle = CreateText(run, "Cycle", new Vector2(-250f,276f), new Vector2(340f,46f), 26f, TextAlignmentOptions.Left, Color.white);
            var stage = CreateText(run, "Stage", new Vector2(30f,276f), new Vector2(350f,46f), 23f, TextAlignmentOptions.Center, new Color(.25f,.82f,1f,1f));
            var temperature = CreateText(run, "Temperature", new Vector2(315f,276f), new Vector2(180f,46f), 26f, TextAlignmentOptions.Right, Color.white);
            var graphObject = CreateRect(run, "Amplification_Graph", new Vector2(0f,35f), new Vector2(780f,390f));
            var graph = graphObject.gameObject.AddComponent<AmplificationGraphView>();
            graph.raycastTarget = false;
            CreateImage(run, "Progress_Track", new Vector2(0f,-205f), new Vector2(760f,20f), new Color(.10f,.18f,.28f,1f));
            var fill = CreateImage(run, "Progress_Fill", new Vector2(0f,-205f), new Vector2(760f,20f), new Color(.16f,.72f,.38f,1f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;
            var meltObject = CreateRect(run, "Melt_Curve", new Vector2(0f,-95f), new Vector2(360f,170f));
            var melt = meltObject.gameObject.AddComponent<MeltCurveView>();
            melt.raycastTarget = false;

            var results = CreatePanel(rect, "Results_Panel");
            CreateText(results, "Results_Title", new Vector2(0f,275f), new Vector2(800f,52f), 30f,
                TextAlignmentOptions.Center, Color.white).text = "qPCR Results · Control Check";
            var interpretation = CreateText(results, "Interpretation", new Vector2(0f,90f), new Vector2(760f,220f), 24f,
                TextAlignmentOptions.Center, Color.white);
            interpretation.text = "Positive control: amplified\nNo-template control (NTC): flat\n\nWhat do these controls indicate?";
            var valid = CreateButton(results, "Controls_Valid", new Vector2(-185f,-105f), new Vector2(330f,70f),
                "Controls passed", new Color(.16f,.58f,.38f,1f));
            var invalid = CreateButton(results, "Controls_Invalid", new Vector2(185f,-105f), new Vector2(330f,70f),
                "Controls failed", new Color(.75f,.28f,.23f,1f));
            CreateText(results, "Disclaimer", new Vector2(0f,-240f), new Vector2(760f,70f), 17f,
                TextAlignmentOptions.Center, new Color(.64f,.72f,.82f,1f)).text =
                "Teaching data only. Correct interpretation requires the positive control to amplify and the NTC to remain flat.";

            welcome.gameObject.SetActive(true);
            protocol.gameObject.SetActive(false);
            run.gameObject.SetActive(false);
            results.gameObject.SetActive(false);

            return new TouchscreenUi(canvas, welcome.gameObject, protocol.gameObject, run.gameObject, results.gameObject,
                fields, activeInput, validation, validateButton, startRunButton, keyButtons, confirmKey,
                graph, melt, cycle, stage, temperature, fill, interpretation, valid, invalid);
        }

        private static void WireProtocolButtons(TouchscreenUi ui, QpcrTouchscreenController touchscreen)
        {
            for (var index = 0; index < ui.Fields.Length; index++)
                UnityEventTools.AddIntPersistentListener(ui.Fields[index].SelectButton.onClick, touchscreen.SelectField, index);

            var keypad = Ensure<QpcrNumericKeypad>(ui.ProtocolPanel);
            keypad.Configure(touchscreen);
            var methods = new UnityEngine.Events.UnityAction[]
            {
                keypad.Press1,keypad.Press2,keypad.Press3,keypad.Press4,keypad.Press5,keypad.Press6,
                keypad.Press7,keypad.Press8,keypad.Press9,keypad.PressDecimal,keypad.Press0,keypad.PressBackspace
            };
            for (var index = 0; index < ui.KeyButtons.Length; index++)
                UnityEventTools.AddVoidPersistentListener(ui.KeyButtons[index].onClick, methods[index]);
            UnityEventTools.AddVoidPersistentListener(ui.ConfirmKey.onClick, keypad.PressConfirm);
        }

        private static NarrationCue[] BuildNarrationCues()
        {
            var mappings = new Dictionary<string,string>
            {
                ["InspectPlateId"] = "InspectPlateId.wav",
                ["InspectA1Marker"] = "InspectA1Marker.wav",
                ["InspectOpticalSeal"] = "InspectOpticalSeal.wav",
                ["InspectBubbles"] = "InspectBubbles.wav",
                ["ReturnPlate"] = "TourPreparedPlate.wav",
                ["PowerOn"] = "PowerOnInstrument.wav",
                ["OpenDrawer"] = "OpenDrawer.wav",
                ["LoadPlate"] = "SeatPlate.wav",
                ["CloseDrawer"] = "CloseDrawer.wav",
                ["ConfigureProtocol"] = "ValidateProtocol.wav",
                ["ValidateProtocol"] = "ValidateProtocol.wav",
                ["StartRun"] = "StartRun.wav",
                ["ObserveAmplification"] = "ObserveAmplification.wav",
                ["InterpretResults"] = "InterpretControlsPassed.wav",
                ["Complete"] = "Complete.wav"
            };
            return mappings.Select(pair => new NarrationCue
            {
                Key = pair.Key,
                Caption = string.Empty,
                Clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Team5/Scripts/Audio/Narration/" + pair.Value)
            }).ToArray();
        }

        private static RectTransform CreatePanel(RectTransform parent, string name)
        {
            var rect = CreateRect(parent, name, Vector2.zero, parent.rect.size);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RectTransform CreateRect(RectTransform parent, string name, Vector2 position, Vector2 size)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + name);
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f);
            rect.pivot = new Vector2(.5f,.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static Image CreateImage(RectTransform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var rect = CreateRect(parent, name, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(RectTransform parent, string name, Vector2 position, Vector2 size,
            float fontSize, TextAlignmentOptions alignment, Color color)
        {
            var rect = CreateRect(parent, name, position, size);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = name;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(RectTransform parent, string name, Vector2 position, Vector2 size,
            string label, Color color)
        {
            var rect = CreateRect(parent, name, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var text = CreateText(rect, "Label", Vector2.zero, size - new Vector2(12f,8f), 17f,
                TextAlignmentOptions.Center, Color.white);
            text.text = label;
            return button;
        }

        private static T Ensure<T>(GameObject gameObject) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(gameObject);
        }

        private static void SetEnabled<T>(GameObject gameObject, bool enabled) where T : Behaviour
        {
            var component = gameObject.GetComponent<T>();
            if (component != null)
                component.enabled = enabled;
        }

        private static GameObject Require(string path)
        {
            var gameObject = GameObject.Find(path);
            if (gameObject == null)
                throw new InvalidOperationException("Required DemoScene object was not found: " + path);
            return gameObject;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == name);
        }

        private static void DestroyChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);
        }

        private readonly struct InstructionUi
        {
            public InstructionUi(Canvas canvas, TMP_Text step, TMP_Text title, TMP_Text target, TMP_Text instruction,
                TMP_Text explanation, TMP_Text actionHint, TMP_Text feedback, TMP_Text progress, Button startButton,
                Button confirmButton, TMP_Text confirmLabel, Button replayButton, Button resetButton)
            {
                Canvas = canvas; Step = step; Title = title; Target = target; Instruction = instruction;
                Explanation = explanation; ActionHint = actionHint; Feedback = feedback; Progress = progress;
                StartButton = startButton; ConfirmButton = confirmButton; ConfirmLabel = confirmLabel;
                ReplayButton = replayButton; ResetButton = resetButton;
            }
            public Canvas Canvas { get; }
            public TMP_Text Step { get; }
            public TMP_Text Title { get; }
            public TMP_Text Target { get; }
            public TMP_Text Instruction { get; }
            public TMP_Text Explanation { get; }
            public TMP_Text ActionHint { get; }
            public TMP_Text Feedback { get; }
            public TMP_Text Progress { get; }
            public Button StartButton { get; }
            public Button ConfirmButton { get; }
            public TMP_Text ConfirmLabel { get; }
            public Button ReplayButton { get; }
            public Button ResetButton { get; }
        }

        private readonly struct TouchscreenUi
        {
            public TouchscreenUi(Canvas canvas, GameObject welcome, GameObject protocol, GameObject run, GameObject results,
                QpcrProtocolFieldView[] fields, TMP_Text activeInput, TMP_Text validation, Button validateButton,
                Button startRunButton, Button[] keyButtons, Button confirmKey, AmplificationGraphView graph,
                MeltCurveView melt, TMP_Text cycle, TMP_Text stage, TMP_Text temperature, Image progress,
                TMP_Text interpretation, Button valid, Button invalid)
            {
                Canvas = canvas; WelcomePanel = welcome; ProtocolPanel = protocol; RunPanel = run; ResultsPanel = results;
                Fields = fields; ActiveInput = activeInput; Validation = validation; ValidateButton = validateButton;
                StartRunButton = startRunButton; KeyButtons = keyButtons; ConfirmKey = confirmKey;
                AmplificationGraph = graph; MeltCurve = melt; CycleText = cycle; StageText = stage;
                TemperatureText = temperature; ProgressFill = progress; Interpretation = interpretation;
                ValidControlsButton = valid; InvalidControlsButton = invalid;
            }
            public Canvas Canvas { get; }
            public GameObject WelcomePanel { get; }
            public GameObject ProtocolPanel { get; }
            public GameObject RunPanel { get; }
            public GameObject ResultsPanel { get; }
            public QpcrProtocolFieldView[] Fields { get; }
            public TMP_Text ActiveInput { get; }
            public TMP_Text Validation { get; }
            public Button ValidateButton { get; }
            public Button StartRunButton { get; }
            public Button[] KeyButtons { get; }
            public Button ConfirmKey { get; }
            public AmplificationGraphView AmplificationGraph { get; }
            public MeltCurveView MeltCurve { get; }
            public TMP_Text CycleText { get; }
            public TMP_Text StageText { get; }
            public TMP_Text TemperatureText { get; }
            public Image ProgressFill { get; }
            public TMP_Text Interpretation { get; }
            public Button ValidControlsButton { get; }
            public Button InvalidControlsButton { get; }
        }
    }
}
#endif
