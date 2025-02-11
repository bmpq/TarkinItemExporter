using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityGLTF;

namespace gltfmod
{
    internal class ExportTestUnityGLTF : MonoBehaviour
    {
        public Transform[] toExport;
        public string outputPath = "ExportedGLTF";
        public string outputFileName = "exported_model";
        public bool exportAsGLB = false;
        public bool exportTextures = true;

        void Update()
        {
            if (Input.GetKeyUp(KeyCode.P))
            {
                HashSet<Transform> toExport = new HashSet<Transform>();
                foreach (LODGroup obj in FindObjectsOfType<LODGroup>())
                {
                    toExport.Add(obj.transform.GetRoot());
                }

                this.toExport = toExport.ToArray();
                Export();
            }
        }

        void PreprocessObject(GameObject go)
        {
            foreach (var lodGroup in go.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = lodGroup.GetLODs();
                for (int i = 0; i < lods.Length; i++)
                {
                    lods[i].renderers = new Renderer[1] { lods[i].renderers[0] };
                }
                lodGroup.SetLODs(lods);
            }
        }

        public void Export()
        {
            foreach (var item in toExport)
            {
                PreprocessObject(item.gameObject);
            }

            GLTFSettings gLTFSettings = GLTFSettings.GetOrCreateSettings();
            gLTFSettings.ExportDisabledGameObjects = true;
            gLTFSettings.RequireExtensions = true;
            ExportContext context = new ExportContext(gLTFSettings);
            GLTFSceneExporter exporter = new GLTFSceneExporter(toExport, context);
            string fullOutputPath = Path.Combine(Application.persistentDataPath, outputPath);
            if (!Directory.Exists(fullOutputPath))
            {
                Directory.CreateDirectory(fullOutputPath);
            }

            if (exportAsGLB)
            {
                exporter.SaveGLB(fullOutputPath, outputFileName);
            }
            else
            {
                exporter.SaveGLTFandBin(fullOutputPath, outputFileName, exportTextures);
            }

            Debug.Log("GLTF Export completed.  Output to: " + fullOutputPath);
        }
    }
}
