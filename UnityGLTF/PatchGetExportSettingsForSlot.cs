﻿using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;
using UnityGLTF;
using static UnityGLTF.GLTFSceneExporter;

namespace TarkinItemExporter
{
    internal class PatchGetExportSettingsForSlot : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GLTFSceneExporter), nameof(GLTFSceneExporter.GetExportSettingsForSlot));
        }

        [PatchPostfix]
        private static void PatchPostfix(ref TextureExportSettings __result, string textureSlot)
        {
            /// we do custom conversion at <see cref="TextureConverter.ConvertAlbedoSpecGlosToSpecGloss"/>
            /// this patch skips a UnityGLTF conversion
            if (textureSlot == TextureMapType.SpecGloss)
            {
                __result.linear = true;
                __result.alphaMode = TextureExportSettings.AlphaMode.Always;
                __result.conversion = TextureExportSettings.Conversion.None;
            }
        }
    }
}
