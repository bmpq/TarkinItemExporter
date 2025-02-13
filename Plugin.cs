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

    private void Awake()
    {
        Log = base.Logger;

        InitConfiguration();

        new PatchGLTFastExporterShader().Enable();
        //new PatchShaderFind().Enable();
        //new PatchResourcesLoad().Enable();

        AssetBundle bundle = AssetBundleLoader.BundleLoader.LoadAssetBundle("gltfast");
        //AssetBundle bundle = AssetBundleLoader.BundleLoader.LoadAssetBundle("unitygltf");
        BundleShaders.Add(bundle.LoadAllAssets<Shader>());

        GameObject yo = new GameObject("yo");
        //yo.AddComponent<ExportTestUnityGLTF>();
        yo.AddComponent<ExportTestGLTFast>();
        DontDestroyOnLoad(yo);
    }

    private void InitConfiguration()
    {
    }
}