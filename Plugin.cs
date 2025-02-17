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