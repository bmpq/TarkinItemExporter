using Diz.Utils;
using EFT.AssetsManager;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace TarkinItemExporter
{
    public class MeshReimporter
    {
        public bool Working { get; private set; } = false;
        public bool Success { get; private set; } = false;
        public string ErrorMessage = "Not run yet";

        static Dictionary<int, Mesh> cacheConvertedMesh = new Dictionary<int, Mesh>();

        FieldInfo fieldInfo = typeof(AssetPoolObject).GetField("ResourceType", BindingFlags.NonPublic | BindingFlags.Instance);

        // since usual unity mesh is unreadable, we use the 3rd-party tool AssetStudio to load the item bundle again, bypassing the limitation
        public void ReimportMeshAssetsAndReplace(HashSet<GameObject> uniqueRootNodes)
        {
            Working = true;
            Success = false;
            ErrorMessage = null;

            if (uniqueRootNodes == null || uniqueRootNodes.Count == 0)
            {
                Working = false;
                Success = false;
                ErrorMessage = "No root nodes provided.";
                return;
            }

            List<AssetPoolObject> assetPoolObjects = uniqueRootNodes.SelectMany(rootNode => rootNode.GetComponentsInChildren<AssetPoolObject>()).ToList();
            HashSet<string> pathsToLoad = new HashSet<string>();

            try
            {
                foreach (var assetPoolObject in assetPoolObjects)
                {
                    MeshFilter[] meshFilters = assetPoolObject.GetComponentsInChildren<MeshFilter>();
                    if (meshFilters.All(meshFilter => meshFilter.sharedMesh == null || meshFilter.sharedMesh.isReadable))
                        continue;

                    ResourceTypeStruct resourceValue = (ResourceTypeStruct)fieldInfo.GetValue(assetPoolObject);
                    if (resourceValue.ItemTemplate == null || resourceValue.ItemTemplate.Prefab == null)
                        continue;
                    string resourcePath = resourceValue.ItemTemplate.Prefab.path; // starts with assets/...
                    string vanillaFullpath = Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, "Windows", resourcePath));

                    if (File.Exists(vanillaFullpath))
                    {
                        pathsToLoad.Add(vanillaFullpath);
                    }

                    DirectoryInfo gameRootDir = new DirectoryInfo(Application.streamingAssetsPath).Parent.Parent;
                    string serverModsDirPath = Path.Combine(gameRootDir.FullName, "user", "mods");
                    foreach (string modDir in Directory.GetDirectories(serverModsDirPath))
                    {
                        string potentialModPath = Path.Combine(modDir, "bundles", resourcePath);

                        if (File.Exists(potentialModPath))
                        {
                            pathsToLoad.Add(potentialModPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error preparing asset paths: {ex.Message}";
                Working = false;
                Success = false;
                Plugin.Log.LogError(ex);
                return;
            }


            Task.Run(() =>
            {
                if (Studio.LoadAssets(pathsToLoad, out List<AssetItem> assets))
                {
                    AsyncWorker.RunInMainTread(() => ReplaceMesh(uniqueRootNodes, assets));
                }
                else
                {
                    AsyncWorker.RunInMainTread(() => {
                        ErrorMessage = "Failed to load assets with AssetStudio.";
                        Working = false;
                        Success = false;
                    });
                }
            }).ContinueWith(task => {
                if (task.IsFaulted)
                {
                    AsyncWorker.RunInMainTread(() => {
                        ErrorMessage = $"Error in AssetStudio task: {task.Exception.InnerException?.Message ?? task.Exception.Message}";
                        Working = false;
                        Success = false;
                        Plugin.Log.LogError(task.Exception);
                    });
                }
            });
        }
        
        private void ReplaceMesh(HashSet<GameObject> uniqueRootNodes, List<AssetItem> assets)
        {
            try
            {
                foreach (var meshFilter in uniqueRootNodes.SelectMany(rootNode => rootNode.GetComponentsInChildren<MeshFilter>()))
                {
                    if (meshFilter.sharedMesh == null)
                        continue;

                    if (meshFilter.sharedMesh.isReadable)
                        continue;

                    int origMeshHash = meshFilter.sharedMesh.GetHashCode();
                    if (cacheConvertedMesh.ContainsKey(origMeshHash))
                    {
                        meshFilter.sharedMesh = cacheConvertedMesh[origMeshHash];
                        Plugin.Log.LogInfo($"{meshFilter.name}: found mesh already converted in cache");
                        continue;
                    }

                    Plugin.Log.LogInfo($"{meshFilter.name}: mesh unreadable, requires reimport. Attempting...");

                    // matching by vertex count is more reliable than just by name
                    // matching names still have higher priority, so the likelihood of selecting the wrong mesh is lessened
                    AssetItem assetItem = assets
                        .Where(asset => asset.Asset is AssetStudio.Mesh mesh &&
                                        mesh.m_VertexCount == meshFilter.sharedMesh.vertexCount)
                        .OrderByDescending(asset => asset.Text == meshFilter.sharedMesh.name)
                        .FirstOrDefault();
                    if (assetItem == null)
                    {
                        Plugin.Log.LogError($"{meshFilter.name}: couldn't find replacement mesh!");
                        continue;
                    }

                    AssetStudio.Mesh asMesh = assetItem.Asset as AssetStudio.Mesh;

                    meshFilter.sharedMesh = asMesh.ConvertToUnityMesh();

                    cacheConvertedMesh[origMeshHash] = meshFilter.sharedMesh;

                    Plugin.Log.LogInfo($"{meshFilter.name}: success reimporting and replacing mesh");
                }

                Success = true;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error replacing meshes: {ex.Message}";
                Success = false;
                Plugin.Log.LogError(ex);
            }
            finally
            {
                Working = false;
            }
        }
    }
}
