using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EFT;
using UnityEngine;
using gltfmod;
using System;
using System.Collections.Generic;

[BepInPlugin("com.tarkin.gltfmod", "gltfmod", "1.0.0.0")]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Log;

    internal static ConfigEntry<bool> OverwriteTextureFiles;

    private void Awake()
    {
        Log = base.Logger;
        AssetStudio.Logger.Default = new AssetStudio.BepinexLogger();

        InitConfiguration();

        new PatchGetUniqueName().Enable();
        new PatchResourcesLoad().Enable();
        new PatchGetExportSettingsForSlot().Enable();
        BundleShaders.Add(AssetBundleLoader.BundleLoader.LoadAssetBundle("unitygltf").LoadAllAssets<Shader>());

        GameObject yo = new GameObject("yo");
        yo.AddComponent<ExportTest>();
        DontDestroyOnLoad(yo);
    }

    private void InitConfiguration()
    {
        OverwriteTextureFiles = Config.Bind("Export", "OverwriteTextureFiles", true, "");
    }
}