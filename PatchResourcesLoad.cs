using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using UnityEngine;

namespace gltfmod
{
    /// <summary>
    /// UnityGLTF expects `NormalChannel.shader` to be packed with game build in `Resources/`
    /// can't do that with a mod
    /// so we are patching a manual loading from a bundle
    /// </summary>
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
                    __result = replacementShader;
                    Plugin.Log.LogInfo($"Replaced Resources.Load shader '{path}' with bundled shader.");
                }
            }
        }
    }
}
