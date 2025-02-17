using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using EFT.AssetsManager;
using System.Reflection;
using System.Collections;
using UnityGLTF;

namespace gltfmod
{
    public static class Exporter
    {
        public static string outputFileName = "exported_model";
        public static bool glb = false;

        public static void ExportCurrentlyOpened(string pathOutputDir)
        {
            CreateDirIfDoesntExist(pathOutputDir);
            Export(GetCurrentlyOpenItems(), pathOutputDir);
        }

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

        public static void Export(List<AssetPoolObject> allItems, string pathDir)
        {
            Plugin.Log.LogInfo($"Exporting {allItems.Count} items.");

            foreach (AssetPoolObject item in allItems)
            {
                // UnityGLTF will attempt to use MSFT_lod and fail, it doesn't support multiple renderers per LOD, so we don't bother
                HandleLODs(item.gameObject);
            }

            HashSet<GameObject> uniqueRootNodes = new HashSet<GameObject>();
            foreach (AssetPoolObject item in allItems)
            {
                if (ReimportMeshAssetAndReplace(item))
                    uniqueRootNodes.Add(item.transform.GetRoot().gameObject);
            }

            DisableAllUnreadableMesh(uniqueRootNodes);

            PreprocessMaterials(uniqueRootNodes);

            GameObject[] toExport = uniqueRootNodes.ToArray();

            Plugin.Log.LogInfo($"Assets preprocessed. Attempting to export {toExport.Length} root nodes");

            Export_UnityGLTF(toExport, pathDir);
        }

        static void DisableAllUnreadableMesh(HashSet<GameObject> uniqueRootNodes)
        {
            foreach (GameObject rootNode in uniqueRootNodes)
            {
                MeshFilter[] meshFilters = rootNode.GetComponentsInChildren<MeshFilter>();
                foreach (var meshFilter in meshFilters)
                {
                    if (!meshFilter.mesh.isReadable || meshFilter.mesh.vertexCount == 0)
                    {
                        Debug.LogWarning($"{meshFilter.name} has an unreadable mesh, disabling it.");
                        meshFilter.gameObject.SetActive(false);
                    }
                }
            }
        }

        // since usual unity mesh is unreadable, we use the 3rd-party tool AssetStudio to load the item bundle again, bypassing the limitation
        static bool ReimportMeshAssetAndReplace(AssetPoolObject assetPoolObject)
        {
            try
            {
                MeshFilter[] meshFilters = assetPoolObject.GetComponentsInChildren<MeshFilter>();
                if (meshFilters.All(meshFilter => meshFilter.mesh.isReadable))
                    return true;

                Plugin.Log.LogInfo($"{assetPoolObject.name}: contains unreadable meshes. Loading its bundle file...");

                FieldInfo fieldInfo = typeof(AssetPoolObject).GetField("ResourceType", BindingFlags.NonPublic | BindingFlags.Instance);
                ResourceTypeStruct resourceValue = (ResourceTypeStruct)fieldInfo.GetValue(assetPoolObject);
                string pathBundle = resourceValue.ItemTemplate.Prefab.path; // starts with assets/...

                List<AssetItem> assets = Studio.LoadAssets(Path.Combine(Application.streamingAssetsPath, "Windows", pathBundle));
                
                foreach (var meshFilter in meshFilters)
                {
                    if (meshFilter.mesh.isReadable)
                        continue;

                    Plugin.Log.LogInfo($"{meshFilter.name}: mesh unreadable, requires reimport. Attempting...");

                    // matching by vertex count is more reliable than just by name
                    // matching names still have higher priority, so the likelihood of selecting the wrong mesh is lessened
                    AssetItem assetItem = assets
                        .Where(asset => asset.Asset is AssetStudio.Mesh mesh &&
                                        mesh.m_VertexCount == meshFilter.mesh.vertexCount)
                        .OrderByDescending(asset => asset.Text == meshFilter.mesh.name)
                        .FirstOrDefault();
                    if (assetItem == null)
                    {
                        Plugin.Log.LogError($"{meshFilter.name}: couldn't find replacement mesh!");
                        continue;
                    }

                    AssetStudio.Mesh asMesh = assetItem.Asset as AssetStudio.Mesh;
                    if (asMesh == null)
                        continue;

                    meshFilter.mesh = asMesh.ConvertToUnityMesh();

                    Plugin.Log.LogInfo($"{meshFilter.name}: success reimporting and replacing mesh");
                }

                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"{assetPoolObject.name}: {ex}");
                return false;
            }
        }

        static public void HandleLODs(GameObject item)
        {
            Plugin.Log.LogInfo($"Handling LODs in {item.name}...");

            foreach (LODGroup lodGroup in item.GetComponentsInChildren<LODGroup>())
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
            foreach (MeshRenderer meshRend in item.GetComponentsInChildren<MeshRenderer>())
            {
                if (meshRend.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly)
                {
                    meshRend.enabled = false;
                }
            }

            item.GetComponentsInChildren<LODGroup>().ToList().ForEach(l => l.enabled = false);
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

        private static void Export_UnityGLTF(GameObject[] rootLevelNodes, string pathDir)
        {
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
                    exporter.SaveGLB(pathDir, outputFileName);
                }
                else
                {
                    exporter.SaveGLTFandBin(pathDir, outputFileName, true);
                }

                Plugin.Log.LogInfo("Successful export with UnityGLTF. Output to: " + Path.Combine(pathDir, outputFileName));
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(ex);
            }
        }
    }
}
