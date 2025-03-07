using HarmonyLib;
using SPT.Reflection.Patching;
using SPT.Reflection.Utils;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityGLTF;

namespace gltfmod
{
    // Allow UnityGLTF to overwrite texture files with the same name
    internal class PatchGetUniqueName : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.FirstMethod(typeof(GLTFSceneExporter), method => method.Name.Contains("GetUniqueName"));
        }

        [PatchPrefix]
        private static bool PatchPrefix(ref string __result, HashSet<string> existingNames, string name)
        {
            if (!Plugin.OverwriteTextureFiles.Value)
                return true;

            __result = name;
            return false;
        }
    }
}
