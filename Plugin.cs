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

    internal static ConfigEntry<AssetExporter> SelectedExporter;

    private void Awake()
    {
        Log = base.Logger;
        AssetStudio.Logger.Default = new AssetStudio.BepinexLogger();

        InitConfiguration();

        if (SelectedExporter.Value == AssetExporter.UnityGLTF)
        {
            new PatchShaderFind().Enable();
            new PatchResourcesLoad().Enable();
            new PatchGetExportSettingsForSlot().Enable();
            BundleShaders.Add(AssetBundleLoader.BundleLoader.LoadAssetBundle("unitygltf").LoadAllAssets<Shader>());
        }
        else if (SelectedExporter.Value == AssetExporter.GLTFast)
        {
            new PatchGLTFastExporterShader().Enable();
            BundleShaders.Add(AssetBundleLoader.BundleLoader.LoadAssetBundle("gltfast").LoadAllAssets<Shader>());
        }

        GameObject yo = new GameObject("yo");
        yo.AddComponent<ExportTest>();
        DontDestroyOnLoad(yo);
    }

    private void InitConfiguration()
    {
        SelectedExporter = Config.Bind("General", "SelectedExporter", AssetExporter.UnityGLTF, "Which file export library to use (changes require game restart)");
    }
}