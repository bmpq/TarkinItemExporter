using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace gltfmod.UI
{
    internal class PatchItemSpecificationsPanel : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ItemSpecificationPanel), nameof(ItemSpecificationPanel.Show));
        }

        [PatchPostfix]
        private static void PatchPostfix(ItemSpecificationPanel __instance, ItemContextAbstractClass itemContext, InteractionButtonsContainer ____interactionButtonsContainer)
        {
            Sprite sprite = CacheResourcesPopAbstractClass.Pop<Sprite>("UI/Icons/handbook/icon_gear_cases");

            SimpleContextMenuButton _buttonTemplate = (SimpleContextMenuButton)AccessTools.Field(typeof(InteractionButtonsContainer), "_buttonTemplate").GetValue(____interactionButtonsContainer);
            RectTransform _buttonsContainer = (RectTransform)AccessTools.Field(typeof(InteractionButtonsContainer), "_buttonsContainer").GetValue(____interactionButtonsContainer);

            SimpleContextMenuButton newButton = ____interactionButtonsContainer.method_1("EXPORTFILE", "Export as glTF", _buttonTemplate, _buttonsContainer, sprite, () => Export(itemContext), null, false, false);

            // make the new button disposable
            ____interactionButtonsContainer.method_5(newButton);
        }

        private static void Export(ItemContextAbstractClass itemContext)
        {
            // todo

            Exporter.ExportCurrentlyOpened(System.IO.Path.Combine(Application.persistentDataPath, "ExportedGLTF"));
        }
    }
}
