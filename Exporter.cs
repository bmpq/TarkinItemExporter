using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using EFT.AssetsManager;
using System.Reflection;
using System.Collections;
using UnityGLTF;
using TarkinItemExporter.UI;
using UnityGLTF.Plugins;

namespace TarkinItemExporter
{
    public static class Exporter
    {
        public static Action CallbackFinished;

        public static bool glb = false;

        static void CreateDirIfDoesntExist(string pathDir)
        {
            if (!Directory.Exists(pathDir))
                Directory.CreateDirectory(pathDir);
        }

        static void NukeDir(string pathDir)
        {
            var dirInfo = new DirectoryInfo(pathDir);

            foreach (FileInfo file in dirInfo.GetFiles())
            {
                file.IsReadOnly = false;
                file.Delete();
            }
        }

        public static List<AssetPoolObject> GetCurrentlyOpenItems()
        {
            return UnityEngine.Object.FindObjectsOfType<AssetPoolObject>().Where(o => o.gameObject.GetComponent<PreviewPivot>() != null).ToList();
        }

        public static string GenerateHashedName(EFT.InventoryLogic.Item item)
        {
            int persistentHash = GClass906.GetItemHash(item); // same hash used by icons
            string filename = item.Template._name + "_" + persistentHash;
            return filename;
        }

        static Coroutine coroutineExport;
        public static void Export(HashSet<GameObject> uniqueRootNodes, string pathDir, string filename)
        {
            if (coroutineExport != null)
                CoroutineRunner.Instance.StopCoroutine(coroutineExport);

            coroutineExport = CoroutineRunner.Instance.StartCoroutine(ExportCoroutine(uniqueRootNodes, pathDir, filename));
        }

        private static IEnumerator ExportCoroutine(HashSet<GameObject> uniqueRootNodes, string pathDir, string filename)
        {
            float origTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            ProgressScreen.Instance.ShowGameObject(true);

            MeshReimporter meshReimporter = new MeshReimporter();

            meshReimporter.ReimportMeshAssetsAndReplace(uniqueRootNodes);

            while (meshReimporter.Working)
            {
                yield return null;
            }

            if (!meshReimporter.Success)
            {
                ProgressScreen.Instance.HideGameObject();
                Plugin.Log.LogInfo("Export failed: Error loading bundles.");
                NotificationManagerClass.DisplayMessageNotification(
                    $"Export failed. Something went wrong loading bundle files.",
                    EFT.Communications.ENotificationDurationType.Long);

                yield break;
            }

            try // everything else is on main thread, since most of it Unity operations
            {
                HandleLODs(uniqueRootNodes);

                DisableAllUnreadableMesh(uniqueRootNodes);
            }
            catch (Exception ex)
            {
                ProgressScreen.Instance.HideGameObject();
                Plugin.Log.LogInfo($"Export failed: {ex}");
                NotificationManagerClass.DisplayMessageNotification(
                    $"Export failed. Something went wrong while handling scene objects.",
                    EFT.Communications.ENotificationDurationType.Long);
            }

            GameObject[] toExport = uniqueRootNodes.ToArray();
            Plugin.Log.LogInfo("Writing to disk: " + Path.Combine(pathDir, filename));

            yield return null;

            try
            {
                Export_UnityGLTF(toExport, pathDir, filename);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogInfo($"Export failed: {ex}");
                NotificationManagerClass.DisplayMessageNotification(
                    $"Export failed. UnityGLTF failure. Or writing to disk failure.",
                    EFT.Communications.ENotificationDurationType.Long);
            }

            ProgressScreen.Instance.HideGameObject();

            Time.timeScale = origTimeScale;
        }

        public static void DisableAllUnreadableMesh(HashSet<GameObject> uniqueRootNodes)
        {
            foreach (GameObject rootNode in uniqueRootNodes)
            {
                MeshFilter[] meshFilters = rootNode.GetComponentsInChildren<MeshFilter>();
                foreach (var meshFilter in meshFilters)
                {
                    if (meshFilter.sharedMesh == null)
                        continue;

                    if (!meshFilter.sharedMesh.isReadable || meshFilter.sharedMesh.vertexCount == 0)
                    {
                        Debug.LogWarning($"{meshFilter.name} has an unreadable mesh, disabling it.");
                        meshFilter.gameObject.SetActive(false);
                    }
                }
            }
        }


        // enabling highest LODs to export
        public static void HandleLODs(HashSet<GameObject> uniqueRootNodes)
        {
            Plugin.Log.LogInfo($"Handling LODs...");

            foreach (GameObject rootNode in uniqueRootNodes)
            {
                LODGroup[] lodGroups = rootNode.GetComponentsInChildren<LODGroup>();
                lodGroups.ToList().ForEach(l => l.enabled = false);

                foreach (LODGroup lodGroup in lodGroups)
                {
                    LOD[] lods = lodGroup.GetLODs();
                    for (int i = 0; i < lods.Length; i++)
                    {
                        foreach (var rend in lods[i].renderers)
                        {
                            if (rend == null)
                                continue;
                            rend.enabled = i == 0;
                        }
                    }
                }

                // don't need the shadow meshes either, they are lodded too
                foreach (MeshRenderer meshRend in rootNode.GetComponentsInChildren<MeshRenderer>())
                {
                    if (meshRend.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly)
                    {
                        meshRend.enabled = false;
                    }
                }
            }
        }

        private static void Export_UnityGLTF(GameObject[] rootLevelNodes, string pathDir, string filename)
        {
            if (!Directory.Exists(pathDir))
                Directory.CreateDirectory(pathDir);

            Transform[] toExport = rootLevelNodes.Select(go => go != null ? go.transform : null).ToArray();

            GLTFSettings gLTFSettings = GLTFSettings.GetOrCreateSettings();
            gLTFSettings.ExportDisabledGameObjects = false;
            gLTFSettings.RequireExtensions = true;
            gLTFSettings.UseTextureFileTypeHeuristic = false;
            gLTFSettings.ExportPlugins = new List<GLTFExportPlugin>
            {
                ScriptableObject.CreateInstance(typeof(TarkovMaterialExport)) as TarkovMaterialExport,
                ScriptableObject.CreateInstance(typeof(SanitizeTextureNames)) as SanitizeTextureNames
            };

            ExportContext context = new ExportContext(gLTFSettings);
            GLTFSceneExporter exporter = new GLTFSceneExporter(toExport, context);

            try
            {
                if (glb)
                {
                    exporter.SaveGLB(pathDir, filename);
                }
                else
                {
                    exporter.SaveGLTFandBin(pathDir, filename, true);
                }

                Plugin.Log.LogInfo("Successful export with UnityGLTF. Output to: " + Path.Combine(pathDir, filename));
                NotificationManagerClass.DisplayMessageNotification(
                    $"Successful export to {Path.Combine(pathDir, filename)}", 
                    EFT.Communications.ENotificationDurationType.Long);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(ex);
            }

            CallbackFinished?.Invoke();
            CallbackFinished = null;
        }
    }
}
