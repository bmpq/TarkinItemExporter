using System.Threading.Tasks;
using UnityEngine.Serialization;
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

namespace gltfmod
{
    internal class ExportTestGLTFast : MonoBehaviour
    {
        public GameObject[] toExport;
        public string outputPathDirName = "ExportedGLTF_Fast";
        public string outputFileName = "exported_model.gltf";

        string destinationFilePath => Path.Combine(Application.persistentDataPath, outputPathDirName, outputFileName);

        void Update()
        {
            if (Input.GetKeyUp(KeyCode.P))
            {
                HashSet<GameObject> toExport = new HashSet<GameObject>();
                foreach (AssetPoolObject obj in FindObjectsOfType<AssetPoolObject>())
                {
                    DestroyChildrenExceptLOD0(obj.gameObject);
                    obj.GetComponentsInChildren<LODGroup>().DestroyAll();

                    ReplaceMesh(obj);
                    toExport.Add(obj.transform.GetRoot().gameObject);
                }

                this.toExport = toExport.ToArray();

                Debug.Log($"Attempting to export {this.toExport.Length} root nodes");
                AdvancedExport(this.toExport);
            }
        }

        void ReplaceMesh(AssetPoolObject assetPoolObject)
        {
            MeshFilter componentLod0 = assetPoolObject.GetComponentInChildren<MeshFilter>();
            if (componentLod0 == null)
                return;

            FieldInfo fieldInfo = typeof(AssetPoolObject).GetField("ResourceType", BindingFlags.NonPublic | BindingFlags.Instance);
            ResourceTypeStruct resourceValue = (ResourceTypeStruct)fieldInfo.GetValue(assetPoolObject);
            string pathBundle = resourceValue.ItemTemplate.Prefab.path; // starts with assets/...

            List<AssetItem> assets = Studio.LoadAssets(Path.Combine(Application.streamingAssetsPath, "Windows", pathBundle));
            AssetStudio.Mesh asMesh = (AssetStudio.Mesh)assets.First(a => (a.Asset is AssetStudio.Mesh && a.Text == componentLod0.mesh.name)).Asset;

            componentLod0.mesh = asMesh.ConvertToUnityMesh();
        }

        public void DestroyChildrenExceptLOD0(GameObject item)
        {
            for (int i = item.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = item.transform.GetChild(i);
                string childName = child.name.ToUpper();

                if (!(childName.Contains("LOD0") && !childName.Contains("SHADOW_LOD0")))
                {
                    Destroy(child.gameObject);
                }
            }
        }

        async Task AdvancedExport(GameObject[] rootLevelNodes)
        {
            // CollectingLogger lets you programmatically go through
            // errors and warnings the export raised
            var logger = new CollectingLogger();

            // ExportSettings and GameObjectExportSettings allow you to configure the export
            // Check their respective source for details

            // ExportSettings provides generic export settings
            var exportSettings = new ExportSettings
            {
                Format = GltfFormat.Json,
                FileConflictResolution = FileConflictResolution.Overwrite,

                // Boost light intensities
                LightIntensityFactor = 100f,

                // Ensure mesh vertex attributes colors and texture coordinate (channels 1 through 8) are always
                // exported, even if they are not used/referenced.
                PreservedVertexAttributes = VertexAttributeUsage.AllTexCoords | VertexAttributeUsage.Color,

                Deterministic = true
            };

            // GameObjectExportSettings provides settings specific to a GameObject/Component based hierarchy
            var gameObjectExportSettings = new GameObjectExportSettings
            {
                // Include inactive GameObjects in export
                OnlyActiveInHierarchy = false,

                DisabledComponents = false
            };

            // GameObjectExport lets you create glTFs from GameObject hierarchies
            var export = new GameObjectExport(exportSettings, gameObjectExportSettings, logger: logger);

            // Add a scene
            export.AddScene(rootLevelNodes, "My new glTF scene");

            if (!Directory.Exists(Path.Combine(Application.persistentDataPath, outputPathDirName)))
            {
                Directory.CreateDirectory(Path.Combine(Application.persistentDataPath, outputPathDirName));
            }

            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(3));

            try
            {
                var success = await export.SaveToFileAndDispose(destinationFilePath, cancellationTokenSource.Token);

                if (!success)
                {
                    Debug.LogError("Something went wrong exporting a glTF");
                    logger.LogAll();
                }
                else
                {
                    Debug.Log($"Success! {destinationFilePath}");
                    logger.LogAll();
                }
            }
            catch (OperationCanceledException)
            {
                Debug.LogError("glTF export timed out!");
                logger.LogAll(); // You might get some logs before timeout
            }
            catch (Exception ex)
            {
                Debug.LogException(ex); // Log any other exceptions
                logger.LogAll();
            }
            finally
            {
                cancellationTokenSource.Dispose();
            }
        }
    }
}
