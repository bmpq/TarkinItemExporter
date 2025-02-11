using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace gltfmod
{
    internal class PatchShaderFind : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(Shader), nameof(Shader.Find));
        }

        [PatchPostfix]
        private static void PatchPostfix(ref Shader __result, string name)
        {
            Plugin.Log.LogInfo($"Intercepted `Shader.Find()` call for `{name}` which found `{__result}`");

            if (__result != null)
                return;

            __result = BundleShaders.Find(name);
        }
    }
}
