using Comfort.Common;
using EFT.InventoryLogic;
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

            SimpleContextMenuButton newButton = ____interactionButtonsContainer.method_1("EXPORTFILE", "Export as glTF", _buttonTemplate, _buttonsContainer, sprite, 
                delegate 
                { 
                    Export(___weaponPreview_0.WeaponPreviewCamera.transform.GetRoot(), itemContext.Item);
                    __instance.Close();
                }, 
                null, false, false);

            int textureQuality = Singleton<SharedGameSettingsClass>.Instance.Graphics.Settings.TextureQuality;
            bool unlocked = textureQuality > 1 || Plugin.AllowLowTextures.Value;
            SetExportButtonInteractable(newButton, unlocked);

            // make the new button disposable
            ____interactionButtonsContainer.method_5(newButton);
        }

        static void SetExportButtonInteractable(SimpleContextMenuButton newButton, bool interactable)
        {
            CanvasGroup newButtonGroup = (CanvasGroup)AccessTools.Field(typeof(SimpleContextMenuButton), "_canvasGroup").GetValue(newButton);
            newButtonGroup.interactable = interactable;
            newButtonGroup.alpha = interactable ? 1f : 0.3f;
            // flag to enable tooltip
            AccessTools.Field(typeof(SimpleContextMenuButton), "bool_1").SetValue(newButton, !interactable);
            // tooltip string
            AccessTools.Field(typeof(SimpleContextMenuButton), "string_0").SetValue(newButton, "Export Disabled: Your current graphics setting is set to low texture quality, it will result in poor quality textures in the export, as textures are taken from runtime material. To proceed with exporting, either increase your graphics settings for better texture quality or allow low texture exports in the plugin settings.");
        }

        private static void Export(Transform rootNode, Item item)
        {
            Transform trToZero = rootNode;
            for (int i = 0; i < 4; i++)
            {
                rootNode.localPosition = Vector3.zero;
                rootNode.localRotation = Quaternion.identity;
                rootNode.localScale = Vector3.zero;
                trToZero = trToZero.GetChild(0);
            }

            string filename = item.Name.Localized();
            HashSet<GameObject> toExport = [trToZero.gameObject];
            Exporter.Export(toExport, Plugin.OutputDir.Value, filename);
        }
    }
}
