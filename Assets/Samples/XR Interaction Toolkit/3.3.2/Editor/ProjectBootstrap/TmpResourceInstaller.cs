using TMPro;
using UnityEditor;
using UnityEngine;

namespace Team5.qPCR.Editor
{
    public static class TmpResourceInstaller
    {
        public static void Install()
        {
            AssetDatabase.importPackageCompleted += OnCompleted;
            AssetDatabase.importPackageFailed += OnFailed;
            AssetDatabase.importPackageCancelled += OnCancelled;
            Debug.Log("[Team 5 TMP] Importing TextMeshPro Essential Resources…");
            TMP_PackageResourceImporter.ImportResources(true, false, false);
        }

        private static void OnCompleted(string packageName)
        {
            Unsubscribe();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Team 5 TMP] Import completed: {packageName}");
            EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }

        private static void OnFailed(string packageName, string error)
        {
            Unsubscribe();
            Debug.LogError($"[Team 5 TMP] Import failed for {packageName}: {error}");
            EditorApplication.Exit(1);
        }

        private static void OnCancelled(string packageName)
        {
            Unsubscribe();
            Debug.LogError($"[Team 5 TMP] Import cancelled: {packageName}");
            EditorApplication.Exit(2);
        }

        private static void Unsubscribe()
        {
            AssetDatabase.importPackageCompleted -= OnCompleted;
            AssetDatabase.importPackageFailed -= OnFailed;
            AssetDatabase.importPackageCancelled -= OnCancelled;
        }
    }
}
