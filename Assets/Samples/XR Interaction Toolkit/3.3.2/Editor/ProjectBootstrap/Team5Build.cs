using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Team5.qPCR.Editor
{
    public static class Team5Build
    {
        public const string RequiredEditorVersion = "6000.3.26f1";
        public static string[] PresentationScenes => new[]
        {
            "Assets/Team5/Scenes/LobbyScene.unity",
            "Assets/Team5/Scenes/DemoScene.unity"
        };
        public static void ScheduleWindowsBuild() => EditorApplication.delayCall += BuildWindows;
        public static void ScheduleAndroidBuild() => EditorApplication.delayCall += BuildAndroid;
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Windows", "Team5-qPCR.exe");
        public static void BuildAndroid() => Build(BuildTarget.Android, "Android", "Team5-qPCR.apk");
        private static void Build(BuildTarget target, string folder, string file)
        {
            if (Application.unityVersion != RequiredEditorVersion)
                throw new BuildFailedException("Use Unity " + RequiredEditorVersion + " to create this presentation build. Current editor: " + Application.unityVersion);
            foreach (var scene in PresentationScenes)
                if (!File.Exists(Path.GetFullPath(scene))) throw new BuildFailedException("Required presentation scene is missing: " + scene);
            var defaultRoot = Path.Combine(Directory.GetParent(Application.dataPath).Parent.FullName, "deliverables", "Team5-qPCR", "Builds");
            var output = Path.GetFullPath(CommandLineValue("-buildOutput") ?? Path.Combine(defaultRoot, folder, file));
            var allowedRoot = Path.GetFullPath(defaultRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!output.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("Choose a build output inside the project's deliverables/Team5-qPCR/Builds directory.");
            var directory = Path.GetDirectoryName(output);
            if (File.Exists(output) || (target == BuildTarget.StandaloneWindows64 && Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any()))
                throw new BuildFailedException("This output already contains a build. Preserve it and choose a new -buildOutput subfolder.");

            // Build with the project's existing VR configuration. Do not delete
            // OpenXR feature sub-assets or turn this into the old desktop MVP.
            var xrObjects = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/XR" })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<ScriptableObject>().ToArray();
            var snapshots = xrObjects.Select(EditorJsonUtility.ToJson).ToArray();
            var previousTarget = EditorUserBuildSettings.activeBuildTarget;
            var previousAppBundle = EditorUserBuildSettings.buildAppBundle;
            try
            {
                var group = BuildPipeline.GetBuildTargetGroup(target);
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
                    throw new BuildFailedException("Could not switch to the requested build target.");
                if (target == BuildTarget.Android) EditorUserBuildSettings.buildAppBundle = false;
                Directory.CreateDirectory(directory);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = PresentationScenes, locationPathName = output, target = target, options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException(target + " build failed: " + report.summary.totalErrors + " errors.");
                Debug.Log("[Team 5 Build] Presentation build completed: " + output);
            }
            finally
            {
                for (var index = 0; index < xrObjects.Length; index++)
                    if (xrObjects[index] != null) { EditorJsonUtility.FromJsonOverwrite(snapshots[index], xrObjects[index]); EditorUtility.SetDirty(xrObjects[index]); }
                EditorUserBuildSettings.buildAppBundle = previousAppBundle;
                AssetDatabase.SaveAssets();
                if (EditorUserBuildSettings.activeBuildTarget != previousTarget)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(previousTarget), previousTarget);
            }
        }
        private static string CommandLineValue(string key)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
                if (arguments[index] == key) return arguments[index + 1];
            return null;
        }
    }
}
