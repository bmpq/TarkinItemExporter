using Diz.Utils;
using EFT.AssetsManager;
using JsonType;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TarkinItemExporter
{
    public class MeshReimporter
    {
        public bool Working { get; private set; } = false;
        public bool Success { get; private set; } = false;
        public string ErrorMessage = "Not run yet";

        private static readonly FieldInfo Field_AssetPoolObject_ResourceType = typeof(AssetPoolObject).GetField("ResourceType", BindingFlags.NonPublic | BindingFlags.Instance);

        // since usual unity mesh is unreadable, we use the 3rd-party tool AssetStudio to load the item bundle again, bypassing the limitation
        public void ReimportMeshAssetsAndReplace(HashSet<GameObject> uniqueRootNodes)
        {
            if (Working)
            {
                Plugin.Log.LogWarning("Mesh reimport is already in progress. Ignoring new request.");
                return;
            }

            Working = true;
            Success = false;
            ErrorMessage = null;

            if (uniqueRootNodes == null || uniqueRootNodes.Count == 0)
            {
                Working = false;
                ErrorMessage = "No root nodes provided.";
                return;
            }

            var assetPoolObjects = uniqueRootNodes.SelectMany(node => node.GetComponentsInChildren<AssetPoolObject>()).ToList();
            HashSet<string> initialBundleRequests = new HashSet<string>();
            bool alreadyReadable = false;

            StringBuilder traceLog = new StringBuilder();

            try
            {
                foreach (var assetPoolObject in assetPoolObjects)
                {
                    MeshFilter[] meshFilters = assetPoolObject.GetComponentsInChildren<MeshFilter>();

                    if (meshFilters.All(meshFilter => meshFilter.sharedMesh == null))
                        continue;

                    if (meshFilters.All(meshFilter => meshFilter.sharedMesh != null && meshFilter.sharedMesh.isReadable))
                    {
                        alreadyReadable = true;
                        continue;
                    }

                    ResourceTypeInfo resourceValue = (ResourceTypeInfo)Field_AssetPoolObject_ResourceType.GetValue(assetPoolObject);
                    if (resourceValue.ItemTemplate?.Prefab == null)
                        continue;

                    string resourcePath = resourceValue.ItemTemplate.Prefab.path;
                    if (!string.IsNullOrEmpty(resourcePath))
                    {
                        initialBundleRequests.Add(resourcePath);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error preparing asset paths: {ex.Message}";
                Working = false;
                Plugin.Log.LogError($"{ErrorMessage}\n{ex}");
                return;
            }

            HashSet<string> bundlesToLoad = BundleResolver.ResolveAllDependencies(initialBundleRequests, traceLog);

            if (bundlesToLoad.Count == 0 && !alreadyReadable)
            {
                Working = false;

                ErrorMessage = $"Error finding bundles for {string.Join(", ", uniqueRootNodes.Select(node => node.name))}";
                Plugin.Log.LogError($"{ErrorMessage}\nSearch Trace:\n{traceLog}");
                return;
            }


            Task.Run(() =>
            {
                if (Studio.LoadAssets(bundlesToLoad, out List<AssetItem> assets))
                {
                    AsyncWorker.RunInMainTread(() => ReplaceMesh(uniqueRootNodes, assets));
                }
                else
                {
                    AsyncWorker.RunInMainTread(() => {
                        ErrorMessage = "Failed to load assets with AssetStudio.";
                        Working = false;
                    });
                }
            }).ContinueWith(task => {
                if (task.IsFaulted)
                {
                    AsyncWorker.RunInMainTread(() => {
                        ErrorMessage = $"Error in AssetStudio task: {task.Exception?.InnerException?.Message ?? task.Exception?.Message}";
                        Working = false;
                        Plugin.Log.LogError(task.Exception);
                    });
                }
            });
        }
        
        private void ReplaceMesh(HashSet<GameObject> uniqueRootNodes, List<AssetItem> assets)
        {
            Plugin.Log.LogInfo($"Starting process of reimporting...");

            try
            {
                int replacedCount = 0;

                foreach (var meshFilter in uniqueRootNodes.SelectMany(rootNode => rootNode.GetComponentsInChildren<MeshFilter>()))
                {
                    if (meshFilter.sharedMesh == null)
                        continue;

                    if (meshFilter.sharedMesh.isReadable)
                        continue;

                    // matching by vertex count is more reliable than just by name
                    // matching names still have higher priority, so the likelihood of selecting the wrong mesh is lessened
                    AssetItem assetItem = assets
                        .Where(asset => asset.Asset is AssetStudio.Mesh mesh &&
                                        mesh.m_VertexCount == meshFilter.sharedMesh.vertexCount)
                        .OrderByDescending(asset => asset.Text == meshFilter.sharedMesh.name)
                        .FirstOrDefault();
                    if (assetItem == null)
                    {
                        Plugin.Log.LogWarning($"{meshFilter.name}: Couldn't find replacement mesh (Vertices: {meshFilter.sharedMesh.vertexCount})!");
                        continue;
                    }

                    AssetStudio.Mesh asMesh = assetItem.Asset as AssetStudio.Mesh;

                    meshFilter.sharedMesh = asMesh.ConvertToUnityMesh();

                    replacedCount++;
                }

                Plugin.Log.LogInfo($"Success reimporting and replacing {replacedCount} meshes");

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
