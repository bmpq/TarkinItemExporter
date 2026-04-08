using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using TarkinItemExporter.UI;
using System;
using System.IO;
using tarkin;
using System.Collections.Generic;

namespace TarkinItemExporter
{
    [BepInPlugin("com.tarkin.itemexporter", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    internal class Plugin : BaseUnityPlugin
    {
        internal static EFTLogger Log;

        internal static ConfigEntry<string> OutputDir;
        internal static ConfigEntry<bool> OpenExplorerOnFinish;

        internal static ConfigEntry<bool> OverwriteTextureFiles;
        internal static ConfigEntry<bool> AllowLowTextures;

        internal static ConfigEntry<bool> TextureFormatWebp;
        internal static ConfigEntry<int> TextureFormatWebpQuality;

        internal const string TEXTTOOLTIP_LOWTEX = "Export Disabled: Your current graphics setting is set to low texture quality, it will result in poor quality textures in the export, as textures are taken from runtime material. To proceed with exporting, either increase your graphics settings for better texture quality or allow low texture exports in the plugin settings.";
        internal const string TEXTBUTTON_EXPORT = "Export as glTF";

        internal static bool InputBlocked;

        AssetBundleHandler assetBundleHandler;

        private void Start()
        {
            Log = new EFTLogger("ItemExporter", () => true);
            BepInEx.Logging.Logger.Sources.Add(Log);

            AssetStudio.Logger.Default = new AssetStudio.BepinexLoggerAdapter(Log);
            AssetStudio.Progress.Default = new AssetStudio.ProgressLogger();

            InitConfiguration();

            assetBundleHandler = new AssetBundleHandler(Path.Combine(BepInEx.Paths.PluginPath, "tarkin", "ItemExporter"), Log);

            GLTF_EFTMaterialExportPlugin.TextureConverter.InjectBundleShaders(
                assetBundleHandler.LoadBundle("eftmaterial_gltf_converter_shaders").LoadAllAssets<Shader>());

            UnityGLTF.BundleResources.InjectBundleShaders(
                assetBundleHandler.LoadBundle("unitygltf").LoadAllAssets<Shader>());

            new PatchGetUniqueName().Enable();
            new PatchMSFT_LOD().Enable();

            new PatchItemSpecificationsPanel().Enable();
            new PatchUIInput().Enable();
        }

        private void InitConfiguration()
        {
            OutputDir = Config.Bind("Export", "OutputDir", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Escape from Tarkov", "ExportedGLTF"), "");
            OpenExplorerOnFinish = Config.Bind("Export", "OpenExplorerOnFinish", true, "Open output folder on finish export");

            OverwriteTextureFiles = Config.Bind("Textures", "OverwriteTextureFiles", true, "");
            AllowLowTextures = Config.Bind("Textures", "AllowLowTextures", false, "Textures are taken from runtime material, so the exported textures depend on the game graphics setting");

            TextureFormatWebp = Config.Bind("Textures", "TextureFormatWebp", false, "WebP makes for smaller texture file size");
            TextureFormatWebpQuality = Config.Bind("Textures", "WebpQuality", 90, "");
        }

        void OnDestroy()
        {
            assetBundleHandler.Dispose();

            BepInEx.Logging.Logger.Sources.Remove(Log);
        }
    }
}
