using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Team5.qPCR.Editor
{
    public static class PackageInstaller
    {
        private static readonly string[] PackagesToAdd =
        {
            "com.unity.render-pipelines.universal@17.3.0",
            "com.unity.inputsystem@1.20.0",
            "com.unity.test-framework@1.6.0",
            "com.unity.xr.interaction.toolkit@3.3.2",
            "com.unity.xr.management@4.6.1",
            "com.unity.xr.openxr@1.16.1",
            "com.unity.xr.meta-openxr@2.3.2",
            "com.unity.pipeline"
        };

        private static AddAndRemoveRequest request;
        private static double deadline;

        public static void Install()
        {
            Debug.Log($"[Team5 PackageInstaller] Adding: {string.Join(", ", PackagesToAdd)}");
            request = Client.AddAndRemove(PackagesToAdd, new string[0]);
            deadline = EditorApplication.timeSinceStartup + 900d;
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (request == null)
            {
                return;
            }

            if (!request.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup > deadline)
                {
                    EditorApplication.update -= Poll;
                    Debug.LogError("[Team5 PackageInstaller] Timed out waiting for Package Manager.");
                    EditorApplication.Exit(2);
                }

                return;
            }

            EditorApplication.update -= Poll;
            if (request.Status == StatusCode.Success)
            {
                var resolved = request.Result.Select(package => $"{package.name}@{package.version}");
                Debug.Log($"[Team5 PackageInstaller] Resolved: {string.Join(", ", resolved)}");
                AssetDatabase.SaveAssets();
                EditorApplication.Exit(0);
                return;
            }

            Debug.LogError($"[Team5 PackageInstaller] Failed: {request.Error?.message}");
            EditorApplication.Exit(1);
        }
    }
}
