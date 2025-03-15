using EFT.InputSystem;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace TarkinItemExporter.UI
{
    internal class PatchUIInput : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(InputNode), nameof(InputNode.TranslateInput));
        }

        [PatchPrefix]
        private static bool PatchPrefix(List<ECommand> commands, ref float[] axes, ref ECursorResult shouldLockCursor)
        {
            if (Plugin.InputBlocked)
                return false;

            return true;
        }
    }
}
