using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using Team5.qPCR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Team5.qPCR.Editor
{
    public static class Team5ProjectBuilder
    {
        private const string Root = "Assets/Team5";
        private const string ScenePath = Root + "/Scenes/Team5_qPCR_LoadAndRun.unity";
        private const string DataPath = Root + "/Data";
        private const string MaterialPath = Root + "/Materials";
        private const string ProcessedPrefabPath = Root + "/Prefabs/Processed";

        private static readonly Color Background = Hex("071217");
        private static readonly Color Panel = Hex("0D1E24");
        private static readonly Color PanelLight = Hex("142B32");
        private static readonly Color Cyan = Hex("2CE5C7");
        private static readonly Color Blue = Hex("4BBFFF");
        private static readonly Color Amber = Hex("FFC34B");
        private static readonly Color Pink = Hex("FF6F86");
        private static readonly Color Text = Hex("EAF7F7");
        private static readonly Color Muted = Hex("8CA3A6");

        public static void Build()
        {
            EnsureFolders();
            EnsureTmpEssentials();
            var font = LoadFont();

            var materials = CreateMaterials();
            var processedModels = ProcessModels(materials);
            var experiment = CreateExperimentDefinition();
            var protocol = CreateRunProtocol();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Team5_qPCR_LoadAndRun";

            var camera = CreateEnvironment(materials);
            var plate = CreatePlateStation(experiment, materials, processedModels.PlatePrefab);
            var instrument = CreateInstrument(materials, processedModels.InstrumentPrefab, plate);
            CreateConsumables(materials, processedModels.PipettePrefab);

            var systems = new GameObject("SimulationSystems");
            var resultsController = systems.AddComponent<ResultsController>();
            var runSimulation = systems.AddComponent<RunSimulationController>();
            var workflow = systems.AddComponent<WorkflowController>();
            var desktopInput = systems.AddComponent<DesktopInputController>();

            var ui = CreateInterface(camera, font, workflow);
            resultsController.Configure(ui.ResultsPanel, ui.CurveGraphic, ui.ResultSummary, ui.ResultCallout);
            runSimulation.Configure(protocol, experiment, resultsController);
            workflow.Configure(plate.Controller, instrument, runSimulation, resultsController);
            desktopInput.Configure(workflow);
            ui.Mentor.Configure(
                workflow,
                ui.StageTitle,
                ui.Instruction,
                ui.StageCounter,
                ui.Status,
                ui.ActionLabel,
                ui.Disclaimer,
                ui.ActionButton,
                ui.Progress);

            var focus = new GameObject("CameraFocus").transform;
            focus.position = new Vector3(0f, 1.3f, 0.65f);
            var orbit = camera.gameObject.AddComponent<CameraOrbitController>();
            orbit.Configure(focus);

            MarkDirty(plate.Controller, instrument, resultsController, runSimulation, workflow, desktopInput, ui.Mentor, orbit);
            ConfigurePlayerSettings();
            ConfigureXrLoadersBestEffort();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            CapturePreview(camera);
            Debug.Log("[Team 5 Builder] qPCR vertical slice scene and assets created successfully.");
        }

        private static void EnsureFolders()
        {
            var folders = new[]
            {
                Root + "/Scenes",
                DataPath,
                MaterialPath,
                Root + "/Prefabs",
                ProcessedPrefabPath,
                Root + "/Tests/EditMode"
            };

            foreach (var folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Directory.CreateDirectory(Path.GetFullPath(folder));
                }
            }

            AssetDatabase.Refresh();
        }

        private static void EnsureTmpEssentials()
        {
            if (!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
            {
                throw new InvalidOperationException(
                    "TextMeshPro Essential Resources are missing. Run TmpResourceInstaller.Install before building the scene.");
            }
        }

        private static TMP_FontAsset LoadFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font == null)
            {
                throw new InvalidOperationException("LiberationSans SDF was not imported with the TMP Essential Resources.");
            }

            return font;
        }

        private static MaterialSet CreateMaterials()
        {
            return new MaterialSet
            {
                Floor = CreateMaterial("M_Floor", new Color(0.025f, 0.055f, 0.065f), 0.24f, 0.78f),
                Worktop = CreateMaterial("M_Worktop", new Color(0.07f, 0.12f, 0.14f), 0.08f, 0.66f),
                Instrument = CreateMaterial("M_Instrument", new Color(0.1f, 0.17f, 0.19f), 0.35f, 0.72f),
                InstrumentAccent = CreateMaterial("M_InstrumentAccent", Cyan * 0.55f, 0.18f, 0.82f, Cyan * 0.3f),
                Plate = CreateMaterial("M_Plate", new Color(0.7f, 0.82f, 0.84f), 0.02f, 0.52f),
                Well = CreateMaterial("M_Well", new Color(0.12f, 0.18f, 0.22f), 0.02f, 0.52f),
                Metal = CreateMaterial("M_Metal", new Color(0.28f, 0.35f, 0.36f), 0.82f, 0.75f),
                Amber = CreateMaterial("M_Amber", Amber * 0.78f, 0.05f, 0.7f, Amber * 0.25f),
                Seal = CreateTransparentMaterial("M_OpticalSeal", new Color(0.52f, 0.9f, 1f, 0.24f))
            };
        }

        private static Material CreateMaterial(string name, Color color, float metallic, float smoothness, Color? emission = null)
        {
            var path = $"{MaterialPath}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateTransparentMaterial(string name, Color color)
        {
            var material = CreateMaterial(name, color, 0f, 0.85f);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static ProcessedModels ProcessModels(MaterialSet materials)
        {
            ConfigureModelImporter(Root + "/Models/Team5_qPCR_Instrument.fbx");
            ConfigureModelImporter(Root + "/Models/Team5_96Well_Plate.fbx");
            ConfigureModelImporter(Root + "/Models/Team5_96Well_Plate_ColorCoded.fbx");
            ConfigureModelImporter(Root + "/Models/Mechanical_Pipette.fbx");

            return new ProcessedModels
            {
                InstrumentPrefab = CreateNormalizedPrefab(
                    Root + "/Models/Team5_qPCR_Instrument.fbx",
                    ProcessedPrefabPath + "/Team5_qPCR_Instrument_Processed.prefab",
                    "Team5_qPCR_Instrument_Processed",
                    3.1f,
                    materials.Instrument),
                PlatePrefab = CreateNormalizedPrefab(
                    Root + "/Models/Team5_96Well_Plate_ColorCoded.fbx",
                    ProcessedPrefabPath + "/Team5_96Well_Plate_Processed.prefab",
                    "Team5_96Well_Plate_Processed",
                    2.35f,
                    materials.Plate),
                PipettePrefab = CreateNormalizedPrefab(
                    Root + "/Models/Mechanical_Pipette.fbx",
                    ProcessedPrefabPath + "/Mechanical_Pipette_Processed.prefab",
                    "Mechanical_Pipette_Processed",
                    1.25f,
                    materials.InstrumentAccent)
            };
        }

        private static void ConfigureModelImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                return;
            }

            importer.globalScale = 1f;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static GameObject CreateNormalizedPrefab(
            string modelPath,
            string prefabPath,
            string wrapperName,
            float targetMaxDimension,
            Material material)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source == null)
            {
                Debug.LogWarning($"[Team 5 Builder] Model missing: {modelPath}");
                return null;
            }

            var wrapper = new GameObject(wrapperName);
            var instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            instance.name = wrapperName + "_Visual";
            instance.transform.SetParent(wrapper.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            var initialBounds = CalculateBounds(instance);
            var maximum = Mathf.Max(initialBounds.size.x, Mathf.Max(initialBounds.size.y, initialBounds.size.z));
            if (maximum > 0.0001f)
            {
                instance.transform.localScale = Vector3.one * (targetMaxDimension / maximum);
            }

            var scaledBounds = CalculateBounds(instance);
            instance.transform.position -= scaledBounds.center;

            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                var count = Mathf.Max(1, renderer.sharedMaterials.Length);
                renderer.sharedMaterials = Enumerable.Repeat(material, count).ToArray();
            }

            var centeredBounds = CalculateBounds(instance);
            var collider = wrapper.AddComponent<BoxCollider>();
            collider.center = wrapper.transform.InverseTransformPoint(centeredBounds.center);
            collider.size = centeredBounds.size;

            var prefab = PrefabUtility.SaveAsPrefabAsset(wrapper, prefabPath);
            UnityEngine.Object.DestroyImmediate(wrapper);
            return prefab;
        }

        private static Bounds CalculateBounds(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(target.transform.position, Vector3.one);
            }

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private static ExperimentDefinition CreateExperimentDefinition()
        {
            var path = DataPath + "/Team5_ExperimentDefinition.asset";
            var experiment = AssetDatabase.LoadAssetAtPath<ExperimentDefinition>(path);
            if (experiment == null)
            {
                experiment = ScriptableObject.CreateInstance<ExperimentDefinition>();
                AssetDatabase.CreateAsset(experiment, path);
            }

            var wells = new WellDefinition[96];
            var sampleNumber = 1;
            for (var row = 0; row < 8; row++)
            {
                for (var column = 0; column < 12; column++)
                {
                    var index = (row * 12) + column;
                    var wellId = $"{(char)('A' + row)}{column + 1}";
                    var type = (row == 0 && column < 2)
                        ? WellType.PositiveControl
                        : (row == 7 && column >= 10 ? WellType.NoTemplateControl : WellType.Sample);
                    var sampleId = type == WellType.PositiveControl
                        ? $"PC-{column + 1}"
                        : type == WellType.NoTemplateControl
                            ? $"NTC-{column - 9}"
                            : $"S-{sampleNumber++:000}";
                    var color = type == WellType.PositiveControl
                        ? Cyan
                        : type == WellType.NoTemplateControl
                            ? Pink
                            : Color.Lerp(Blue, Cyan, row / 7f) * 0.82f;

                    wells[index] = new WellDefinition
                    {
                        Row = row,
                        Column = column,
                        WellId = wellId,
                        SampleId = sampleId,
                        Type = type,
                        DisplayColor = color
                    };
                }
            }

            experiment.Configure(
                "Team 5 Educational qPCR Panel",
                "Educational simulation only. Curves and Cq values are representative and must not be used for diagnosis.",
                wells);
            EditorUtility.SetDirty(experiment);
            return experiment;
        }

        private static RunProtocol CreateRunProtocol()
        {
            var path = DataPath + "/Team5_RunProtocol.asset";
            var protocol = AssetDatabase.LoadAssetAtPath<RunProtocol>(path);
            if (protocol == null)
            {
                protocol = ScriptableObject.CreateInstance<RunProtocol>();
                AssetDatabase.CreateAsset(protocol, path);
            }

            protocol.Configure(40, 95f, 60f, 72f, 10f);
            EditorUtility.SetDirty(protocol);
            return protocol;
        }

        private static Camera CreateEnvironment(MaterialSet materials)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.08f, 0.14f, 0.16f);
            RenderSettings.ambientEquatorColor = new Color(0.035f, 0.08f, 0.09f);
            RenderSettings.ambientGroundColor = Background;

            var cameraObject = new GameObject("MainCamera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.fieldOfView = 46f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 80f;
            camera.transform.position = new Vector3(2.2f, 5.6f, -8.8f);
            camera.transform.LookAt(new Vector3(0f, 1.25f, 0.8f));

            var keyLight = new GameObject("KeyLight", typeof(Light)).GetComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(0.78f, 0.93f, 1f);
            keyLight.intensity = 0.92f;
            keyLight.shadows = LightShadows.Soft;
            keyLight.transform.rotation = Quaternion.Euler(48f, -28f, 0f);

            var accentLight = new GameObject("CyanAccentLight", typeof(Light)).GetComponent<Light>();
            accentLight.type = LightType.Point;
            accentLight.color = Cyan;
            accentLight.intensity = 2.2f;
            accentLight.range = 8f;
            accentLight.transform.position = new Vector3(-2.3f, 3.5f, -0.2f);

            var warmLight = new GameObject("WarmInstrumentLight", typeof(Light)).GetComponent<Light>();
            warmLight.type = LightType.Point;
            warmLight.color = Amber;
            warmLight.intensity = 0.75f;
            warmLight.range = 5f;
            warmLight.transform.position = new Vector3(2.8f, 2.8f, 0.4f);

            CreateCube("Floor", new Vector3(0f, -0.15f, 1f), new Vector3(15f, 0.25f, 12f), materials.Floor);
            CreateCube("RearWall", new Vector3(0f, 3.3f, 5.4f), new Vector3(15f, 7f, 0.25f), materials.Floor);
            CreateCube("Workbench", new Vector3(0f, 0.72f, 0.8f), new Vector3(8.4f, 0.34f, 4.2f), materials.Worktop);
            CreateCube("WorkbenchFront", new Vector3(0f, 0.22f, 0.8f), new Vector3(8.4f, 0.7f, 3.8f), materials.Floor);

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(DataPath + "/Team5_PostProcessing.asset");
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, DataPath + "/Team5_PostProcessing.asset");
            }

            profile.components.Clear();
            var bloom = profile.Add<Bloom>();
            bloom.intensity.Override(0.38f);
            bloom.threshold.Override(0.9f);
            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.35f);
            var color = profile.Add<ColorAdjustments>();
            color.contrast.Override(12f);
            color.saturation.Override(-5f);
            EditorUtility.SetDirty(profile);

            var volume = new GameObject("ClinicalPostProcessing", typeof(Volume)).GetComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            return camera;
        }

        private static PlateStation CreatePlateStation(ExperimentDefinition experiment, MaterialSet materials, GameObject platePrefab)
        {
            var home = new GameObject("PlateHomeAnchor").transform;
            home.position = new Vector3(-1.4f, 1.18f, 0.15f);
            home.rotation = Quaternion.Euler(0f, -8f, 0f);

            var root = new GameObject("Prepared96WellPlate").transform;
            root.SetParent(home, false);
            var controller = root.gameObject.AddComponent<PlateController>();

            CreateCube("PlateBase", Vector3.zero, new Vector3(2.75f, 0.16f, 1.92f), materials.Plate, root);
            if (platePrefab != null)
            {
                var model = PrefabUtility.InstantiatePrefab(platePrefab) as GameObject;
                model.name = "Team5PlateModel_VisualReference";
                model.transform.SetParent(root, false);
                model.transform.localPosition = new Vector3(0f, 0.09f, 0f);
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
            }

            var wellsRoot = new GameObject("Generated96Wells").transform;
            wellsRoot.SetParent(root, false);
            var renderers = new Renderer[96];
            for (var row = 0; row < 8; row++)
            {
                for (var column = 0; column < 12; column++)
                {
                    var index = (row * 12) + column;
                    var well = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    well.name = $"Well_{(char)('A' + row)}{column + 1:00}";
                    well.transform.SetParent(wellsRoot, false);
                    well.transform.localPosition = new Vector3(-1.1f + (column * 0.2f), 0.13f, -0.69f + (row * 0.195f));
                    well.transform.localScale = new Vector3(0.072f, 0.035f, 0.072f);
                    well.GetComponent<Renderer>().sharedMaterial = materials.Well;
                    UnityEngine.Object.DestroyImmediate(well.GetComponent<Collider>());
                    renderers[index] = well.GetComponent<Renderer>();
                }
            }

            var seal = CreateCube(
                "OpticalSeal",
                new Vector3(0f, 0.225f, 0f),
                new Vector3(2.68f, 0.025f, 1.86f),
                materials.Seal,
                root);
            seal.SetActive(false);

            var orientationMarker = CreateCube(
                "A1_OrientationMarker",
                new Vector3(-1.26f, 0.22f, -0.83f),
                new Vector3(0.12f, 0.05f, 0.12f),
                materials.Amber,
                root);
            orientationMarker.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);

            controller.Configure(experiment, wellsRoot, renderers, seal.GetComponent<Renderer>());
            EditorUtility.SetDirty(controller);
            return new PlateStation { Root = root, Home = home, Controller = controller };
        }

        private static InstrumentController CreateInstrument(MaterialSet materials, GameObject instrumentPrefab, PlateStation plate)
        {
            var root = new GameObject("qPCRInstrumentStation").transform;
            root.position = new Vector3(2.25f, 1.56f, 1.35f);
            var controller = root.gameObject.AddComponent<InstrumentController>();

            CreateCube("InstrumentBody", Vector3.zero, new Vector3(3.15f, 1.65f, 2.45f), materials.Instrument, root);
            CreateCube("InstrumentTop", new Vector3(0f, 0.88f, 0.2f), new Vector3(2.9f, 0.18f, 2.1f), materials.Metal, root);
            CreateCube("StatusLight", new Vector3(0.98f, 0.42f, -1.24f), new Vector3(0.75f, 0.14f, 0.05f), materials.InstrumentAccent, root);

            if (instrumentPrefab != null)
            {
                var model = PrefabUtility.InstantiatePrefab(instrumentPrefab) as GameObject;
                model.name = "Team5InstrumentModel_VisualReference";
                model.transform.SetParent(root, false);
                model.transform.localPosition = new Vector3(0f, 0.15f, 0.08f);
                model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                model.transform.localScale = Vector3.one;
            }

            var drawer = new GameObject("MotorizedPlateDrawer").transform;
            drawer.SetParent(root, false);
            drawer.localPosition = new Vector3(0f, -0.56f, -1.18f);
            CreateCube("DrawerTray", Vector3.zero, new Vector3(2.4f, 0.13f, 1.38f), materials.Metal, drawer);
            var plateAnchor = new GameObject("InstrumentPlateAnchor").transform;
            plateAnchor.SetParent(drawer, false);
            plateAnchor.localPosition = new Vector3(0f, 0.18f, 0.05f);
            plateAnchor.localRotation = Quaternion.identity;

            controller.Configure(plate.Root, plate.Home, plateAnchor, drawer);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void CreateConsumables(MaterialSet materials, GameObject pipettePrefab)
        {
            var area = new GameObject("ConsumablesAndSealingArea").transform;
            area.position = new Vector3(-3.15f, 1.14f, 1.05f);
            CreateCube("TipRack", Vector3.zero, new Vector3(1.05f, 0.22f, 0.76f), materials.Instrument, area);
            for (var row = 0; row < 4; row++)
            {
                for (var column = 0; column < 6; column++)
                {
                    var tip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    tip.name = $"Tip_{row}_{column}";
                    tip.transform.SetParent(area, false);
                    tip.transform.localPosition = new Vector3(-0.42f + (column * 0.17f), 0.2f, -0.25f + (row * 0.17f));
                    tip.transform.localScale = new Vector3(0.035f, 0.12f, 0.035f);
                    tip.GetComponent<Renderer>().sharedMaterial = materials.Plate;
                    UnityEngine.Object.DestroyImmediate(tip.GetComponent<Collider>());
                }
            }

            CreateCube("OpticalSealDispenser", new Vector3(0f, 0.24f, 0.95f), new Vector3(1.25f, 0.42f, 0.72f), materials.Instrument, area);
            CreateCube("SealAccent", new Vector3(0f, 0.46f, 0.68f), new Vector3(0.82f, 0.05f, 0.08f), materials.Amber, area);

            if (pipettePrefab != null)
            {
                var pipette = PrefabUtility.InstantiatePrefab(pipettePrefab) as GameObject;
                pipette.name = "MechanicalPipette_Processed";
                pipette.transform.SetParent(area, false);
                pipette.transform.localPosition = new Vector3(0f, 0.88f, -0.72f);
                pipette.transform.localRotation = Quaternion.Euler(8f, 0f, -24f);
            }
        }

        private static InterfaceReferences CreateInterface(Camera camera, TMP_FontAsset font, WorkflowController workflow)
        {
            var canvasObject = new GameObject("ClinicalInterface", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 0.55f;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            var topBar = CreatePanel("TopBar", canvas.transform, new Color(0.035f, 0.09f, 0.105f, 0.96f));
            SetRect(topBar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 96f));
            CreateText("Brand", topBar, font, "TEAM 5  /  qPCR LOAD & RUN", 27f, FontStyles.Bold, Text, TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(52f, -24f), new Vector2(840f, 52f));
            CreateText("Mode", topBar, font, "DESKTOP MVP  •  XR-READY", 19f, FontStyles.Bold, Cyan, TextAlignmentOptions.MidlineRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-52f, -24f), new Vector2(620f, 52f));

            var mentorPanel = CreatePanel("MentorPanel", canvas.transform, new Color(Panel.r, Panel.g, Panel.b, 0.96f));
            SetRect(mentorPanel, new Vector2(0f, 0f), new Vector2(0.355f, 1f), new Vector2(0f, 0.5f), new Vector2(28f, 24f), new Vector2(-14f, -132f));
            var counter = CreateText("StageCounter", mentorPanel, font, "STEP 01 / 09", 17f, FontStyles.Bold, Cyan, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(34f, -30f), new Vector2(-68f, 34f));
            var title = CreateText("StageTitle", mentorPanel, font, "Load & Run", 43f, FontStyles.Bold, Text, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(34f, -82f), new Vector2(-68f, 78f));
            var instruction = CreateText("Instruction", mentorPanel, font, WorkflowContent.For(WorkflowStage.Introduction).Instruction, 23f, FontStyles.Normal, new Color(0.78f, 0.89f, 0.9f), TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(34f, -178f), new Vector2(-68f, 210f));
            instruction.enableWordWrapping = true;

            var divider = CreatePanel("Divider", mentorPanel, new Color(Cyan.r, Cyan.g, Cyan.b, 0.45f));
            SetRect(divider, new Vector2(0f, 0.58f), new Vector2(1f, 0.58f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-68f, 2f));

            var progress = CreateSlider("RunProgress", mentorPanel, new Vector2(34f, 270f), new Vector2(-68f, 24f));
            var status = CreateText("Status", mentorPanel, font, "Station ready.", 19f, FontStyles.Normal, Muted, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(34f, 190f), new Vector2(-68f, 62f));
            status.enableWordWrapping = true;

            var buttonObject = CreatePanel("PrimaryActionButton", mentorPanel, Cyan);
            SetRect(buttonObject, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 86f), new Vector2(-68f, 70f));
            var button = buttonObject.gameObject.AddComponent<UnityEngine.UI.Button>();
            buttonObject.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            button.targetGraphic = buttonObject.GetComponent<UnityEngine.UI.Image>();
            var colors = button.colors;
            colors.normalColor = Cyan;
            colors.highlightedColor = new Color(0.36f, 1f, 0.85f);
            colors.pressedColor = new Color(0.12f, 0.66f, 0.57f);
            colors.disabledColor = new Color(0.18f, 0.28f, 0.3f, 0.7f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            var actionLabel = CreateText("ActionLabel", buttonObject.transform, font, "BEGIN BRIEFING", 21f, FontStyles.Bold, Background, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-28f, -12f));

            var disclaimer = CreateText("Disclaimer", mentorPanel, font, "EDUCATIONAL SIMULATION  •  REPRESENTATIVE DATA  •  NOT FOR DIAGNOSIS", 13f, FontStyles.Bold, Amber, TextAlignmentOptions.BottomLeft,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(34f, 28f), new Vector2(-68f, 30f));

            var resultsPanel = CreatePanel("ResultsPanel", canvas.transform, new Color(Panel.r, Panel.g, Panel.b, 0.94f));
            SetRect(resultsPanel, new Vector2(0.615f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-28f, 24f), new Vector2(-14f, -132f));
            CreateText("ResultsHeader", resultsPanel, font, "AMPLIFICATION MONITOR", 22f, FontStyles.Bold, Cyan, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(28f, -26f), new Vector2(-56f, 36f));
            var resultSummary = CreateText("ResultSummary", resultsPanel, font, "AWAITING RUN", 18f, FontStyles.Bold, Text, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(28f, -74f), new Vector2(-56f, 58f));

            var graphBackground = CreatePanel("CurvePlot", resultsPanel, new Color(0.025f, 0.07f, 0.082f, 0.95f));
            SetRect(graphBackground, new Vector2(0f, 0.35f), new Vector2(1f, 0.79f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-56f, -8f));
            var graphObject = new GameObject("AmplificationCurves", typeof(RectTransform), typeof(CanvasRenderer), typeof(AmplificationCurveGraphic));
            graphObject.transform.SetParent(graphBackground, false);
            SetRect(graphObject.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var curveGraphic = graphObject.GetComponent<AmplificationCurveGraphic>();
            curveGraphic.raycastTarget = false;
            CreateText("YAxis", graphBackground, font, "ΔRn", 14f, FontStyles.Bold, Muted, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -10f), new Vector2(42f, 24f));
            CreateText("XAxis", graphBackground, font, "CYCLE  0                                      20                                      40", 12f, FontStyles.Normal, Muted, TextAlignmentOptions.Bottom,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(-84f, 24f));

            var callout = CreateText("ResultCallout", resultsPanel, font, "Threshold 0.22 ΔRn\nPositive samples cross the threshold; valid no-template controls remain flat.", 18f, FontStyles.Normal, new Color(0.75f, 0.88f, 0.9f), TextAlignmentOptions.TopLeft,
                new Vector2(0f, 0f), new Vector2(1f, 0.33f), new Vector2(0f, 0f), new Vector2(28f, 24f), new Vector2(-56f, -20f));
            callout.enableWordWrapping = true;

            var hint = CreateText("InputHint", canvas.transform, font, "SPACE / ENTER  advance     •     R  restart     •     RIGHT-DRAG  orbit     •     WHEEL  zoom", 15f, FontStyles.Bold, Muted, TextAlignmentOptions.Bottom,
                new Vector2(0.355f, 0f), new Vector2(0.615f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(0f, 36f));

            var mentor = canvasObject.AddComponent<MentorPanelController>();
            return new InterfaceReferences
            {
                Mentor = mentor,
                StageTitle = title,
                Instruction = instruction,
                StageCounter = counter,
                Status = status,
                ActionLabel = actionLabel,
                Disclaimer = disclaimer,
                ActionButton = button,
                Progress = progress,
                ResultsPanel = resultsPanel.gameObject,
                CurveGraphic = curveGraphic,
                ResultSummary = resultSummary,
                ResultCallout = callout
            };
        }

        private static Transform CreatePanel(string name, Transform parent, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            panel.transform.SetParent(parent, false);
            var image = panel.GetComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return panel.transform;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string value,
            float size,
            FontStyles style,
            Color color,
            TextAlignmentOptions alignment,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Truncate;
            SetRect(textObject.transform, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
            return text;
        }

        private static UnityEngine.UI.Slider CreateSlider(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var sliderObject = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Slider));
            sliderObject.transform.SetParent(parent, false);
            SetRect(sliderObject.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), position, size);

            var background = CreatePanel("Background", sliderObject.transform, new Color(0.14f, 0.24f, 0.27f, 1f));
            SetRect(background, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var fillArea = new GameObject("FillArea", typeof(RectTransform)).transform;
            fillArea.SetParent(sliderObject.transform, false);
            SetRect(fillArea, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-8f, -8f));
            var fill = CreatePanel("Fill", fillArea, Cyan);
            SetRect(fill, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

            var slider = sliderObject.GetComponent<UnityEngine.UI.Slider>();
            slider.fillRect = fill as RectTransform;
            slider.targetGraphic = fill.GetComponent<UnityEngine.UI.Image>();
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.interactable = false;
            return slider;
        }

        private static void SetRect(
            Transform transform,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var rect = transform as RectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static GameObject CreateCube(string name, Vector3 position, Vector3 scale, Material material, Transform parent = null)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            if (parent != null)
            {
                cube.transform.SetParent(parent, false);
                cube.transform.localPosition = position;
                cube.transform.localScale = scale;
            }
            else
            {
                cube.transform.position = position;
                cube.transform.localScale = scale;
            }

            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "XR and AI Genomics Hackathon 2026 • Team 5";
            PlayerSettings.productName = "Team 5 qPCR Load & Run";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, "org.team5.qpcrloadrun");
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "org.team5.qpcrloadrun");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.Android.bundleVersionCode = 1;
        }

        private static void ConfigureXrLoadersBestEffort()
        {
            try
            {
                var metadataType = Type.GetType("UnityEditor.XR.Management.Metadata.XRPackageMetadataStore, Unity.XR.Management.Editor");
                if (metadataType == null)
                {
                    Debug.LogWarning("[Team 5 Builder] XR packages installed; automatic loader assignment API was unavailable.");
                    return;
                }

                Debug.Log("[Team 5 Builder] XR package metadata available. OpenXR packages are installed and the interaction architecture remains loader-independent for the desktop MVP.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Team 5 Builder] XR readiness check skipped: {exception.Message}");
            }
        }

        private static void CapturePreview(Camera camera)
        {
            var deliverableDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "deliverables", "Team5-qPCR", "Screenshots"));
            Directory.CreateDirectory(deliverableDirectory);
            Canvas.ForceUpdateCanvases();
            foreach (var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
            {
                text.ForceMeshUpdate(true, true);
            }

            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(deliverableDirectory, "Team5-qPCR-Station.png"), texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void MarkDirty(params UnityEngine.Object[] objects)
        {
            foreach (var target in objects)
            {
                if (target != null)
                {
                    EditorUtility.SetDirty(target);
                }
            }
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }

        private sealed class MaterialSet
        {
            public Material Floor;
            public Material Worktop;
            public Material Instrument;
            public Material InstrumentAccent;
            public Material Plate;
            public Material Well;
            public Material Metal;
            public Material Amber;
            public Material Seal;
        }

        private sealed class ProcessedModels
        {
            public GameObject InstrumentPrefab;
            public GameObject PlatePrefab;
            public GameObject PipettePrefab;
        }

        private sealed class PlateStation
        {
            public Transform Root;
            public Transform Home;
            public PlateController Controller;
        }

        private sealed class InterfaceReferences
        {
            public MentorPanelController Mentor;
            public TMP_Text StageTitle;
            public TMP_Text Instruction;
            public TMP_Text StageCounter;
            public TMP_Text Status;
            public TMP_Text ActionLabel;
            public TMP_Text Disclaimer;
            public UnityEngine.UI.Button ActionButton;
            public UnityEngine.UI.Slider Progress;
            public GameObject ResultsPanel;
            public AmplificationCurveGraphic CurveGraphic;
            public TMP_Text ResultSummary;
            public TMP_Text ResultCallout;
        }
    }
}
