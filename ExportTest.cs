﻿using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using EFT.AssetsManager;
using System.Reflection;
using System.Collections;
using UnityGLTF;
using gltfmod.UI;

namespace gltfmod
{
    public static class Exporter
    {
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
            int persistentHash = GClass903.GetItemHash(item); // same hash used by icons
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
            ProgressScreen.Instance.ShowGameObject(true);

            MeshReimporter.ReimportMeshAssetsAndReplace(uniqueRootNodes);

            while (!MeshReimporter.Done)
            {
                yield return null;
            }

            HandleLODs(uniqueRootNodes);

            DisableAllUnreadableMesh(uniqueRootNodes);

            PreprocessMaterials(uniqueRootNodes);

            GameObject[] toExport = uniqueRootNodes.ToArray();

            Plugin.Log.LogInfo($"Assets preprocessed. Attempting to export {toExport.Length} root nodes");

            Export_UnityGLTF(toExport, pathDir, filename);

            ProgressScreen.Instance.HideGameObject();
        }

        static void DisableAllUnreadableMesh(HashSet<GameObject> uniqueRootNodes)
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


        // UnityGLTF will attempt to use MSFT_lod and fail, it doesn't support multiple renderers per LOD, so we don't bother
        static void HandleLODs(HashSet<GameObject> uniqueRootNodes)
        {
            Plugin.Log.LogInfo($"Handling LODs...");

            foreach (GameObject rootNode in uniqueRootNodes)
            {
                foreach (LODGroup lodGroup in rootNode.GetComponentsInChildren<LODGroup>())
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

                rootNode.GetComponentsInChildren<LODGroup>().ToList().ForEach(Component.DestroyImmediate);
            }
        }

        static void PreprocessMaterials(HashSet<GameObject> uniqueRootNodes)
        {
            foreach (GameObject rootNode in uniqueRootNodes)
            {
                foreach (var rend in rootNode.GetComponentsInChildren<Renderer>())
                {
                    Material[] mats = rend.materials;

                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null)
                            continue;

                        mats[i] = mats[i].ConvertToSpecGlos();
                    }

                    rend.materials = mats;
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
        }
    }
}
