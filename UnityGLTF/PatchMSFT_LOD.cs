using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;
using UnityGLTF.Plugins;

namespace gltfmod
{
    internal class PatchMSFT_LOD : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(MSFT_lods_Extension), nameof(MSFT_lods_Extension.AfterNodeExport));
        }

        [PatchPrefix]
        private static bool PatchPrefix()
        {
            // disabling because:
            // - doesn't support multiple renderers per LOD (tarkov got them all over the place)
            // - blender has no native support for glTF.MSFT_lod
            return false;
        }
    }
}
