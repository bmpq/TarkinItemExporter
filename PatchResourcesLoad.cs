using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using UnityEngine;

namespace gltfmod
{
    internal class PatchResourcesLoad : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(Resources), nameof(Resources.Load), [typeof(string), typeof(Type)]);
        }

        [PatchPostfix]
        private static void Postfix(ref UnityEngine.Object __result, string path, Type systemTypeInstance)
        {
            if (systemTypeInstance == typeof(Shader))
            {
                Shader replacementShader = BundleShaders.Find("Hidden/" + path);

                if (replacementShader != null)
                {
                    __result = replacementShader; // Replace the original result with our bundled shader
                    Plugin.Log.LogInfo($"[gltfmod] Replaced Resources.Load shader '{path}' with bundled shader.");
                }
                else
                {
                    // Optionally log if a shader wasn't replaced (for debugging)
                    Plugin.Log.LogWarning($"[gltfmod] Resources.Load shader '{path}' not replaced (not found in bundle).");
                }
            }
        }
    }
}
