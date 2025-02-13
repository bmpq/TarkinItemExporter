using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using GLTFast.Export;
using UnityEngine;
using System;

namespace gltfmod
{
    internal class PatchGLTFastExporterShader : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            Type imageExportType = Type.GetType("GLTFast.Export.ImageExport, glTFast.Export");

            return AccessTools.Method(imageExportType, "LoadBlitMaterial");
        }

        [PatchPrefix]
        private static bool PatchPrefix(ref Material __result, string shaderName)
        {
            Shader shader = BundleShaders.Find("Hidden/" + shaderName);
            if (shader != null)
            {
                __result = new Material(shader);
                return false;
            }

            return true;
        }
    }
}
