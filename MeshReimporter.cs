using Diz.Utils;
using EFT.AssetsManager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TarkinItemExporter
{
    internal class MeshReimporter
    {
        public static bool Done;
        public static bool Success;

        static Dictionary<int, Mesh> cacheConvertedMesh = new Dictionary<int, Mesh>();

        // since usual unity mesh is unreadable, we use the 3rd-party tool AssetStudio to load the item bundle again, bypassing the limitation
        public static void ReimportMeshAssetsAndReplace(HashSet<GameObject> uniqueRootNodes)
        {
            Done = false;
            Success = false;

            List<AssetPoolObject> assetPoolObjects = uniqueRootNodes.SelectMany(rootNode => rootNode.GetComponentsInChildren<AssetPoolObject>()).ToList();

            FieldInfo fieldInfo = typeof(AssetPoolObject).GetField("ResourceType", BindingFlags.NonPublic | BindingFlags.Instance);
            HashSet<string> pathsToLoad = new HashSet<string>();
            foreach (var assetPoolObject in assetPoolObjects)
            {
                MeshFilter[] meshFilters = assetPoolObject.GetComponentsInChildren<MeshFilter>();
                if (meshFilters.All(meshFilter => meshFilter.sharedMesh == null || meshFilter.sharedMesh.isReadable))
                    continue;

                ResourceTypeStruct resourceValue = (ResourceTypeStruct)fieldInfo.GetValue(assetPoolObject);
                string pathBundle = resourceValue.ItemTemplate.Prefab.path; // starts with assets/...
                string osPath = Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, "Windows", pathBundle));
                pathsToLoad.Add(osPath);
            }

            Task.Run(() =>
            {
                if (Studio.LoadAssets(pathsToLoad, out List<AssetItem> assets))
                {
                    AsyncWorker.RunInMainTread(() => ReplaceMesh(uniqueRootNodes, assets));
                }
                else
                {
                    Done = true;
                }
            });
        }

        private static void ReplaceMesh(HashSet<GameObject> uniqueRootNodes, List<AssetItem> assets)
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
                Plugin.Log.LogError(ex);
            }

            Done = true;
        }
    }
}
