using Comfort.Common;
using EFT.AssetsManager;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.WeaponModding;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Collections.Generic;
using System.Reflection;
using tarkin;
using UnityEngine;

namespace TarkinItemExporter.UI
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
            Sprite sprite = AssetBundleLoader.LoadAssetBundle("unitygltf").LoadAsset<Sprite>("saveicon");

            SimpleContextMenuButton _buttonTemplate = (SimpleContextMenuButton)AccessTools.Field(typeof(InteractionButtonsContainer), "_buttonTemplate").GetValue(____interactionButtonsContainer);
            RectTransform _buttonsContainer = (RectTransform)AccessTools.Field(typeof(InteractionButtonsContainer), "_buttonsContainer").GetValue(____interactionButtonsContainer);

            SimpleContextMenuButton newButton = ____interactionButtonsContainer.method_1("EXPORTFILE", Plugin.TEXTBUTTON_EXPORT, _buttonTemplate, _buttonsContainer, sprite, 
                delegate
                {
                    Exporter.CallbackFinished = () => __instance.GetComponent<InfoWindow>().Close();
                    Export(___weaponPreview_0.WeaponPreviewCamera.transform.root, itemContext.Item);
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
            string tooltipBlocked = Plugin.TEXTTOOLTIP_LOWTEX;

            IResult result = interactable 
                ? new SuccessfulResult()
                : new FailedResult(tooltipBlocked);

            newButton.SetButtonInteraction(result);
        }

        private static void Export(Transform rootNode, Item mainItem)
        {
            AssetPoolObject itemObject = rootNode.GetComponentInChildren<AssetPoolObject>();
            if (itemObject == null)
            {
                Plugin.Log.LogError("Trying to export someting that doesn't have AssetPoolObject component!");
                return;
            }

            itemObject.transform.ZeroTransformAndItsParents();

            string filename = Exporter.GenerateHashedName(mainItem);
            Exporter.Export([itemObject.gameObject], Plugin.OutputDir.Value, filename);
        }
    }
}
