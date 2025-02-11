using GLTFast;
using UnityEngine;
using GLTFast.Export;
using GLTFast.Logging;
using System.Linq;
using System.Collections.Generic;

namespace gltfmod
{
    public class ExportTestGLTFFast : MonoBehaviour
    {
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.O))
            {
                AdvancedExport();
            }
        }

        async void AdvancedExport()
        {
            string path = Application.persistentDataPath + "/_gltftest/yo.gltf";

            // CollectingLogger lets you programmatically go through
            // errors and warnings the export raised
            var logger = new CollectingLogger();

            // ExportSettings and GameObjectExportSettings allow you to configure the export
            // Check their respective source for details

            // ExportSettings provides generic export settings
            var exportSettings = new ExportSettings
            {
                FileConflictResolution = FileConflictResolution.Overwrite,
                Format = GltfFormat.Json,
                Deterministic = true
            };

            // GameObjectExportSettings provides settings specific to a GameObject/Component based hierarchy
            var gameObjectExportSettings = new GameObjectExportSettings
            {
                // Include inactive GameObjects in export
                OnlyActiveInHierarchy = false,
                // Also export disabled components
                DisabledComponents = true,
            };

            // GameObjectExport lets you create glTFs from GameObject hierarchies
            var export = new GameObjectExport(exportSettings, gameObjectExportSettings, logger: logger);

            HashSet<GameObject> toExport = new HashSet<GameObject>();

            Material tempMat = new Material(Shader.Find("Standard"));

            foreach (var lodGroup in FindObjectsOfType<LODGroup>())
            {
                foreach (var rend in lodGroup.GetComponentsInChildren<Renderer>())
                {
                    Material[] temp = new Material[rend.materials.Length];
                    for (int i = 0; i < temp.Length; i++)
                    {
                        temp[i] = tempMat;
                    }

                    rend.materials = temp;
                    rend.material = tempMat;
                }
                toExport.Add(lodGroup.transform.GetRoot().gameObject);
            }

            // Add a scene
            export.AddScene(toExport.ToArray(), "My new glTF scene");

            // Async glTF export
            var success = await export.SaveToFileAndDispose(path);

            if (!success)
            {
                Debug.LogError("Something went wrong exporting a glTF");
                // Log all exporter messages
                logger.LogAll();
            }
            else
            {
                Debug.LogError("glTF export success");
            }
        }
    }
}