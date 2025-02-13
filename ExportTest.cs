using System.Threading.Tasks;
using UnityEngine;
using GLTFast;
using GLTFast.Export;
using GLTFast.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
            if (Input.GetKeyUp(KeyCode.P))
            {
                StartCoroutine(TriggerExport());
            }
        }

        IEnumerator TriggerExport()
        {
            List<AssetPoolObject> allItems = FindObjectsOfType<AssetPoolObject>().Where(o => o.gameObject.GetComponent<PreviewPivot>() != null).ToList();
            foreach (AssetPoolObject item in allItems)
            {
                DestroyChildrenExceptLOD0(item.gameObject);
                item.GetComponentsInChildren<LODGroup>().DestroyAll();
            }

            // in unity destroy is delayed until the end of the frame
            yield return new WaitForEndOfFrame();

            HashSet<GameObject> uniqueRootNodes = new HashSet<GameObject>();
            foreach (AssetPoolObject item in allItems)
            {
                if (ReimportMeshAssetAndReplace(item))
                    uniqueRootNodes.Add(item.transform.GetRoot().gameObject);
                else
                    Destroy(item.gameObject);
            }

            yield return new WaitForEndOfFrame();

            toExport = uniqueRootNodes.ToArray();

            Debug.Log($"Assets preprocessed. Attempting to export {this.toExport.Length} root nodes");

            if (Plugin.SelectedExporter.Value == AssetExporter.UnityGLTF)
                Export_UnityGLTF(toExport);
            else if (Plugin.SelectedExporter.Value == AssetExporter.GLTFast)
                Export_GLTFast(toExport);
        }

        // since usual unity mesh is unreadable, we use the 3rd-party tool AssetStudio to load the item bundle again, bypassing the limitation
        bool ReimportMeshAssetAndReplace(AssetPoolObject assetPoolObject)
        {
            try
            {
                MeshFilter componentLod0 = assetPoolObject.GetComponentInChildren<MeshFilter>();
                if (componentLod0.mesh.isReadable)
                    return true;

                FieldInfo fieldInfo = typeof(AssetPoolObject).GetField("ResourceType", BindingFlags.NonPublic | BindingFlags.Instance);
                ResourceTypeStruct resourceValue = (ResourceTypeStruct)fieldInfo.GetValue(assetPoolObject);
                string pathBundle = resourceValue.ItemTemplate.Prefab.path; // starts with assets/...

                List<AssetItem> assets = Studio.LoadAssets(Path.Combine(Application.streamingAssetsPath, "Windows", pathBundle));
                AssetStudio.Mesh asMesh = (AssetStudio.Mesh)assets.First(a => (a.Asset is AssetStudio.Mesh && a.Text == componentLod0.mesh.name)).Asset;

                componentLod0.mesh = asMesh.ConvertToUnityMesh();

                Plugin.Log.LogInfo($"{assetPoolObject.name}: success reimporting and replacing mesh");

                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"{assetPoolObject.name}: {ex}");
                return false;
            }
        }

        public void DestroyChildrenExceptLOD0(GameObject item)
        {
            for (int i = item.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = item.transform.GetChild(i);
                string childName = child.name.ToUpper();

                if (!childName.Contains("LOD0") || childName.Contains("SHADOW_LOD0"))
                {
                    Destroy(child.gameObject);
                }
            }
        }

        async Task Export_GLTFast(GameObject[] rootLevelNodes)
        {
            var logger = new CollectingLogger();

            var exportSettings = new ExportSettings
            {
                Format = glb ? GltfFormat.Binary : GltfFormat.Json,
                FileConflictResolution = FileConflictResolution.Overwrite,
                PreservedVertexAttributes = VertexAttributeUsage.AllTexCoords | VertexAttributeUsage.Color,

                Deterministic = true
            };

            var gameObjectExportSettings = new GameObjectExportSettings
            {
                OnlyActiveInHierarchy = false,
                DisabledComponents = false
            };

            var export = new GameObjectExport(exportSettings, gameObjectExportSettings, logger: logger);
            export.AddScene(rootLevelNodes, "gltfscene");

            try
            {
                var success = await export.SaveToFileAndDispose(PathExportFull + ".gltf");

                if (success)
                    Debug.Log($"Successfully exported to: {PathExportFull}");
                else
                    Debug.LogError("Something went wrong with GLTFast exporting");

                logger.LogAll();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                logger.LogAll();
            }
        }

        void Export_UnityGLTF(GameObject[] rootLevelNodes)
        {
            Transform[] toExport = rootLevelNodes.Select(go => go != null ? go.transform : null).ToArray();

            GLTFSettings gLTFSettings = GLTFSettings.GetOrCreateSettings();
            gLTFSettings.ExportDisabledGameObjects = true;
            gLTFSettings.RequireExtensions = true;
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

                Debug.Log("Successful export with UnityGLTF. Output to: " + PathExportFull);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}
