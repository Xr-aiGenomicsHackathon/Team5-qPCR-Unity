using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using Team5.qPCR;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Team5.qPCR.Editor
{
    public static class Team5RealisticLabBuilder
    {
        private const string Root = "Assets/Team5";
        private const string ScenePath = Root + "/Scenes/Team5_qPCR_RealisticLab.unity";
        private const string DataPath = Root + "/Data";
        private const string MaterialPath = Root + "/Materials/RealisticLab";
        private const string DeliverablePath = "C:/TEAM-5/deliverables/Team5-qPCR";

        private static readonly Color Ink = Hex("EAF7F7");
        private static readonly Color Muted = Hex("9EB5B8");
        private static readonly Color Teal = Hex("26D7C6");
        private static readonly Color Blue = Hex("4DA8FF");
        private static readonly Color Amber = Hex("FFC857");
        private static readonly Color Red = Hex("FF6376");
        private static readonly Color Panel = new Color(0.027f, 0.07f, 0.082f, 0.96f);
        private static TMP_FontAsset font;
        private static MaterialSet materials;

        public static void Build()
        {
            if (File.Exists(ScenePath))
            {
                Team5VisualUpgrade.Apply();
                Team5InteractiveVrUpgrade.Apply();
                return;
            }
            EnsureFoldersAndSamples();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font == null)
            {
                throw new InvalidOperationException("TextMeshPro Essential Resources are required before building the laboratory.");
            }

            materials = CreateMaterials();
            var experiment = CreateExperimentDefinition();
            var protocol = CreateRunProtocol();
            var handoff = CreatePlateHandoff();
            var dialogue = CreateDialogueSequence();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Team5_qPCR_RealisticLab";

            var shell = BuildLaboratoryShell();
            BuildLaboratoryFurniture();
            BuildBackgroundEquipment();
            var camera = BuildLightingAndCamera();
            var plate = BuildPreparedPlate(experiment, handoff);
            var instrument = BuildInstrument(plate);
            var dnaVisualizer = BuildDnaVisualizer();

            var systems = new GameObject("Team5_qPCR_Systems");
            var results = systems.AddComponent<ResultsController>();
            var simulation = systems.AddComponent<RunSimulationController>();
            var protocolSetup = systems.AddComponent<ProtocolSetupController>();
            var workflow = systems.AddComponent<WorkflowController>();
            var desktopInput = systems.AddComponent<DesktopInputController>();

            var ui = BuildInterface(camera, workflow, protocol, protocolSetup);
            results.Configure(ui.ResultsPanel, ui.CurveGraphic, ui.ResultSummary, ui.ResultCallout);
            simulation.Configure(protocol, experiment, results, dnaVisualizer);
            workflow.Configure(plate.Controller, instrument, simulation, results, protocolSetup);
            desktopInput.Configure(workflow);
            ui.Mentor.Configure(
                workflow, dialogue, ui.StageTitle, ui.Instruction, ui.StageCounter, ui.Status,
                ui.ActionLabel, ui.SecondaryActionLabel, ui.Disclaimer, ui.ActionButton,
                ui.SecondaryActionButton, ui.WhyButton, ui.Progress, ui.WhyPanel, ui.WhyText,
                ui.ProtocolPanel, ui.ResultsPanel);

            var xrUi = BuildXrInterface(workflow, dialogue, protocolSetup);
            results.ConfigureSecondaryView(xrUi.CurveGraphic, xrUi.ResultSummary, xrUi.ResultCallout);

            var xr = BuildXrRig(plate.Root);
            var shellController = systems.AddComponent<LabShellController>();
            shellController.Configure(shell.Ceiling, shell.UpperFrontWall, camera.gameObject, xr.Origin, xr.Simulator,
                ui.Root, xrUi.Root);
            shellController.SetMode(InteractionMode.Desktop);

            ConfigureCameraOrbit(camera);
            ConfigurePlayerSettings();
            MarkDirty(results, simulation, protocolSetup, workflow, desktopInput, ui.Mentor, shellController,
                plate.Controller, instrument, dnaVisualizer);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Team5VisualUpgrade.Apply();
            Team5InteractiveVrUpgrade.Apply();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            CapturePreview(camera);
            Debug.Log("[Team 5 Realistic Lab] Scene, data, desktop controls, and XR rig created successfully.");
        }

        private static void EnsureFoldersAndSamples()
        {
            var folders = new[]
            {
                Root + "/Scenes", DataPath, MaterialPath, Root + "/Tests/EditMode", Root + "/Tests/PlayMode"
            };
            foreach (var folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Directory.CreateDirectory(Path.GetFullPath(folder));
                }
            }

            AssetDatabase.Refresh();
            ImportSample("Starter Assets");
            ImportSample("XR Interaction Simulator");
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        private static void ImportSample(string displayName)
        {
            var samples = Sample.FindByPackage("com.unity.xr.interaction.toolkit", "3.3.2");
            foreach (var sample in samples)
            {
                if (string.Equals(sample.displayName, displayName, StringComparison.OrdinalIgnoreCase))
                {
                    sample.Import(Sample.ImportOptions.OverridePreviousImports);
                    Debug.Log($"[Team 5 Realistic Lab] Imported XRI sample: {displayName}");
                    return;
                }
            }

            Debug.LogWarning($"[Team 5 Realistic Lab] XRI sample not found: {displayName}");
        }

        private static MaterialSet CreateMaterials()
        {
            var set = new MaterialSet
            {
                Epoxy = CreateMaterial("M_EpoxyFloor", Hex("53666D"), 0.05f, 0.72f),
                Wall = CreateMaterial("M_PaintedWall", Hex("D8E1DE"), 0f, 0.32f),
                Worktop = CreateMaterial("M_LabWorktop", Hex("20383D"), 0.08f, 0.65f),
                Cabinet = CreateMaterial("M_LabCabinet", Hex("C1D0CD"), 0.15f, 0.52f),
                Steel = CreateMaterial("M_StainlessSteel", Hex("AAB8B9"), 0.86f, 0.78f),
                Instrument = CreateMaterial("M_InstrumentPlastic", Hex("24343A"), 0.12f, 0.7f),
                Rubber = CreateMaterial("M_Rubber", Hex("151A1B"), 0f, 0.16f),
                Glass = CreateTransparentMaterial("M_LabGlass", new Color(0.62f, 0.86f, 0.9f, 0.18f)),
                Seal = CreateTransparentMaterial("M_OpticalSeal", new Color(0.55f, 0.9f, 1f, 0.3f)),
                Plate = CreateMaterial("M_PlatePolypropylene", Hex("CFDADD"), 0f, 0.48f),
                UnusedWell = CreateMaterial("M_UnusedWell", Hex("26383E"), 0f, 0.5f),
                Teal = CreateMaterial("M_TealEmission", Teal * 0.62f, 0.1f, 0.76f, Teal * 0.55f),
                Red = CreateMaterial("M_RedEmission", Red * 0.64f, 0.05f, 0.68f, Red * 0.45f),
                Blue = CreateMaterial("M_BlueEmission", Blue * 0.6f, 0.05f, 0.68f, Blue * 0.4f),
                Amber = CreateMaterial("M_AmberEmission", Amber * 0.62f, 0.05f, 0.68f, Amber * 0.4f),
                Biohazard = CreateMaterial("M_BiohazardBin", Hex("E6C23B"), 0.02f, 0.42f),
                Waste = CreateMaterial("M_OrdinaryWaste", Hex("3E5660"), 0.02f, 0.38f),
                LiquidSample = CreateMaterial("M_SampleLiquid", Hex("38BEE8"), 0f, 0.84f, Blue * 0.18f),
                LiquidControl = CreateMaterial("M_ControlLiquid", Hex("46E49F"), 0f, 0.84f, Teal * 0.18f)
            };
            set.Pipette = CreatePipetteMaterial();
            return set;
        }

        private static Material CreatePipetteMaterial()
        {
            const string basePath = Root + "/Models/PipetteTextures/Pipette_Base_color.png";
            const string sourceNormalPath = Root + "/Models/PipetteTextures/Pipette_Normal_DirectX.png";
            var convertedNormalPath = MaterialPath + "/Pipette_Normal_OpenGL.png";
            var sourceImporter = AssetImporter.GetAtPath(sourceNormalPath) as TextureImporter;
            if (sourceImporter != null)
            {
                var oldReadable = sourceImporter.isReadable;
                var oldType = sourceImporter.textureType;
                var oldSrgb = sourceImporter.sRGBTexture;
                sourceImporter.isReadable = true;
                sourceImporter.textureType = TextureImporterType.Default;
                sourceImporter.sRGBTexture = false;
                sourceImporter.SaveAndReimport();
                var source = AssetDatabase.LoadAssetAtPath<Texture2D>(sourceNormalPath);
                if (source != null)
                {
                    var pixels = source.GetPixels32();
                    for (var index = 0; index < pixels.Length; index++)
                    {
                        pixels[index].g = (byte)(255 - pixels[index].g);
                    }

                    var converted = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
                    converted.SetPixels32(pixels);
                    converted.Apply();
                    File.WriteAllBytes(Path.GetFullPath(convertedNormalPath), converted.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(converted);
                    AssetDatabase.ImportAsset(convertedNormalPath, ImportAssetOptions.ForceUpdate);
                    var convertedImporter = AssetImporter.GetAtPath(convertedNormalPath) as TextureImporter;
                    if (convertedImporter != null)
                    {
                        convertedImporter.textureType = TextureImporterType.NormalMap;
                        convertedImporter.sRGBTexture = false;
                        convertedImporter.SaveAndReimport();
                    }
                }

                sourceImporter.isReadable = oldReadable;
                sourceImporter.textureType = oldType;
                sourceImporter.sRGBTexture = oldSrgb;
                sourceImporter.SaveAndReimport();
            }

            var result = CreateMaterial("M_Pipette_Textured", Color.white, 0.05f, 0.64f);
            var baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
            var normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(convertedNormalPath);
            if (baseMap != null) result.SetTexture("_BaseMap", baseMap);
            if (normalMap != null)
            {
                result.SetTexture("_BumpMap", normalMap);
                result.SetFloat("_BumpScale", 1f);
                result.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(result);
            return result;
        }

        private static Material CreateMaterial(string name, Color color, float metallic, float smoothness, Color? emission = null)
        {
            var path = $"{MaterialPath}/{name}.mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null)
            {
                result = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(result, path);
            }

            result.SetColor("_BaseColor", color);
            result.SetFloat("_Metallic", metallic);
            result.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                result.EnableKeyword("_EMISSION");
                result.SetColor("_EmissionColor", emission.Value);
            }
            EditorUtility.SetDirty(result);
            return result;
        }

        private static Material CreateTransparentMaterial(string name, Color color)
        {
            var result = CreateMaterial(name, color, 0f, 0.88f);
            result.SetFloat("_Surface", 1f);
            result.SetFloat("_Blend", 0f);
            result.SetFloat("_ZWrite", 0f);
            result.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            result.renderQueue = 3000;
            EditorUtility.SetDirty(result);
            return result;
        }

        private static ExperimentDefinition CreateExperimentDefinition()
        {
            var path = DataPath + "/Team5_ExperimentDefinition.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ExperimentDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ExperimentDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            var wells = new WellDefinition[96];
            for (var row = 0; row < 8; row++)
            {
                for (var column = 0; column < 12; column++)
                {
                    var index = (row * 12) + column;
                    var type = index == 0 ? WellType.PositiveControl :
                        index == 27 ? WellType.NoTemplateControl : index < 27 ? WellType.Sample : WellType.Unused;
                    var sampleId = type == WellType.PositiveControl ? "PTC" :
                        type == WellType.NoTemplateControl ? "NTC" : type == WellType.Sample ? $"S-{index:00}" : "UNUSED";
                    wells[index] = new WellDefinition
                    {
                        Row = row,
                        Column = column,
                        WellId = $"{(char)('A' + row)}{column + 1}",
                        SampleId = sampleId,
                        Type = type,
                        DisplayColor = type == WellType.PositiveControl ? Teal :
                            type == WellType.NoTemplateControl ? Red : type == WellType.Sample ? Blue : Hex("26383E")
                    };
                }
            }

            asset.Configure("Team 5 Educational SYBR Green qPCR", "Representative educational data only — not for diagnosis.", wells);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static RunProtocol CreateRunProtocol()
        {
            var path = DataPath + "/Team5_RunProtocol.asset";
            var asset = AssetDatabase.LoadAssetAtPath<RunProtocol>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<RunProtocol>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.Configure(new RunConfiguration(), 30f);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static PlateHandoffData CreatePlateHandoff()
        {
            var path = DataPath + "/Team4_PlateHandoff.asset";
            var asset = AssetDatabase.LoadAssetAtPath<PlateHandoffData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<PlateHandoffData>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.Configure("T4-QPCR-2026-05", true, true, true, true, 28);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static DialogueSequence CreateDialogueSequence()
        {
            var path = DataPath + "/Team5_DialogueSequence.asset";
            var asset = AssetDatabase.LoadAssetAtPath<DialogueSequence>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<DialogueSequence>();
                AssetDatabase.CreateAsset(asset, path);
            }

            var entries = new DialogueEntry[10];
            for (var index = 0; index < entries.Length; index++)
            {
                var stage = (WorkflowStage)index;
                var content = WorkflowContent.For(stage);
                entries[index] = new DialogueEntry
                {
                    Stage = stage, Title = content.Title, Instruction = content.Instruction,
                    Why = content.Why, Action = content.Action
                };
            }

            asset.Configure(entries);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static ShellReferences BuildLaboratoryShell()
        {
            var root = new GameObject("LABORATORY_SHELL_12m_x_9m").transform;
            Cube("Epoxy_Floor", new Vector3(0f, -0.08f, 0f), new Vector3(12f, 0.16f, 9f), materials.Epoxy, root);
            Cube("Back_Wall", new Vector3(0f, 1.6f, 4.45f), new Vector3(12f, 3.2f, 0.1f), materials.Wall, root);
            Cube("Left_Wall", new Vector3(-5.95f, 1.6f, 0f), new Vector3(0.1f, 3.2f, 9f), materials.Wall, root);
            Cube("Right_Wall", new Vector3(5.95f, 1.6f, 0f), new Vector3(0.1f, 3.2f, 9f), materials.Wall, root);

            var frontUpper = new GameObject("REMOVABLE_UPPER_FRONT_WALL");
            frontUpper.transform.SetParent(root, false);
            Cube("Front_Header", new Vector3(0f, 1.25f, -4.45f), new Vector3(12f, 0.7f, 0.1f), materials.Wall, frontUpper.transform);
            Cube("Front_Left_Return", new Vector3(-5.25f, -0.35f, -4.45f), new Vector3(1.4f, 2.5f, 0.1f), materials.Wall, frontUpper.transform);
            Cube("Front_Right_Return", new Vector3(5.25f, -0.35f, -4.45f), new Vector3(1.4f, 2.5f, 0.1f), materials.Wall, frontUpper.transform);

            var ceiling = new GameObject("REMOVABLE_CEILING");
            ceiling.transform.SetParent(root, false);
            Cube("Ceiling_Slab", new Vector3(0f, 3.18f, 0f), new Vector3(12f, 0.12f, 9f), materials.Wall, ceiling.transform);
            for (var x = -3.8f; x <= 3.8f; x += 3.8f)
            {
                for (var z = -2.6f; z <= 2.6f; z += 2.6f)
                {
                    Cube($"LED_Panel_{x:0}_{z:0}", new Vector3(x, 3.08f, z), new Vector3(1.5f, 0.04f, 0.62f), materials.Teal, ceiling.transform);
                }
            }

            for (var x = -3.7f; x <= 3.7f; x += 3.7f)
            {
                Cube($"Window_Frame_{x:0}", new Vector3(x, 2.04f, 4.37f), new Vector3(2.35f, 1.35f, 0.08f), materials.Steel, root);
                Cube($"Window_Glass_{x:0}", new Vector3(x, 2.04f, 4.30f), new Vector3(2.12f, 1.15f, 0.035f), materials.Glass, root);
            }

            Cube("Door_Frame", new Vector3(-4.45f, 1.25f, -4.36f), new Vector3(1.45f, 2.5f, 0.12f), materials.Steel, root);
            Cube("Door_Glass", new Vector3(-4.45f, 1.25f, -4.25f), new Vector3(1.24f, 2.25f, 0.05f), materials.Glass, root);
            Cube("Door_Handle", new Vector3(-3.98f, 1.25f, -4.14f), new Vector3(0.06f, 0.42f, 0.06f), materials.Steel, root);

            return new ShellReferences { Ceiling = ceiling, UpperFrontWall = frontUpper };
        }

        private static void BuildLaboratoryFurniture()
        {
            var root = new GameObject("LAB_FURNITURE_AND_STORAGE").transform;
            Cube("Back_Bench_Worktop", new Vector3(0f, 0.92f, 3.55f), new Vector3(10.8f, 0.12f, 1.35f), materials.Worktop, root);
            Cube("Left_Bench_Worktop", new Vector3(-5.25f, 0.92f, 0.15f), new Vector3(1.25f, 0.12f, 5.6f), materials.Worktop, root);

            for (var x = -4.6f; x <= 4.6f; x += 1.15f)
            {
                Cube($"Base_Cabinet_{x:0.0}", new Vector3(x, 0.43f, 3.74f), new Vector3(1.04f, 0.86f, 0.88f), materials.Cabinet, root);
                Cube($"Cabinet_Handle_{x:0.0}", new Vector3(x, 0.58f, 3.27f), new Vector3(0.34f, 0.035f, 0.035f), materials.Steel, root);
            }

            for (var z = -2.15f; z <= 2.15f; z += 1.1f)
            {
                Cube($"Side_Cabinet_{z:0.0}", new Vector3(-5.45f, 0.43f, z), new Vector3(0.8f, 0.86f, 1f), materials.Cabinet, root);
                Cube($"Side_Drawer_Handle_{z:0.0}", new Vector3(-5.0f, 0.62f, z), new Vector3(0.035f, 0.035f, 0.34f), materials.Steel, root);
            }

            var sink = Cube("Handwashing_Sink", new Vector3(-4.35f, 1.01f, 3.5f), new Vector3(1.35f, 0.12f, 0.95f), materials.Steel, root);
            Cube("Sink_Basin", new Vector3(0f, 0.04f, 0f), new Vector3(0.9f, 0.08f, 0.58f), materials.Instrument, sink.transform);
            Cube("Faucet_Stem", new Vector3(0f, 0.38f, 0.28f), new Vector3(0.08f, 0.7f, 0.08f), materials.Steel, sink.transform);
            Cube("Faucet_Neck", new Vector3(0f, 0.7f, 0.05f), new Vector3(0.08f, 0.08f, 0.46f), materials.Steel, sink.transform);
            Cube("Soap_Dispenser", new Vector3(-3.45f, 1.34f, 4.1f), new Vector3(0.22f, 0.5f, 0.18f), materials.Teal, root);

            var ppe = new GameObject("PPE_AND_LAB_COAT_AREA").transform;
            ppe.SetParent(root, false);
            ppe.localPosition = new Vector3(4.85f, 0f, -3.45f);
            Cube("PPE_Backboard", new Vector3(0f, 1.65f, 0.38f), new Vector3(1.55f, 2.5f, 0.1f), materials.Cabinet, ppe);
            for (var index = 0; index < 3; index++)
            {
                Cube($"Coat_{index + 1}", new Vector3(-0.47f + index * 0.47f, 1.45f, 0.19f), new Vector3(0.34f, 1.55f, 0.08f), materials.Wall, ppe);
                Cylinder($"Coat_Hook_{index + 1}", new Vector3(-0.47f + index * 0.47f, 2.3f, 0.11f), new Vector3(0.05f, 0.12f, 0.05f), materials.Steel, ppe, Quaternion.Euler(90f, 0f, 0f));
            }

            Cylinder("Biohazard_Waste", new Vector3(4.4f, 0.42f, -1.9f), new Vector3(0.52f, 0.42f, 0.52f), materials.Biohazard, root);
            Cylinder("Ordinary_Waste", new Vector3(5.2f, 0.42f, -1.9f), new Vector3(0.52f, 0.42f, 0.52f), materials.Waste, root);
            WorldLabel("BIOHAZARD", new Vector3(4.4f, 0.48f, -2.43f), 0.11f, Hex("332B05"), root);
            WorldLabel("GENERAL", new Vector3(5.2f, 0.48f, -2.43f), 0.11f, Ink, root);

            WorldLabel("TEAM 5 · REAL-TIME PCR STATION", new Vector3(2.7f, 2.75f, 4.28f), 0.24f, Teal, root);
            WorldLabel("HAND WASHING", new Vector3(-4.35f, 2.55f, 4.28f), 0.15f, Hex("25454B"), root);
        }

        private static void BuildBackgroundEquipment()
        {
            var root = new GameObject("BACKGROUND_EQUIPMENT_NON_INTERACTIVE").transform;

            var centrifuge = Cube("Plate_Centrifuge", new Vector3(-2.9f, 1.24f, 3.52f), new Vector3(1.2f, 0.58f, 0.82f), materials.Instrument, root);
            Cylinder("Centrifuge_Lid", new Vector3(0f, 0.32f, 0f), new Vector3(0.5f, 0.08f, 0.5f), materials.Cabinet, centrifuge.transform);
            Cube("Centrifuge_Display", new Vector3(0f, 0.05f, -0.43f), new Vector3(0.42f, 0.18f, 0.03f), materials.Teal, centrifuge.transform);

            var coldBlock = Cube("Cold_Block", new Vector3(-1.5f, 1.03f, 3.4f), new Vector3(0.85f, 0.14f, 0.58f), materials.Steel, root);
            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column < 5; column++)
                {
                    Cylinder($"ColdBlock_Tube_{row}_{column}", new Vector3(-0.3f + column * 0.15f, 0.16f, -0.16f + row * 0.16f),
                        new Vector3(0.065f, 0.18f, 0.065f), column % 2 == 0 ? materials.LiquidSample : materials.LiquidControl,
                        coldBlock.transform);
                }
            }

            for (var rackIndex = 0; rackIndex < 2; rackIndex++)
            {
                var rack = Cube($"Tube_Rack_{rackIndex + 1}", new Vector3(-4.2f, 1.02f, -1.05f + rackIndex * 1.45f),
                    new Vector3(0.72f, 0.14f, 1.05f), materials.Blue, root);
                for (var row = 0; row < 4; row++)
                {
                    for (var column = 0; column < 3; column++)
                    {
                        Cylinder($"Microtube_{row}_{column}", new Vector3(-0.2f + column * 0.2f, 0.24f, -0.36f + row * 0.24f),
                            new Vector3(0.065f, 0.24f, 0.065f), row % 2 == 0 ? materials.LiquidSample : materials.LiquidControl,
                            rack.transform);
                    }
                }
            }

            var pipettePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Processed/Mechanical_Pipette_Processed.prefab");
            var stand = Cube("Pipette_Stand", new Vector3(-4.85f, 1.15f, 1.7f), new Vector3(0.42f, 0.45f, 0.85f), materials.Steel, root);
            for (var index = 0; index < 3; index++)
            {
                if (pipettePrefab == null) continue;
                var pipette = (GameObject)PrefabUtility.InstantiatePrefab(pipettePrefab);
                pipette.name = $"Calibrated_Pipette_{index + 1}";
                pipette.transform.SetParent(stand.transform, false);
                pipette.transform.localPosition = new Vector3(0f, 0.55f + index * 0.12f, -0.25f + index * 0.25f);
                pipette.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
                pipette.transform.localScale = Vector3.one * 0.22f;
                foreach (var renderer in pipette.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterial = materials.Pipette;
                }
            }

            Cube("Computer_Monitor", new Vector3(4.82f, 1.72f, 3.7f), new Vector3(1.45f, 0.9f, 0.12f), materials.Instrument, root);
            Cube("Computer_Screen", new Vector3(4.82f, 1.72f, 3.62f), new Vector3(1.24f, 0.7f, 0.025f), materials.Teal, root);
            Cube("Monitor_Stand", new Vector3(4.82f, 1.15f, 3.72f), new Vector3(0.12f, 0.42f, 0.12f), materials.Steel, root);
            Cube("Keyboard", new Vector3(4.82f, 1.02f, 3.02f), new Vector3(1.25f, 0.07f, 0.42f), materials.Instrument, root);
        }

        private static Camera BuildLightingAndCamera()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("D6E6E7");
            RenderSettings.ambientEquatorColor = Hex("879A9D");
            RenderSettings.ambientGroundColor = Hex("34444A");
            RenderSettings.ambientIntensity = 0.48f;
            RenderSettings.skybox = null;

            var lighting = new GameObject("CLINICAL_LIGHTING").transform;
            var sun = new GameObject("Soft_Directional_Light").AddComponent<Light>();
            sun.transform.SetParent(lighting, false);
            sun.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
            sun.type = LightType.Directional;
            sun.color = new Color(0.92f, 0.98f, 1f);
            sun.intensity = 0.42f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.65f;

            var ceilingPositions = new[]
            {
                new Vector3(-3.5f, 2.85f, -2.3f), new Vector3(0f, 2.85f, -2.3f), new Vector3(3.5f, 2.85f, -2.3f),
                new Vector3(-3.5f, 2.85f, 2.2f), new Vector3(0f, 2.85f, 2.2f), new Vector3(3.5f, 2.85f, 2.2f)
            };
            for (var index = 0; index < ceilingPositions.Length; index++)
            {
                var light = new GameObject($"Clinical_Area_Light_{index + 1}").AddComponent<Light>();
                light.transform.SetParent(lighting, false);
                light.transform.position = ceilingPositions[index];
                light.type = LightType.Point;
                light.range = 6.2f;
                light.intensity = 46f;
                light.color = new Color(0.78f, 0.91f, 1f);
                light.shadows = index % 2 == 0 ? LightShadows.Soft : LightShadows.None;
            }

            var probe = new GameObject("Laboratory_Reflection_Probe").AddComponent<ReflectionProbe>();
            probe.transform.position = new Vector3(0f, 1.5f, 0f);
            probe.size = new Vector3(11.5f, 3f, 8.5f);
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution = 128;
            probe.intensity = 0.75f;

            var profilePath = DataPath + "/Team5_Realistic_PostProcessing.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            profile.components.Clear();
            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.ACES);
            var color = profile.Add<ColorAdjustments>();
            color.postExposure.Override(-0.32f);
            color.contrast.Override(7f);
            color.saturation.Override(-4f);
            var bloom = profile.Add<Bloom>();
            bloom.intensity.Override(0.14f);
            bloom.threshold.Override(1.15f);
            bloom.scatter.Override(0.55f);
            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.08f);
            vignette.smoothness.Override(0.36f);
            EditorUtility.SetDirty(profile);
            var volume = new GameObject("Clinical_PostProcessing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;

            var cameraObject = new GameObject("Desktop_Overview_Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Hex("18282D");
            camera.fieldOfView = 48f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 60f;
            var additional = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            additional.renderPostProcessing = true;
            additional.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            return camera;
        }

        private static PlateReferences BuildPreparedPlate(ExperimentDefinition experiment, PlateHandoffData handoff)
        {
            var station = new GameObject("TEAM4_HANDOFF_PLATE_STATION").transform;
            var home = new GameObject("Prepared_Plate_Home_Anchor").transform;
            home.SetParent(station, false);
            home.position = new Vector3(0.25f, 1.12f, 3.25f);

            var root = new GameObject("T4_QPCR_2026_05_PREPARED_PLATE");
            root.transform.SetParent(home, false);
            var controller = root.AddComponent<PlateController>();
            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            rigidbody.isKinematic = false;
            rigidbody.mass = 0.18f;
            rigidbody.linearDamping = 8f;
            rigidbody.angularDamping = 8f;
            var grab = root.AddComponent<XRGrabInteractable>();
            grab.throwOnDetach = false;

            var basePlate = Cube("Polypropylene_96_Well_Plate", new Vector3(0f, 0f, 0f), new Vector3(1.65f, 0.11f, 1.05f), materials.Plate, root.transform);
            var baseCollider = basePlate.GetComponent<BoxCollider>();
            if (baseCollider != null) baseCollider.size = new Vector3(1f, 1.2f, 1f);

            var wellsRoot = new GameObject("ALL_96_WELLS_8x12").transform;
            wellsRoot.SetParent(root.transform, false);
            wellsRoot.localPosition = new Vector3(0f, 0.08f, 0f);
            var renderers = new Renderer[96];
            for (var row = 0; row < 8; row++)
            {
                for (var column = 0; column < 12; column++)
                {
                    var index = row * 12 + column;
                    var definition = experiment.Wells[index];
                    var liquidMaterial = definition.Type == WellType.PositiveControl ? materials.LiquidControl :
                        definition.Type == WellType.NoTemplateControl ? materials.Red :
                        definition.Type == WellType.Sample ? materials.LiquidSample : materials.UnusedWell;
                    var well = Cylinder(definition.WellId + "_" + definition.SampleId,
                        new Vector3(-0.72f + column * 0.131f, 0f, 0.45f - row * 0.128f),
                        new Vector3(0.075f, 0.028f, 0.075f), liquidMaterial, wellsRoot);
                    RemoveCollider(well);
                    renderers[index] = well.GetComponent<Renderer>();
                }
            }

            var seal = Cube("Transparent_Optical_Seal", new Vector3(0f, 0.12f, 0f), new Vector3(1.58f, 0.025f, 0.98f),
                materials.Seal, root.transform);
            RemoveCollider(seal);
            var marker = Cube("A1_ORIENTATION_MARKER", new Vector3(-0.79f, 0.17f, 0.49f), new Vector3(0.1f, 0.025f, 0.1f),
                materials.Red, root.transform).transform;
            WorldLabel("A1", new Vector3(-0.79f, 0.2f, 0.42f), 0.075f, Red, root.transform);
            WorldLabel("PLATE ID: T4-QPCR-2026-05", new Vector3(0f, 0.2f, -0.52f), 0.075f, Hex("183238"), root.transform);

            controller.Configure(experiment, handoff, wellsRoot, renderers, seal.GetComponent<Renderer>(), marker);
            return new PlateReferences { Root = root, Controller = controller, HomeAnchor = home };
        }

        private static InstrumentController BuildInstrument(PlateReferences plate)
        {
            var root = new GameObject("TEAM5_REAL_TIME_QPCR_INSTRUMENT");
            root.transform.position = new Vector3(2.75f, 1.0f, 3.35f);

            Cube("Instrument_Main_Housing", new Vector3(0f, 0.62f, 0.05f), new Vector3(2.35f, 1.25f, 1.3f), materials.Instrument, root.transform);
            Cube("Instrument_Top", new Vector3(0f, 1.3f, 0.16f), new Vector3(2.15f, 0.18f, 1.08f), materials.Cabinet, root.transform);
            Cube("Touchscreen_Bezel", new Vector3(0f, 0.78f, -0.68f), new Vector3(1.28f, 0.58f, 0.08f), materials.Rubber, root.transform);
            Cube("Touchscreen_Glass", new Vector3(0f, 0.78f, -0.735f), new Vector3(1.14f, 0.46f, 0.02f), materials.Teal, root.transform);
            WorldLabel("REAL-TIME PCR\n35-CYCLE PROTOCOL", new Vector3(0f, 0.8f, -0.755f), 0.1f, Ink, root.transform);
            for (var x = -0.92f; x <= 0.92f; x += 1.84f)
            {
                for (var z = -0.42f; z <= 0.42f; z += 0.84f)
                {
                    Cylinder("Rubber_Foot", new Vector3(x, -0.05f, z), new Vector3(0.11f, 0.06f, 0.11f), materials.Rubber, root.transform);
                }
            }

            var drawer = Cube("Motorized_Plate_Drawer", new Vector3(0f, 0.21f, -0.72f), new Vector3(1.92f, 0.25f, 0.92f),
                materials.Steel, root.transform).transform;
            Cube("Drawer_Thermal_Block", new Vector3(0f, 0.15f, 0f), new Vector3(1.68f, 0.12f, 0.74f), materials.Rubber, drawer);
            var plateAnchor = new GameObject("A1_ALIGNED_PLATE_ANCHOR").transform;
            plateAnchor.SetParent(drawer, false);
            plateAnchor.localPosition = new Vector3(0f, 0.27f, 0f);
            plateAnchor.localRotation = Quaternion.identity;

            var powerIndicator = Sphere("Power_Status_LED", new Vector3(0.92f, 0.78f, -0.76f), new Vector3(0.075f, 0.075f, 0.075f),
                materials.Teal, root.transform).GetComponent<Renderer>();
            Cube("Emergency_Stop", new Vector3(-0.93f, 0.78f, -0.76f), new Vector3(0.16f, 0.16f, 0.08f), materials.Red, root.transform);

            var sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Processed/Team5_qPCR_Instrument_Processed.prefab");
            if (sourceModel != null)
            {
                var detailModel = (GameObject)PrefabUtility.InstantiatePrefab(sourceModel);
                detailModel.name = "Original_Team5_Instrument_Model_Detail";
                detailModel.transform.SetParent(root.transform, false);
                detailModel.transform.localPosition = new Vector3(0f, 0.58f, 0.1f);
                detailModel.transform.localRotation = Quaternion.identity;
                detailModel.transform.localScale = Vector3.one * 0.34f;
                foreach (var collider in detailModel.GetComponentsInChildren<Collider>())
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                }
            }

            var controller = root.AddComponent<InstrumentController>();
            controller.Configure(plate.Root.transform, plate.HomeAnchor, plateAnchor, drawer, powerIndicator);
            return controller;
        }

        private static DnaCycleVisualizer BuildDnaVisualizer()
        {
            var root = new GameObject("DNA_CYCLE_VISUALIZER");
            root.transform.position = new Vector3(0.65f, 1.62f, 3.94f);
            Cube("Visualizer_Backplate", Vector3.zero, new Vector3(1.55f, 1.0f, 0.08f), materials.Instrument, root.transform);
            var label = WorldLabel("THERMAL CYCLE VISUALIZER\nWaiting for run", new Vector3(0f, 0.35f, -0.06f), 0.105f, Ink, root.transform);

            var denaturation = new GameObject("Denaturation_Separated_Strands");
            denaturation.transform.SetParent(root.transform, false);
            Cube("Red_Strand_Left", new Vector3(-0.25f, -0.12f, -0.08f), new Vector3(0.08f, 0.58f, 0.05f), materials.Red, denaturation.transform)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            Cube("Red_Strand_Right", new Vector3(0.25f, -0.12f, -0.08f), new Vector3(0.08f, 0.58f, 0.05f), materials.Red, denaturation.transform)
                .transform.localRotation = Quaternion.Euler(0f, 0f, -12f);

            var annealing = new GameObject("Annealing_Primers_Attach");
            annealing.transform.SetParent(root.transform, false);
            Cube("Blue_Template_Left", new Vector3(-0.14f, -0.12f, -0.08f), new Vector3(0.06f, 0.58f, 0.05f), materials.Blue, annealing.transform);
            Cube("Blue_Template_Right", new Vector3(0.14f, -0.12f, -0.08f), new Vector3(0.06f, 0.58f, 0.05f), materials.Blue, annealing.transform);
            Cube("Primer_Left", new Vector3(-0.23f, -0.27f, -0.09f), new Vector3(0.22f, 0.06f, 0.05f), materials.Amber, annealing.transform);
            Cube("Primer_Right", new Vector3(0.23f, 0.03f, -0.09f), new Vector3(0.22f, 0.06f, 0.05f), materials.Amber, annealing.transform);

            var extension = new GameObject("Extension_New_DNA");
            extension.transform.SetParent(root.transform, false);
            for (var index = 0; index < 4; index++)
            {
                Cube($"Green_DNA_{index}", new Vector3(-0.3f + index * 0.2f, -0.12f, -0.08f), new Vector3(0.055f, 0.58f, 0.05f),
                    materials.Teal, extension.transform);
            }

            var controller = root.AddComponent<DnaCycleVisualizer>();
            controller.Configure(denaturation, annealing, extension, label);
            return controller;
        }

        private static XrReferences BuildXrRig(GameObject plate)
        {
            var systems = new GameObject("XR_INTERACTION_SYSTEMS");
            systems.AddComponent<XRInteractionManager>();
            var actions = systems.AddComponent<InputActionManager>();
            var defaultActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/XRI Default Input Actions.inputactions");
            if (defaultActions != null)
            {
                actions.actionAssets = new List<InputActionAsset> { defaultActions };
            }

            var originPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/Prefabs/XR Origin (XR Rig).prefab");
            GameObject origin;
            if (originPrefab != null)
            {
                origin = (GameObject)PrefabUtility.InstantiatePrefab(originPrefab);
            }
            else
            {
                origin = new GameObject("XR Origin (Fallback - install Starter Assets)");
                Debug.LogWarning("[Team 5 Realistic Lab] XR Origin prefab missing; fallback root created.");
            }
            origin.name = "XR_ORIGIN_LEFT_RIGHT_CONTROLLERS";
            origin.transform.position = new Vector3(0f, 0f, -2.4f);

            var simulatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Samples/XR Interaction Toolkit/3.3.2/XR Interaction Simulator/XR Interaction Simulator.prefab");
            GameObject simulator = null;
            if (simulatorPrefab != null)
            {
                simulator = (GameObject)PrefabUtility.InstantiatePrefab(simulatorPrefab);
                simulator.name = "XR_DEVICE_INTERACTION_SIMULATOR_EDITOR_ONLY";
                simulator.tag = "EditorOnly";
                var simulatorComponent = simulator.GetComponent<UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRInteractionSimulator>();
                simulator.AddComponent<XRInteractionSimulatorInputBridge>().Configure(simulatorComponent);
            }

            origin.SetActive(false);
            if (simulator != null) simulator.SetActive(false);
            return new XrReferences { Origin = origin, Simulator = simulator };
        }

        private static GameObject FindPrefab(string name, string folder)
        {
            var guids = AssetDatabase.FindAssets($"{name} t:Prefab", new[] { folder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return prefab;
                }
            }

            return null;
        }

        private static void ConfigureCameraOrbit(Camera camera)
        {
            var focus = new GameObject("Desktop_Camera_Focus").transform;
            focus.position = new Vector3(0.8f, 1.2f, 2.0f);
            var orbit = camera.gameObject.AddComponent<CameraOrbitController>();
            orbit.Configure(focus, -10f, 12f, 9.2f);
            EditorUtility.SetDirty(orbit);
        }

        private static InterfaceReferences BuildInterface(
            Camera camera,
            WorkflowController workflow,
            RunProtocol protocol,
            ProtocolSetupController protocolSetup)
        {
            var canvasObject = new GameObject("COMPACT_DESKTOP_AND_XR_UI");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();

            BuildEventSystem();

            var topBar = PanelRect("Top_Status_Bar", canvasObject.transform, new Color(0.025f, 0.066f, 0.078f, 0.96f),
                new Vector2(0f, 0.925f), Vector2.one, Vector2.zero, Vector2.zero);
            var projectTitle = UiText("Project_Title", topBar, "TEAM 5  ·  REAL-TIME PCR LABORATORY", 26f, FontStyles.Bold, Ink,
                TextAlignmentOptions.Left);
            SetRect(projectTitle.rectTransform, new Vector2(0f, 0f), new Vector2(0.55f, 1f), new Vector2(28f, 0f), new Vector2(-8f, 0f));
            var disclaimer = UiText("Scientific_Disclaimer", topBar, string.Empty, 16f, FontStyles.Bold, Amber, TextAlignmentOptions.Right);
            SetRect(disclaimer.rectTransform, new Vector2(0.52f, 0f), Vector2.one, new Vector2(0f, 0f), new Vector2(-28f, 0f));

            var mentorPanel = PanelRect("Reusable_Mentor_Dialogue", canvasObject.transform, Panel,
                new Vector2(0.018f, 0.105f), new Vector2(0.305f, 0.895f), Vector2.zero, Vector2.zero);
            AddOutline(mentorPanel.gameObject, new Color(0.15f, 0.7f, 0.66f, 0.32f), 1.5f);
            var stageCounter = UiText("Step_Counter", mentorPanel, "STEP 01 / 10", 16f, FontStyles.Bold, Teal, TextAlignmentOptions.Left);
            SetRect(stageCounter.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -52f), new Vector2(-24f, -18f));
            var title = UiText("Stage_Title", mentorPanel, "Welcome", 31f, FontStyles.Bold, Ink, TextAlignmentOptions.Left);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -132f), new Vector2(-24f, -56f));
            var instruction = UiText("Short_Instruction", mentorPanel, string.Empty, 18f, FontStyles.Normal, Ink, TextAlignmentOptions.TopLeft);
            instruction.enableWordWrapping = true;
            instruction.lineSpacing = 4f;
            SetRect(instruction.rectTransform, new Vector2(0f, 0.53f), new Vector2(1f, 0.82f), new Vector2(24f, 0f), new Vector2(-24f, 0f));

            var whyButton = ButtonRect("Why_Button", mentorPanel, "WHY?", new Color(0.11f, 0.2f, 0.23f, 0.96f), Teal,
                new Vector2(0f, 0.45f), new Vector2(0.28f, 0.52f), new Vector2(24f, 0f), new Vector2(-4f, 0f), out _);
            var whyPanel = PanelRect("Optional_Why_Explanation", mentorPanel, new Color(0.055f, 0.15f, 0.17f, 0.98f),
                new Vector2(0.04f, 0.26f), new Vector2(0.96f, 0.49f), Vector2.zero, Vector2.zero).gameObject;
            var whyText = UiText("Why_Text", whyPanel.transform, string.Empty, 16f, FontStyles.Normal, Ink, TextAlignmentOptions.TopLeft);
            whyText.enableWordWrapping = true;
            SetRect(whyText.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 14f), new Vector2(-18f, -14f));
            whyPanel.SetActive(false);

            var status = UiText("Context_Status", mentorPanel, "Station ready.", 16f, FontStyles.Normal, Muted, TextAlignmentOptions.TopLeft);
            status.enableWordWrapping = true;
            SetRect(status.rectTransform, new Vector2(0f, 0.15f), new Vector2(1f, 0.255f), new Vector2(24f, 0f), new Vector2(-24f, 0f));
            var progress = SliderRect("Run_Progress", mentorPanel, new Vector2(0.04f, 0.115f), new Vector2(0.96f, 0.14f));
            var secondaryButton = ButtonRect("Secondary_Action", mentorPanel, "ALIGN A1", new Color(0.19f, 0.25f, 0.27f, 1f), Ink,
                new Vector2(0.04f, 0.025f), new Vector2(0.47f, 0.10f), Vector2.zero, Vector2.zero, out var secondaryLabel);
            var actionButton = ButtonRect("Primary_Action", mentorPanel, "BEGIN", Teal, Hex("062622"),
                new Vector2(0.50f, 0.025f), new Vector2(0.96f, 0.10f), Vector2.zero, Vector2.zero, out var actionLabel);

            var protocolPanel = BuildProtocolPanel(canvasObject.transform, protocol, protocolSetup);
            var resultsPanel = BuildResultsPanel(canvasObject.transform, out var curve, out var resultSummary, out var resultCallout);

            var mentor = mentorPanel.gameObject.AddComponent<MentorPanelController>();
            secondaryButton.gameObject.SetActive(false);
            protocolPanel.SetActive(false);
            resultsPanel.SetActive(false);

            return new InterfaceReferences
            {
                Root = canvasObject,
                Mentor = mentor,
                StageTitle = title,
                Instruction = instruction,
                StageCounter = stageCounter,
                Status = status,
                ActionLabel = actionLabel,
                SecondaryActionLabel = secondaryLabel,
                Disclaimer = disclaimer,
                ActionButton = actionButton,
                SecondaryActionButton = secondaryButton,
                WhyButton = whyButton,
                Progress = progress,
                WhyPanel = whyPanel,
                WhyText = whyText,
                ProtocolPanel = protocolPanel,
                ResultsPanel = resultsPanel,
                CurveGraphic = curve,
                ResultSummary = resultSummary,
                ResultCallout = resultCallout
            };
        }

        private static GameObject BuildProtocolPanel(Transform canvas, RunProtocol protocol, ProtocolSetupController controller)
        {
            var panel = PanelRect("Machine_Protocol_Touchscreen", canvas, Panel,
                new Vector2(0.64f, 0.105f), new Vector2(0.982f, 0.895f), Vector2.zero, Vector2.zero);
            AddOutline(panel.gameObject, new Color(0.15f, 0.7f, 0.66f, 0.32f), 1.5f);
            var heading = UiText("Protocol_Heading", panel, "qPCR RUN PROTOCOL", 29f, FontStyles.Bold, Ink, TextAlignmentOptions.Left);
            SetRect(heading.rectTransform, new Vector2(0f, 0.91f), new Vector2(1f, 0.985f), new Vector2(24f, 0f), new Vector2(-24f, 0f));
            var subheading = UiText("Protocol_Subheading", panel,
                "Enter each value. Hover or select a field, then validate from the mentor card.", 15f, FontStyles.Normal, Muted, TextAlignmentOptions.Left);
            SetRect(subheading.rectTransform, new Vector2(0f, 0.855f), new Vector2(1f, 0.92f), new Vector2(24f, 0f), new Vector2(-24f, 0f));

            var fieldNames = new[]
            {
                "Reaction volume (µL)", "Heated lid (°C)", "Initial hold (°C)", "Initial hold (s)",
                "Cycle count", "Denaturation (°C)", "Denaturation (s)", "Annealing (°C)",
                "Annealing (s)", "Extension (°C)", "Extension (s)", "Acquire at (°C)",
                "Melt start (°C)", "Melt end (°C)"
            };
            var inputFields = new TMP_InputField[fieldNames.Length];
            for (var index = 0; index < fieldNames.Length; index++)
            {
                var column = index / 7;
                var row = index % 7;
                var xMin = column == 0 ? 0.04f : 0.52f;
                var xMax = column == 0 ? 0.48f : 0.96f;
                var top = 0.82f - row * 0.095f;
                var label = UiText($"Label_{index:00}", panel, fieldNames[index], 14f, FontStyles.Normal, Muted, TextAlignmentOptions.Left);
                SetRect(label.rectTransform, new Vector2(xMin, top), new Vector2(xMax, top + 0.035f), Vector2.zero, Vector2.zero);
                inputFields[index] = InputRect($"Protocol_Field_{index:00}", panel, new Vector2(xMin, top - 0.052f), new Vector2(xMax, top), "0");
            }

            var fluorLabel = UiText("Fluorophore_Label", panel, "Fluorescence chemistry", 14f, FontStyles.Normal, Muted, TextAlignmentOptions.Left);
            SetRect(fluorLabel.rectTransform, new Vector2(0.04f, 0.092f), new Vector2(0.48f, 0.125f), Vector2.zero, Vector2.zero);
            var fluorophore = DropdownRect("Fluorophore_Dropdown", panel, new Vector2(0.04f, 0.035f), new Vector2(0.48f, 0.09f));
            var feedback = UiText("Protocol_Validation_Feedback", panel, "Check each value, then validate the protocol.", 15f,
                FontStyles.Bold, Teal, TextAlignmentOptions.TopLeft);
            feedback.enableWordWrapping = true;
            SetRect(feedback.rectTransform, new Vector2(0.52f, 0.025f), new Vector2(0.96f, 0.125f), Vector2.zero, Vector2.zero);

            controller.Configure(protocol, panel.gameObject, inputFields, fluorophore, feedback);
            return panel.gameObject;
        }

        private static XrInterfaceReferences BuildXrInterface(
            WorkflowController workflow,
            DialogueSequence dialogue,
            ProtocolSetupController protocolSetup)
        {
            var root = new GameObject("XR_WORLD_SPACE_LEARNING_UI", typeof(RectTransform), typeof(Canvas));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 30;
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1200f, 650f);
            root.transform.position = new Vector3(0.1f, 1.55f, 0.25f);
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one * 0.002f;
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<TrackedDeviceGraphicRaycaster>();

            var mentorPanel = PanelRect("XR_Mentor_Card", root.transform, Panel,
                new Vector2(0.01f, 0.02f), new Vector2(0.43f, 0.98f), Vector2.zero, Vector2.zero);
            AddOutline(mentorPanel.gameObject, new Color(0.15f, 0.7f, 0.66f, 0.45f), 2f);
            var counter = UiText("XR_Step", mentorPanel, "STEP 01 / 10", 20f, FontStyles.Bold, Teal, TextAlignmentOptions.Left);
            SetRect(counter.rectTransform, new Vector2(0f, 0.9f), new Vector2(1f, 0.98f), new Vector2(22f, 0f), new Vector2(-22f, 0f));
            var title = UiText("XR_Title", mentorPanel, "Welcome", 34f, FontStyles.Bold, Ink, TextAlignmentOptions.TopLeft);
            SetRect(title.rectTransform, new Vector2(0f, 0.76f), new Vector2(1f, 0.91f), new Vector2(22f, 0f), new Vector2(-22f, 0f));
            var instruction = UiText("XR_Instruction", mentorPanel, string.Empty, 22f, FontStyles.Normal, Ink, TextAlignmentOptions.TopLeft);
            instruction.textWrappingMode = TextWrappingModes.Normal;
            SetRect(instruction.rectTransform, new Vector2(0f, 0.43f), new Vector2(1f, 0.76f), new Vector2(22f, 0f), new Vector2(-22f, 0f));

            var whyButton = ButtonRect("XR_Why", mentorPanel, "WHY?", new Color(0.1f, 0.2f, 0.23f, 1f), Teal,
                new Vector2(0.045f, 0.35f), new Vector2(0.31f, 0.42f), Vector2.zero, Vector2.zero, out _);
            var whyPanel = PanelRect("XR_Why_Panel", mentorPanel, new Color(0.055f, 0.15f, 0.17f, 1f),
                new Vector2(0.04f, 0.19f), new Vector2(0.96f, 0.35f), Vector2.zero, Vector2.zero).gameObject;
            var whyText = UiText("XR_Why_Text", whyPanel.transform, string.Empty, 18f, FontStyles.Normal, Ink, TextAlignmentOptions.TopLeft);
            whyText.textWrappingMode = TextWrappingModes.Normal;
            SetRect(whyText.rectTransform, Vector2.zero, Vector2.one, new Vector2(15f, 10f), new Vector2(-15f, -10f));
            whyPanel.SetActive(false);
            var status = UiText("XR_Status", mentorPanel, "Station ready.", 18f, FontStyles.Normal, Muted, TextAlignmentOptions.TopLeft);
            status.textWrappingMode = TextWrappingModes.Normal;
            SetRect(status.rectTransform, new Vector2(0.04f, 0.10f), new Vector2(0.96f, 0.18f), Vector2.zero, Vector2.zero);
            var progress = SliderRect("XR_Progress", mentorPanel, new Vector2(0.04f, 0.075f), new Vector2(0.96f, 0.095f));
            var secondary = ButtonRect("XR_Secondary", mentorPanel, "ALIGN A1", new Color(0.18f, 0.25f, 0.27f, 1f), Ink,
                new Vector2(0.04f, 0.015f), new Vector2(0.48f, 0.065f), Vector2.zero, Vector2.zero, out var secondaryLabel);
            var primary = ButtonRect("XR_Primary", mentorPanel, "BEGIN", Teal, Hex("062622"),
                new Vector2(0.51f, 0.015f), new Vector2(0.96f, 0.065f), Vector2.zero, Vector2.zero, out var actionLabel);

            var protocolPanel = BuildXrProtocolPanel(root.transform, protocolSetup);
            var resultsPanel = BuildResultsPanel(root.transform, out var curve, out var resultSummary, out var resultCallout);
            var mentor = mentorPanel.gameObject.AddComponent<MentorPanelController>();
            mentor.Configure(workflow, dialogue, title, instruction, counter, status, actionLabel, secondaryLabel, null,
                primary, secondary, whyButton, progress, whyPanel, whyText, protocolPanel, resultsPanel);

            secondary.gameObject.SetActive(false);
            protocolPanel.SetActive(false);
            resultsPanel.SetActive(false);
            root.SetActive(false);
            return new XrInterfaceReferences
            {
                Root = root,
                CurveGraphic = curve,
                ResultSummary = resultSummary,
                ResultCallout = resultCallout
            };
        }

        private static GameObject BuildXrProtocolPanel(Transform canvas, ProtocolSetupController controller)
        {
            var panel = PanelRect("XR_Protocol_Touchscreen", canvas, Panel,
                new Vector2(0.45f, 0.02f), new Vector2(0.99f, 0.98f), Vector2.zero, Vector2.zero);
            AddOutline(panel.gameObject, new Color(0.15f, 0.7f, 0.66f, 0.45f), 2f);
            var heading = UiText("XR_Protocol_Heading", panel, "qPCR RUN PROTOCOL", 30f, FontStyles.Bold, Ink, TextAlignmentOptions.Left);
            SetRect(heading.rectTransform, new Vector2(0.04f, 0.90f), new Vector2(0.96f, 0.98f), Vector2.zero, Vector2.zero);
            var names = new[]
            {
                "Volume µL", "Lid °C", "Hold °C", "Hold sec", "Cycles", "Denat °C", "Denat sec",
                "Anneal °C", "Anneal sec", "Extend °C", "Extend sec", "Acquire °C", "Melt start", "Melt end"
            };
            var fields = new TMP_InputField[names.Length];
            for (var index = 0; index < names.Length; index++)
            {
                var column = index / 7;
                var row = index % 7;
                var xMin = column == 0 ? 0.04f : 0.52f;
                var xMax = column == 0 ? 0.48f : 0.96f;
                var top = 0.86f - row * 0.105f;
                var label = UiText($"XR_Protocol_Label_{index:00}", panel, names[index], 17f, FontStyles.Normal, Muted, TextAlignmentOptions.Left);
                SetRect(label.rectTransform, new Vector2(xMin, top), new Vector2(xMax, top + 0.04f), Vector2.zero, Vector2.zero);
                fields[index] = InputRect($"XR_Protocol_Field_{index:00}", panel, new Vector2(xMin, top - 0.06f), new Vector2(xMax, top), "0");
            }

            var chemistryLabel = UiText("XR_Chemistry_Label", panel, "Fluorescence chemistry", 17f, FontStyles.Normal, Muted, TextAlignmentOptions.Left);
            SetRect(chemistryLabel.rectTransform, new Vector2(0.04f, 0.075f), new Vector2(0.48f, 0.115f), Vector2.zero, Vector2.zero);
            var dropdown = DropdownRect("XR_Fluorophore", panel, new Vector2(0.04f, 0.015f), new Vector2(0.48f, 0.075f));
            var feedback = UiText("XR_Protocol_Feedback", panel, "Check each value, then validate.", 17f, FontStyles.Bold, Teal, TextAlignmentOptions.TopLeft);
            feedback.textWrappingMode = TextWrappingModes.Normal;
            SetRect(feedback.rectTransform, new Vector2(0.52f, 0.015f), new Vector2(0.96f, 0.12f), Vector2.zero, Vector2.zero);
            controller.ConfigureSecondaryView(panel.gameObject, fields, dropdown, feedback);
            return panel.gameObject;
        }

        private static GameObject BuildResultsPanel(
            Transform canvas,
            out AmplificationCurveGraphic curve,
            out TMP_Text summary,
            out TMP_Text callout)
        {
            var panel = PanelRect("Dedicated_Results_Dialogue", canvas, Panel,
                new Vector2(0.59f, 0.105f), new Vector2(0.982f, 0.895f), Vector2.zero, Vector2.zero);
            AddOutline(panel.gameObject, new Color(0.2f, 0.55f, 0.9f, 0.34f), 1.5f);
            var heading = UiText("Results_Heading", panel, "AMPLIFICATION RESULTS", 29f, FontStyles.Bold, Ink, TextAlignmentOptions.Left);
            SetRect(heading.rectTransform, new Vector2(0f, 0.91f), new Vector2(1f, 0.985f), new Vector2(24f, 0f), new Vector2(-24f, 0f));
            summary = UiText("Run_Summary", panel, "AWAITING RUN", 18f, FontStyles.Bold, Teal, TextAlignmentOptions.Left);
            summary.enableWordWrapping = true;
            SetRect(summary.rectTransform, new Vector2(0.04f, 0.80f), new Vector2(0.96f, 0.9f), Vector2.zero, Vector2.zero);

            var graphPanel = PanelRect("Amplification_Graph_Background", panel, new Color(0.015f, 0.04f, 0.05f, 1f),
                new Vector2(0.04f, 0.31f), new Vector2(0.96f, 0.78f), Vector2.zero, Vector2.zero);
            curve = new GameObject("Progressive_35_Cycle_Curves", typeof(RectTransform), typeof(CanvasRenderer), typeof(AmplificationCurveGraphic))
                .GetComponent<AmplificationCurveGraphic>();
            curve.transform.SetParent(graphPanel, false);
            SetRect(curve.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var yAxis = UiText("Y_Axis", graphPanel, "ΔRn", 13f, FontStyles.Bold, Muted, TextAlignmentOptions.Left);
            SetRect(yAxis.rectTransform, new Vector2(0.01f, 0.88f), new Vector2(0.16f, 0.99f), Vector2.zero, Vector2.zero);
            var xAxis = UiText("X_Axis", graphPanel, "Cycle 1                                      Cycle 35", 13f, FontStyles.Normal, Muted, TextAlignmentOptions.Center);
            SetRect(xAxis.rectTransform, new Vector2(0.08f, 0f), new Vector2(0.98f, 0.09f), Vector2.zero, Vector2.zero);

            var legend = UiText("Curve_Legend", panel,
                "● Positive control     ● Representative positive     ● No-template control", 15f,
                FontStyles.Normal, Ink, TextAlignmentOptions.Left);
            SetRect(legend.rectTransform, new Vector2(0.04f, 0.245f), new Vector2(0.96f, 0.3f), Vector2.zero, Vector2.zero);
            callout = UiText("Scientific_Callout", panel,
                "Threshold 0.22 ΔRn\nRepresentative educational data — not for diagnosis.", 16f,
                FontStyles.Normal, Ink, TextAlignmentOptions.TopLeft);
            callout.enableWordWrapping = true;
            SetRect(callout.rectTransform, new Vector2(0.04f, 0.055f), new Vector2(0.96f, 0.225f), Vector2.zero, Vector2.zero);
            return panel.gameObject;
        }

        private static void BuildEventSystem()
        {
            var eventObject = new GameObject("Unified_Desktop_XR_EventSystem");
            eventObject.AddComponent<EventSystem>();
            var module = eventObject.AddComponent<XRUIInputModule>();
            var actionsPath = DataPath + "/Team5_UI_InputActions.asset";
            var existing = AssetDatabase.LoadAssetAtPath<InputActionAsset>(actionsPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(actionsPath);
            }

            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "Team5 UI Input Actions";
            var map = asset.AddActionMap("UI");
            var point = map.AddAction("Point", InputActionType.PassThrough, "<Mouse>/position", expectedControlLayout: "Vector2");
            var leftClick = map.AddAction("LeftClick", InputActionType.PassThrough, "<Mouse>/leftButton", expectedControlLayout: "Button");
            var middleClick = map.AddAction("MiddleClick", InputActionType.PassThrough, "<Mouse>/middleButton", expectedControlLayout: "Button");
            var rightClick = map.AddAction("RightClick", InputActionType.PassThrough, "<Mouse>/rightButton", expectedControlLayout: "Button");
            var scroll = map.AddAction("ScrollWheel", InputActionType.PassThrough, "<Mouse>/scroll", expectedControlLayout: "Vector2");
            var navigate = map.AddAction("Navigate", InputActionType.PassThrough, "<Gamepad>/leftStick", expectedControlLayout: "Vector2");
            var submit = map.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter", expectedControlLayout: "Button");
            submit.AddBinding("<Gamepad>/buttonSouth");
            var cancel = map.AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape", expectedControlLayout: "Button");
            cancel.AddBinding("<Gamepad>/buttonEast");
            AssetDatabase.CreateAsset(asset, actionsPath);

            module.pointAction = CreateActionReference(asset, point, "Point Reference");
            module.leftClickAction = CreateActionReference(asset, leftClick, "Left Click Reference");
            module.middleClickAction = CreateActionReference(asset, middleClick, "Middle Click Reference");
            module.rightClickAction = CreateActionReference(asset, rightClick, "Right Click Reference");
            module.scrollWheelAction = CreateActionReference(asset, scroll, "Scroll Reference");
            module.navigateAction = CreateActionReference(asset, navigate, "Navigate Reference");
            module.submitAction = CreateActionReference(asset, submit, "Submit Reference");
            module.cancelAction = CreateActionReference(asset, cancel, "Cancel Reference");
            EditorUtility.SetDirty(module);
            EditorUtility.SetDirty(asset);
        }

        private static InputActionReference CreateActionReference(InputActionAsset asset, InputAction action, string name)
        {
            var reference = InputActionReference.Create(action);
            reference.name = name;
            AssetDatabase.AddObjectToAsset(reference, asset);
            return reference;
        }

        private static RectTransform PanelRect(
            string name,
            Transform parent,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            target.transform.SetParent(parent, false);
            target.GetComponent<Image>().color = color;
            var rect = target.GetComponent<RectTransform>();
            SetRect(rect, anchorMin, anchorMax, offsetMin, offsetMax);
            return rect;
        }

        private static TMP_Text UiText(
            string name,
            Transform parent,
            string value,
            float size,
            FontStyles style,
            Color color,
            TextAlignmentOptions alignment)
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            target.transform.SetParent(parent, false);
            var text = target.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Truncate;
            return text;
        }

        private static Button ButtonRect(
            string name,
            Transform parent,
            string labelValue,
            Color background,
            Color foreground,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            out TMP_Text label)
        {
            var rect = PanelRect(name, parent, background, anchorMin, anchorMax, offsetMin, offsetMax);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.72f, 0.85f, 0.84f, 1f);
            colors.disabledColor = new Color(0.4f, 0.46f, 0.47f, 0.65f);
            button.colors = colors;
            label = UiText("Label", rect, labelValue, 16f, FontStyles.Bold, foreground, TextAlignmentOptions.Center);
            SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 3f), new Vector2(-6f, -3f));
            return button;
        }

        private static TMP_InputField InputRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, string placeholder)
        {
            var root = PanelRect(name, parent, new Color(0.07f, 0.13f, 0.15f, 1f), anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            var input = root.gameObject.AddComponent<TMP_InputField>();
            input.contentType = TMP_InputField.ContentType.DecimalNumber;
            input.lineType = TMP_InputField.LineType.SingleLine;

            var viewport = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(root, false);
            SetRect(viewport.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(10f, 2f), new Vector2(-10f, -2f));
            var text = UiText("Text", viewport.transform, string.Empty, 17f, FontStyles.Bold, Ink, TextAlignmentOptions.MidlineLeft);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var hint = UiText("Placeholder", viewport.transform, placeholder, 16f, FontStyles.Italic, Muted, TextAlignmentOptions.MidlineLeft);
            SetRect(hint.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            input.textViewport = viewport.GetComponent<RectTransform>();
            input.textComponent = (TextMeshProUGUI)text;
            input.placeholder = (TextMeshProUGUI)hint;
            input.targetGraphic = root.GetComponent<Image>();
            return input;
        }

        private static TMP_Dropdown DropdownRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var root = PanelRect(name, parent, new Color(0.07f, 0.13f, 0.15f, 1f), anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            var dropdown = root.gameObject.AddComponent<TMP_Dropdown>();
            dropdown.targetGraphic = root.GetComponent<Image>();
            var caption = UiText("Label", root, "SYBR Green", 17f, FontStyles.Bold, Ink, TextAlignmentOptions.MidlineLeft);
            SetRect(caption.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-40f, 0f));
            var arrow = UiText("Arrow", root, "▼", 15f, FontStyles.Bold, Teal, TextAlignmentOptions.Center);
            SetRect(arrow.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(-36f, 0f), new Vector2(-4f, 0f));

            var template = PanelRect("Template", root, new Color(0.04f, 0.1f, 0.12f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, -126f), new Vector2(0f, -2f));
            var scrollRect = template.gameObject.AddComponent<ScrollRect>();
            var viewport = PanelRect("Viewport", template, new Color(0.03f, 0.08f, 0.09f, 1f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport, false);
            SetRect(content.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -120f), Vector2.zero);
            var item = PanelRect("Item", content.transform, new Color(0.07f, 0.14f, 0.16f, 1f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -38f), Vector2.zero);
            var toggle = item.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = item.GetComponent<Image>();
            var checkmark = UiText("Item Checkmark", item, "✓", 16f, FontStyles.Bold, Teal, TextAlignmentOptions.Center);
            SetRect(checkmark.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(4f, 0f), new Vector2(30f, 0f));
            toggle.graphic = checkmark;
            var itemLabel = UiText("Item Label", item, "Option", 16f, FontStyles.Normal, Ink, TextAlignmentOptions.MidlineLeft);
            SetRect(itemLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(34f, 0f), new Vector2(-4f, 0f));
            scrollRect.viewport = viewport;
            scrollRect.content = content.GetComponent<RectTransform>();
            scrollRect.horizontal = false;
            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            dropdown.template = template;
            template.gameObject.SetActive(false);
            return dropdown;
        }

        private static Slider SliderRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            SetRect(root.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            var background = PanelRect("Background", root.transform, new Color(0.08f, 0.16f, 0.18f, 1f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            SetRect(fillArea.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            var fill = PanelRect("Fill", fillArea.transform, Teal, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var slider = root.GetComponent<Slider>();
            slider.fillRect = fill;
            slider.targetGraphic = background.GetComponent<Image>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.interactable = false;
            return slider;
        }

        private static void AddOutline(GameObject target, Color color, float distance)
        {
            var outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static GameObject Cube(string name, Vector3 localPosition, Vector3 scale, Material material, Transform parent = null)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = name;
            if (parent != null) target.transform.SetParent(parent, false);
            target.transform.localPosition = localPosition;
            target.transform.localScale = scale;
            target.GetComponent<Renderer>().sharedMaterial = material;
            return target;
        }

        private static GameObject Cylinder(
            string name,
            Vector3 localPosition,
            Vector3 scale,
            Material material,
            Transform parent = null,
            Quaternion? rotation = null)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            target.name = name;
            if (parent != null) target.transform.SetParent(parent, false);
            target.transform.localPosition = localPosition;
            target.transform.localRotation = rotation ?? Quaternion.identity;
            target.transform.localScale = scale;
            target.GetComponent<Renderer>().sharedMaterial = material;
            return target;
        }

        private static GameObject Sphere(string name, Vector3 localPosition, Vector3 scale, Material material, Transform parent = null)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            target.name = name;
            if (parent != null) target.transform.SetParent(parent, false);
            target.transform.localPosition = localPosition;
            target.transform.localScale = scale;
            target.GetComponent<Renderer>().sharedMaterial = material;
            return target;
        }

        private static TMP_Text WorldLabel(string value, Vector3 localPosition, float scale, Color color, Transform parent)
        {
            var target = new GameObject("Label_" + value.Replace('\n', '_'), typeof(RectTransform), typeof(TextMeshPro));
            target.transform.SetParent(parent, false);
            target.transform.localPosition = localPosition;
            target.transform.localScale = Vector3.one * scale;
            var text = target.GetComponent<TextMeshPro>();
            text.font = font;
            text.text = value;
            text.fontSize = 2.2f;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.rectTransform.sizeDelta = new Vector2(8f, 1.5f);
            return text;
        }

        private static void RemoveCollider(GameObject target)
        {
            var collider = target == null ? null : target.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Team 5 PCR Education";
            PlayerSettings.productName = "Team 5 Realistic qPCR Laboratory";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        }

        private static void CapturePreview(Camera camera)
        {
            Directory.CreateDirectory(DeliverablePath + "/Screenshots");
            var renderTexture = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            camera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;

            RenderCameraToPng(camera, DeliverablePath + "/Screenshots/Team5_qPCR_Station.png");
            var originalPosition = camera.transform.position;
            var originalRotation = camera.transform.rotation;
            var originalFieldOfView = camera.fieldOfView;
            camera.transform.position = new Vector3(0f, 6.7f, -9.8f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.15f, 0.65f) - camera.transform.position);
            camera.fieldOfView = 58f;
            RenderCameraToPng(camera, DeliverablePath + "/Screenshots/Team5_RealisticLab_Overview.png");
            camera.transform.position = originalPosition;
            camera.transform.rotation = originalRotation;
            camera.fieldOfView = originalFieldOfView;

            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(renderTexture);
        }

        private static void RenderCameraToPng(Camera camera, string path)
        {
            camera.Render();
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }

        // LAB_BUILD_METHODS

        private static void MarkDirty(params UnityEngine.Object[] objects)
        {
            foreach (var target in objects)
            {
                if (target != null) EditorUtility.SetDirty(target);
            }
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }

        private sealed class MaterialSet
        {
            public Material Epoxy, Wall, Worktop, Cabinet, Steel, Instrument, Rubber, Glass, Seal, Plate,
                UnusedWell, Teal, Red, Blue, Amber, Biohazard, Waste, LiquidSample, LiquidControl, Pipette;
        }

        private sealed class ShellReferences
        {
            public GameObject Ceiling;
            public GameObject UpperFrontWall;
        }

        private sealed class PlateReferences
        {
            public GameObject Root;
            public PlateController Controller;
            public Transform HomeAnchor;
        }

        private sealed class InterfaceReferences
        {
            public GameObject Root;
            public MentorPanelController Mentor;
            public TMP_Text StageTitle, Instruction, StageCounter, Status, ActionLabel, SecondaryActionLabel,
                Disclaimer, WhyText, ResultSummary, ResultCallout;
            public Button ActionButton, SecondaryActionButton, WhyButton;
            public Slider Progress;
            public GameObject WhyPanel, ProtocolPanel, ResultsPanel;
            public AmplificationCurveGraphic CurveGraphic;
        }

        private sealed class XrInterfaceReferences
        {
            public GameObject Root;
            public AmplificationCurveGraphic CurveGraphic;
            public TMP_Text ResultSummary;
            public TMP_Text ResultCallout;
        }

        private sealed class XrReferences
        {
            public GameObject Origin;
            public GameObject Simulator;
        }
    }
}
