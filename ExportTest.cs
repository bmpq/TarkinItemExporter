﻿using System.Threading.Tasks;
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
            if (Input.GetKeyUp(KeyCode.RightBracket))
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
            }

            yield return new WaitForEndOfFrame();

            FailSafeDestroyAllUnreadableMesh(uniqueRootNodes);

            yield return new WaitForEndOfFrame();

            toExport = uniqueRootNodes.ToArray();

            Debug.Log($"Assets preprocessed. Attempting to export {this.toExport.Length} root nodes");

            if (Plugin.SelectedExporter.Value == AssetExporter.UnityGLTF)
                Export_UnityGLTF(toExport);
            else if (Plugin.SelectedExporter.Value == AssetExporter.GLTFast)
                Export_GLTFast(toExport);
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
                        Debug.LogError($"{meshFilter.name} has an unreadable mesh, destroying it.");
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

                FieldInfo fieldInfo = typeof(AssetPoolObject).GetField("ResourceType", BindingFlags.NonPublic | BindingFlags.Instance);
                ResourceTypeStruct resourceValue = (ResourceTypeStruct)fieldInfo.GetValue(assetPoolObject);
                string pathBundle = resourceValue.ItemTemplate.Prefab.path; // starts with assets/...

                List<AssetItem> assets = Studio.LoadAssets(Path.Combine(Application.streamingAssetsPath, "Windows", pathBundle));

                // some meshes are in client_assets.bundle (idk)
                assets.AddRange(Studio.LoadAssets(Path.Combine(Application.streamingAssetsPath, "Windows", Path.Combine(Path.GetDirectoryName(pathBundle), "client_assets.bundle"))));

                foreach (var meshFilter in meshFilters)
                {
                    if (meshFilter.mesh.isReadable)
                        continue;

                    Plugin.Log.LogInfo($"{meshFilter.name}: attempting reimport...");

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

        public void DestroyChildrenExceptLOD0(GameObject item)
        {
            string[] toDestroy = new string[] { "LOD1", "LOD2", "LOD3", "LOD4", "SHADOW_LOD0" };

            foreach (MeshFilter meshFilter in item.GetComponentsInChildren<MeshFilter>())
            {
                string childName = meshFilter.gameObject.name.ToUpper();

                foreach (string s in toDestroy)
                {
                    if (childName.Contains(s))
                    {
                        Plugin.Log.LogInfo($"{meshFilter.gameObject}: deleting...");

                        Destroy(meshFilter.gameObject.GetComponent<HotObject>());
                        Destroy(meshFilter.gameObject.GetComponent<MeshRenderer>());
                        Destroy(meshFilter);
                    }
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
                OnlyActiveInHierarchy = true,
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
