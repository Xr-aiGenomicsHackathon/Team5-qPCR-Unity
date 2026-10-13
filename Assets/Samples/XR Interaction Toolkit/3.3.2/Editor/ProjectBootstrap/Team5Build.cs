using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace Team5.qPCR.Editor
{
    public static class Team5Build
    {
        private const string ScenePath = "Assets/Team5/Scenes/Team5_qPCR_RealisticLab.unity";
        private const string OpenXrSettingsKey = "com.unity.xr.openxr.settings4";
        private const string AndroidOpenXrLoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";
        private const string TemporaryDesktopSettingsPath = "Assets/Editor/Team5BuildSupport/EmptyDesktopOpenXRSettings.asset";
        private const string TemporaryDesktopPackageSettingsPath = "Assets/Editor/Team5BuildSupport/EmptyDesktopOpenXRPackageSettings.asset";
        private const string OpenXrPackageSettingsPath = "Assets/XR/Settings/OpenXR Package Settings.asset";

        public static void ScheduleWindowsBuild()
        {
            Debug.Log("[Team 5 Build] Windows build queued.");
            EditorApplication.delayCall += () =>
            {
                try { BuildWindows(); }
                catch (Exception exception) { Debug.LogException(exception); }
            };
        }

        public static void ScheduleAndroidBuild()
        {
            Debug.Log("[Team 5 Build] Android Quest-ready build queued.");
            EditorApplication.delayCall += () =>
            {
                try { BuildAndroid(); }
                catch (Exception exception) { Debug.LogException(exception); }
            };
        }

        public static void BuildWindows()
        {
            var output = GetCommandLineValue("-buildOutput") ??
                         Path.Combine(GetDeliverableRoot(), "Builds", "Windows", "Team5-qPCR.exe");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            PrepareDesktopOpenXrSettings();
            var packageSettings = AssetDatabase.LoadAllAssetsAtPath(OpenXrPackageSettingsPath)
                .FirstOrDefault(asset => asset != null && asset.name == "OpenXR Package Settings");
            var desktopSettings = CreateTemporaryDesktopOpenXrSettings();
            OverrideOpenXrSettingsLocator(packageSettings, desktopSettings);
            var desktopPackageSettings = CreateTemporaryDesktopOpenXrPackageSettings(
                packageSettings,
                desktopSettings);
            EditorBuildSettings.AddConfigObject(OpenXrSettingsKey, desktopPackageSettings, true);
            PruneDesktopIncompatibleFeatures(desktopSettings);
            try
            {
                BuildPlayer(output, BuildTarget.StandaloneWindows64, BuildOptions.None);
            }
            finally
            {
                var restoredPackageSettings = AssetDatabase.LoadAllAssetsAtPath(OpenXrPackageSettingsPath)
                    .FirstOrDefault(asset => asset != null && asset.name == "OpenXR Package Settings");
                if (restoredPackageSettings == null)
                {
                    throw new BuildFailedException("Could not restore the project OpenXR package settings.");
                }

                RestoreOpenXrSettingsLocator(restoredPackageSettings);
                EditorBuildSettings.AddConfigObject(OpenXrSettingsKey, restoredPackageSettings, true);
                AssetDatabase.DeleteAsset(TemporaryDesktopPackageSettingsPath);
                AssetDatabase.DeleteAsset(TemporaryDesktopSettingsPath);
            }
        }

        public static void BuildAndroid()
        {
            var output = GetCommandLineValue("-buildOutput") ??
                         Path.Combine(GetDeliverableRoot(), "Builds", "Android", "Team5-qPCR.apk");
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                throw new BuildFailedException("Could not switch the active Unity build target to Android.");
            }
            PrepareAndroidOpenXrSettings();
            EditorUserBuildSettings.buildAppBundle = false;
            BuildPlayer(output, BuildTarget.Android, BuildOptions.None);
        }

        private static void BuildPlayer(string output, BuildTarget target, BuildOptions options)
        {
            if (!File.Exists(Path.GetFullPath(ScenePath)))
            {
                throw new BuildFailedException($"Required scene does not exist: {ScenePath}");
            }

            output = Path.GetFullPath(output);
            CleanBuildOutput(output, target);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = target,
                options = options
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"{target} build failed: {report.summary.result}, {report.summary.totalErrors} errors.");
            }

            Debug.Log($"[Team 5 Build] {target} build completed: {output} ({report.summary.totalSize} bytes)");
        }

        private static void CleanBuildOutput(string output, BuildTarget target)
        {
            var buildRoot = Path.GetFullPath(Path.Combine(GetDeliverableRoot(), "Builds"))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!output.StartsWith(buildRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new BuildFailedException(
                    $"Refusing to clean a build output outside the Team 5 deliverable root: {output}");
            }

            if (target == BuildTarget.StandaloneWindows64)
            {
                var outputDirectory = Path.GetDirectoryName(output);
                if (Directory.Exists(outputDirectory))
                {
                    Directory.Delete(outputDirectory, true);
                }
            }
            else if (File.Exists(output))
            {
                File.Delete(output);
            }
        }

        private static void PrepareDesktopOpenXrSettings()
        {
            // The desktop MVP deliberately does not initialize an XR loader. Keeping the
            // Standalone feature array empty also prevents Android-only Meta feature data
            // from being deserialized by the Windows player. Android settings stay intact.
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            if (settings == null)
            {
                return;
            }

            // Meta OpenXR stores feature sub-assets for every platform in one package settings asset.
            // Unity 6 can otherwise serialize Android-only Meta fields into a non-XR Windows player,
            // producing missing-script warnings at startup. Desktop XR is outside this MVP, so remove
            // only the Standalone feature sub-assets while leaving every Android/Quest feature intact.
            foreach (var candidate in AssetDatabase.LoadAllAssetsAtPath(OpenXrPackageSettingsPath))
            {
                if (candidate is OpenXRFeature &&
                    candidate.name.EndsWith(" Standalone", StringComparison.OrdinalIgnoreCase))
                {
                    UnityEngine.Object.DestroyImmediate(candidate, true);
                }
            }

            var perTargetSettings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(
                "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            if (perTargetSettings != null &&
                perTargetSettings.HasManagerSettingsForBuildTarget(BuildTargetGroup.Standalone))
            {
                var managerSettings = perTargetSettings.ManagerSettingsForBuildTarget(BuildTargetGroup.Standalone);
                if (managerSettings != null)
                {
                    XRPackageMetadataStore.RemoveLoader(managerSettings, AndroidOpenXrLoaderType, BuildTargetGroup.Standalone);
                    managerSettings.automaticLoading = false;
                    managerSettings.automaticRunning = false;
                    EditorUtility.SetDirty(managerSettings);
                }

                var generalSettings = perTargetSettings.SettingsForBuildTarget(BuildTargetGroup.Standalone);
                if (generalSettings != null)
                {
                    generalSettings.InitManagerOnStart = false;
                    EditorUtility.SetDirty(generalSettings);
                }
            }

            var serializedSettings = new SerializedObject(settings);
            var features = serializedSettings.FindProperty("features");
            if (features != null)
            {
                features.arraySize = 0;
                serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private static void PrepareAndroidOpenXrSettings()
        {
            // XR Simulation moves its editor-only resources through this temporary folder
            // during a player build. Remove a copy left by an interrupted build so the
            // package can perform that move and restore cleanly.
            if (AssetDatabase.IsValidFolder("Assets/XR/Temp") &&
                !AssetDatabase.DeleteAsset("Assets/XR/Temp"))
            {
                throw new BuildFailedException("Could not clear the stale XR Simulation build folder.");
            }

            var perTargetSettings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(
                "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            if (perTargetSettings == null)
            {
                throw new BuildFailedException("XR General Settings asset is missing.");
            }

            if (!perTargetSettings.HasSettingsForBuildTarget(BuildTargetGroup.Android))
            {
                perTargetSettings.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
            }

            if (!perTargetSettings.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
            {
                perTargetSettings.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            }

            var generalSettings = perTargetSettings.SettingsForBuildTarget(BuildTargetGroup.Android);
            var managerSettings = perTargetSettings.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            if (generalSettings == null || managerSettings == null ||
                !XRPackageMetadataStore.AssignLoader(
                    managerSettings,
                    AndroidOpenXrLoaderType,
                    BuildTargetGroup.Android))
            {
                throw new BuildFailedException("Could not assign the OpenXR loader for Android.");
            }

            generalSettings.InitManagerOnStart = true;
            managerSettings.automaticLoading = true;
            managerSettings.automaticRunning = true;
            EditorUtility.SetDirty(generalSettings);
            EditorUtility.SetDirty(managerSettings);

            var openXrSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (openXrSettings == null)
            {
                throw new BuildFailedException("Android OpenXR settings are missing.");
            }

            openXrSettings.latencyOptimization = OpenXRSettings.LatencyOptimization.PrioritizeInputPolling;
            EnsureStandaloneValidationFeature(
                "UnityEngine.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature");
            EnsureStandaloneValidationFeature(
                "UnityEngine.XR.OpenXR.Features.Interactions.OculusTouchControllerProfile");
            EnsureStandaloneValidationFeature(
                "UnityEngine.XR.OpenXR.Features.Interactions.MetaQuestTouchPlusControllerProfile");
            EnsureStandaloneValidationFeature(
                "UnityEngine.XR.OpenXR.Features.CompositionLayers.OpenXRCompositionLayersFeature");
            EnsureStandaloneValidationFeature(
                "UnityEngine.XR.Hands.OpenXR.HandTracking");
            EnsureStandaloneValidationFeature(
                "UnityEngine.XR.Hands.OpenXR.MetaHandTrackingAim");
            DisableMobileAmbientOcclusion();

            EnableOpenXrFeature(openXrSettings,
                "UnityEngine.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature");
            EnableOpenXrFeature(openXrSettings,
                "UnityEngine.XR.OpenXR.Features.Interactions.OculusTouchControllerProfile");
            EnableOpenXrFeature(openXrSettings,
                "UnityEngine.XR.OpenXR.Features.Interactions.MetaQuestTouchPlusControllerProfile");
            EnableOpenXrFeature(openXrSettings,
                "UnityEngine.XR.OpenXR.Features.CompositionLayers.OpenXRCompositionLayersFeature");
            EnableOpenXrFeature(openXrSettings,
                "UnityEngine.XR.Hands.OpenXR.HandTracking");
            EnableOpenXrFeature(openXrSettings,
                "UnityEngine.XR.Hands.OpenXR.MetaHandTrackingAim");

            EditorUtility.SetDirty(openXrSettings);
            AssetDatabase.SaveAssets();
        }

        private static OpenXRSettings CreateTemporaryDesktopOpenXrSettings()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TemporaryDesktopSettingsPath));
            AssetDatabase.Refresh();
            AssetDatabase.DeleteAsset(TemporaryDesktopSettingsPath);

            var settings = ScriptableObject.CreateInstance<OpenXRSettings>();
            settings.name = "Team5 Desktop OpenXR Validation Settings";
            AssetDatabase.CreateAsset(settings, TemporaryDesktopSettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static UnityEngine.Object CreateTemporaryDesktopOpenXrPackageSettings(
            UnityEngine.Object sourcePackageSettings,
            OpenXRSettings desktopSettings)
        {
            if (sourcePackageSettings == null)
            {
                throw new BuildFailedException("The OpenXR package settings object is missing.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(TemporaryDesktopPackageSettingsPath));
            AssetDatabase.Refresh();
            AssetDatabase.DeleteAsset(TemporaryDesktopPackageSettingsPath);

            var packageSettings = ScriptableObject.CreateInstance(sourcePackageSettings.GetType());
            packageSettings.name = "Team5 Empty Desktop OpenXR Package Settings";
            AssetDatabase.CreateAsset(packageSettings, TemporaryDesktopPackageSettingsPath);
            OverrideOpenXrSettingsLocator(packageSettings, desktopSettings);
            AssetDatabase.SaveAssets();
            return packageSettings;
        }

        internal static void PruneDesktopIncompatibleFeatures(OpenXRSettings settings)
        {
            // Meta's Android-only feature objects currently have a different player-side
            // serialization layout on Windows. Retain ordinary OpenXR feature records for
            // package validation, but exclude Meta/Quest feature sub-assets from this
            // temporary non-XR desktop settings object.
            AssetDatabase.Refresh();
            var retained = AssetDatabase.LoadAllAssetsAtPath(TemporaryDesktopSettingsPath)
                .OfType<OpenXRFeature>()
                .Where(feature =>
                {
                    var typeName = feature.GetType().FullName ?? string.Empty;
                    var assemblyName = feature.GetType().Assembly.GetName().Name ?? string.Empty;
                    var isMetaFeature = assemblyName.IndexOf("MetaOpenXR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        typeName.IndexOf("MetaQuest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        typeName.IndexOf("OculusQuest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        typeName.IndexOf("SpaceWarp", StringComparison.OrdinalIgnoreCase) >= 0;
                    var serializedFeature = new SerializedObject(feature);
                    var featureId = serializedFeature.FindProperty("featureIdInternal");
                    return !isMetaFeature && featureId != null &&
                           !string.IsNullOrWhiteSpace(featureId.stringValue);
                })
                .ToArray();

            foreach (var feature in AssetDatabase.LoadAllAssetsAtPath(TemporaryDesktopSettingsPath)
                         .OfType<OpenXRFeature>()
                         .Except(retained)
                         .ToArray())
            {
                UnityEngine.Object.DestroyImmediate(feature, true);
            }

            var serializedSettings = new SerializedObject(settings);
            var features = serializedSettings.FindProperty("features");
            features.arraySize = retained.Length;
            for (var index = 0; index < retained.Length; index++)
            {
                features.GetArrayElementAtIndex(index).objectReferenceValue = retained[index];
            }

            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(TemporaryDesktopSettingsPath, ImportAssetOptions.ForceUpdate);
        }

        private static void EnsureStandaloneValidationFeature(string fullTypeName)
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            if (settings == null)
            {
                return;
            }

            var existing = settings.GetFeatures().FirstOrDefault(feature =>
                feature != null && feature.GetType().FullName == fullTypeName);
            if (existing != null)
            {
                existing.enabled = true;
                EnsureValidationFeatureId(existing, fullTypeName);
                EditorUtility.SetDirty(existing);
                return;
            }

            var featureType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullTypeName, false))
                .FirstOrDefault(type => type != null);
            if (featureType == null)
            {
                return;
            }

            var feature = ScriptableObject.CreateInstance(featureType) as OpenXRFeature;
            if (feature == null)
            {
                return;
            }

            feature.name = featureType.Name + " Standalone Validation";
            feature.enabled = true;
            EnsureValidationFeatureId(feature, fullTypeName);
            AssetDatabase.AddObjectToAsset(feature, settings);
            var serializedSettings = new SerializedObject(settings);
            var features = serializedSettings.FindProperty("features");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        private static void EnsureValidationFeatureId(OpenXRFeature feature, string fullTypeName)
        {
            // Unity 6.3's Magic Leap deprecation validator calls ToLower() on every
            // enabled feature id without first checking for null. These Standalone
            // records only exist to satisfy package validation while producing an
            // Android build, so give them a stable non-Magic-Leap identifier.
            var serializedFeature = new SerializedObject(feature);
            var featureId = serializedFeature.FindProperty("featureIdInternal");
            if (featureId != null && string.IsNullOrWhiteSpace(featureId.stringValue))
            {
                featureId.stringValue = "team5.validation." + fullTypeName.ToLowerInvariant();
                serializedFeature.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void DisableMobileAmbientOcclusion()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>("Assets/Settings/Mobile_Renderer.asset");
            if (renderer == null)
            {
                return;
            }

            foreach (var feature in renderer.rendererFeatures)
            {
                if (feature != null && feature.name.IndexOf("AmbientOcclusion", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    feature.SetActive(false);
                    EditorUtility.SetDirty(feature);
                }
            }

            EditorUtility.SetDirty(renderer);
        }

        private static void EnableOpenXrFeature(OpenXRSettings settings, string featureTypeName)
        {
            var feature = settings.GetFeatures()
                .FirstOrDefault(candidate => candidate != null && candidate.GetType().FullName == featureTypeName);
            if (feature == null)
            {
                throw new BuildFailedException($"Required Android OpenXR feature is missing: {featureTypeName}");
            }

            feature.enabled = true;
            EditorUtility.SetDirty(feature);
        }

        private static void OverrideOpenXrSettingsLocator(UnityEngine.Object packageSettings, OpenXRSettings settings)
        {
            if (packageSettings == null)
            {
                return;
            }

            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            const string methodName = "UnityEngine.XR.OpenXR.IPackageSettings2.OverrideSettingsLocatorFunc";
            var method = packageSettings.GetType().GetMethod(methodName, flags);
            if (method == null)
            {
                throw new BuildFailedException("Could not configure OpenXR settings isolation for the desktop build.");
            }

            Func<BuildTargetGroup, OpenXRSettings> desktopSettings = _ => settings;
            method.Invoke(packageSettings, new object[] { desktopSettings });
        }

        private static void RestoreOpenXrSettingsLocator(UnityEngine.Object packageSettings)
        {
            if (packageSettings == null)
            {
                return;
            }

            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            const string methodName = "UnityEngine.XR.OpenXR.IPackageSettings2.RestoreDefaultSettingsLocatorFunc";
            var method = packageSettings.GetType().GetMethod(methodName, flags);
            method?.Invoke(packageSettings, null);
        }

        private static string GetDeliverableRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "deliverables", "Team5-qPCR"));
        }

        private static string GetCommandLineValue(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], key, StringComparison.OrdinalIgnoreCase))
                {
                    return args[index + 1];
                }
            }

            return null;
        }
    }

    public sealed class Team5DesktopOpenXrPreprocessor : IPreprocessBuildWithReport
    {
        // OpenXR's own callback discovers and creates feature records at order 0.
        // Prune Android-only Meta records after all package preprocessors have run,
        // but before Unity serializes the preloaded settings into the Windows player.
        public int callbackOrder => 1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64)
            {
                return;
            }

            var settings = AssetDatabase.LoadAssetAtPath<OpenXRSettings>(
                "Assets/Editor/Team5BuildSupport/EmptyDesktopOpenXRSettings.asset");
            if (settings != null)
            {
                Team5Build.PruneDesktopIncompatibleFeatures(settings);
            }
        }
    }
}
