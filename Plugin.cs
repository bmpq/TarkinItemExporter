using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EFT;
using UnityEngine;
using gltfmod;
using gltfmod.UI;
using System;
using System.Collections.Generic;
using System.IO;

[BepInPlugin("com.tarkin.gltfmod", "gltfmod", "1.0.0.0")]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Log;

    internal static ConfigEntry<bool> OverwriteTextureFiles;
    internal static ConfigEntry<bool> AllowLowTextures;
    internal static ConfigEntry<string> OutputDir;

    internal const string TEXTTOOLTIP_LOWTEX = "Export Disabled: Your current graphics setting is set to low texture quality, it will result in poor quality textures in the export, as textures are taken from runtime material. To proceed with exporting, either increase your graphics settings for better texture quality or allow low texture exports in the plugin settings.";
    internal const string TEXTBUTTON_EXPORT = "Export as glTF";

    private void Awake()
    {
        Log = base.Logger;
        AssetStudio.Logger.Default = new AssetStudio.BepinexLogger();

        InitConfiguration();

        new PatchGetUniqueName().Enable();
        new PatchResourcesLoad().Enable();
        new PatchGetExportSettingsForSlot().Enable();
        BundleShaders.Add(AssetBundleLoader.BundleLoader.LoadAssetBundle("unitygltf").LoadAllAssets<Shader>());

        new PatchItemSpecificationsPanel().Enable();
    }

    private void InitConfiguration()
    {
        OverwriteTextureFiles = Config.Bind("Export", "OverwriteTextureFiles", true, "");
        AllowLowTextures = Config.Bind("Export", "AllowLowTextures", false, "Textures are taken from runtime material, so the exported textures depend on the game graphics setting");
        OutputDir = Config.Bind("Export", "OutputDir", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Escape from Tarkov", "ExportedGLTF"), "");
    }
}