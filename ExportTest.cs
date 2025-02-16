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
    internal class ExportTest : MonoBehaviour
    {
        public GameObject[] toExport;
        public string outputPathDirName = "ExportedGLTF";
        public string outputFileName = "exported_model";
        public bool glb = false;

        string PathExportDirectory
        { 
            get 
            {
                string path = Path.Combine(Application.persistentDataPath, outputPathDirName);
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                return path;
            } 
        }
        string PathExportFull => Path.Combine(PathExportDirectory, outputFileName);

        void Update()
        {
            if (Input.GetKeyUp(KeyCode.RightBracket))
            {
                NukeOutputDir();
                StartCoroutine(TriggerExport());
            }
        }

        void NukeOutputDir()
        {
            var dirInfo = new DirectoryInfo(PathExportDirectory);

            foreach (FileInfo file in dirInfo.GetFiles())
            {
                file.IsReadOnly = false;
                file.Delete();
            }
        }

        IEnumerator TriggerExport()
        {
            List<AssetPoolObject> allItems = FindObjectsOfType<AssetPoolObject>().Where(o => o.gameObject.GetComponent<PreviewPivot>() != null).ToList();

            Plugin.Log.LogInfo($"Exporting {allItems.Count} items.");

            foreach (AssetPoolObject item in allItems)
            {
                // UnityGLTF will attempt to use MSFT_lod and fail, it doesn't support multiple renderers per LOD, so we don't bother
                HandleLODs(item.gameObject);
            }

            // in unity destroy is delayed until the end of the frame
            yield return new WaitForEndOfFrame();

            HashSet<GameObject> uniqueRootNodes = new HashSet<GameObject>();
            foreach (AssetPoolObject item in allItems)
            {
                if (ReimportMeshAssetAndReplace(item))
                    uniqueRootNodes.Add(item.transform.GetRoot().gameObject);
            }

            yield return new WaitForEndOfFrame();

            FailSafeDestroyAllUnreadableMesh(uniqueRootNodes);

            yield return new WaitForEndOfFrame();

            PreprocessMaterials(uniqueRootNodes);

            yield return new WaitForEndOfFrame();

            toExport = uniqueRootNodes.ToArray();

            Plugin.Log.LogInfo($"Assets preprocessed. Attempting to export {this.toExport.Length} root nodes");

            Export_UnityGLTF(toExport);
        }

        void FailSafeDestroyAllUnreadableMesh(HashSet<GameObject> uniqueRootNodes)
        {
            foreach (GameObject rootNode in uniqueRootNodes)
            {
                MeshFilter[] meshFilters = rootNode.GetComponentsInChildren<MeshFilter>();
                foreach (var meshFilter in meshFilters)
                {
                    if (!meshFilter.mesh.isReadable || meshFilter.mesh.vertexCount == 0)
                    {
                        Debug.LogWarning($"{meshFilter.name} has an unreadable mesh, destroying it.");
                        Destroy(meshFilter);
                    }
                }
            }
        }

        // since usual unity mesh is unreadable, we use the 3rd-party tool AssetStudio to load the item bundle again, bypassing the limitation
        bool ReimportMeshAssetAndReplace(AssetPoolObject assetPoolObject)
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

                // some meshes are in client_assets.bundle (idk)
                string pathExtraBundle = Path.Combine(Application.streamingAssetsPath, "Windows", Path.Combine(Path.GetDirectoryName(pathBundle), "client_assets.bundle"));
                if (File.Exists(pathExtraBundle))
                    assets.AddRange(Studio.LoadAssets(pathExtraBundle, false));

                foreach (var meshFilter in meshFilters)
                {
                    if (meshFilter.mesh.isReadable)
                        continue;

                    Plugin.Log.LogInfo($"{meshFilter.name}: mesh unreadable, requires reimport. Attempting...");

                    AssetItem assetItem = assets.FirstOrDefault(a => (a.Asset is AssetStudio.Mesh && a.Text == meshFilter.mesh.name));
                    if (assetItem == null)
                        continue;

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

        public void HandleLODs(GameObject item)
        {
            Plugin.Log.LogInfo($"Handling LODs in {item.name}...");

            foreach (LODGroup lodGroup in item.GetComponentsInChildren<LODGroup>())
            {
                LOD[] lods = lodGroup.GetLODs();
                for (int i = 0; i < lods.Length; i++)
                {
                    foreach (var rend in lods[i].renderers)
                    {
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

            item.GetComponentsInChildren<LODGroup>().DestroyAll();
        }

        void PreprocessMaterials(HashSet<GameObject> uniqueRootNodes)
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

        void Export_UnityGLTF(GameObject[] rootLevelNodes)
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
                    exporter.SaveGLB(PathExportDirectory, outputFileName);
                }
                else
                {
                    exporter.SaveGLTFandBin(PathExportDirectory, outputFileName, true);
                }

                Plugin.Log.LogInfo("Successful export with UnityGLTF. Output to: " + PathExportFull);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(ex);
            }
        }
    }
}
