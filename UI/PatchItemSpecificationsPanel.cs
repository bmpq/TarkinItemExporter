﻿using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.WeaponModding;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Collections.Generic;
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
        private static void PatchPostfix(ItemSpecificationPanel __instance, ItemContextAbstractClass itemContext, InteractionButtonsContainer ____interactionButtonsContainer, WeaponPreview ___weaponPreview_0)
        {
            Sprite sprite = CacheResourcesPopAbstractClass.Pop<Sprite>("UI/Icons/handbook/icon_gear_cases");

            SimpleContextMenuButton _buttonTemplate = (SimpleContextMenuButton)AccessTools.Field(typeof(InteractionButtonsContainer), "_buttonTemplate").GetValue(____interactionButtonsContainer);
            RectTransform _buttonsContainer = (RectTransform)AccessTools.Field(typeof(InteractionButtonsContainer), "_buttonsContainer").GetValue(____interactionButtonsContainer);

            SimpleContextMenuButton newButton = ____interactionButtonsContainer.method_1("EXPORTFILE", "Export as glTF", _buttonTemplate, _buttonsContainer, sprite, () => Export(___weaponPreview_0.WeaponPreviewCamera.transform.GetRoot(), itemContext.Item), null, false, false);

            // make the new button disposable
            ____interactionButtonsContainer.method_5(newButton);
        }

        private static void Export(Transform rootNode, Item item)
        {
            string filename = item.Name.Localized();
            HashSet<GameObject> toExport = [rootNode.gameObject];
            Exporter.Export(toExport, System.IO.Path.Combine(Application.persistentDataPath, "ExportedGLTF"), filename);
        }
    }
}
