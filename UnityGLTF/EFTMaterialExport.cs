using UnityEngine;
using UnityGLTF;
using UnityGLTF.Plugins;
using GLTF.Schema;
using System.Collections.Generic;
using System.IO;
using UnityGLTF.Extensions;
using System;

public class EFTShaderExportPlugin : GLTFExportPlugin
{
    public override string DisplayName => "EFT Shader Exporter";
    public override bool EnabledByDefault => true;

    public override GLTFExportPluginContext CreateInstance(ExportContext context)
    {
        return new EFTShaderExportContext();
    }
}

public class EFTShaderExportContext : GLTFExportPluginContext
{
    public override bool BeforeMaterialExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot, Material material, GLTFMaterial materialNode)
    {
        if (material.shader.name.Contains("Bumped Specular"))
        {
            try
            {
                // bsg got fucked up naming?
                float specularness = material.GetFloat("_Glossness");
                float glossiness = material.GetFloat("_Specularness");

                TextureInfo texGlos = exporter.ExportTextureInfoWithTextureTransform(material, material.GetTexture("_SpecMap"), "_SpecMap", new GLTFSceneExporter.TextureExportSettings());

                // this is definitely wrong, i dont know how to combine two texture into one yet. spec is the alpha channel in _MainTex. how to extract that?
                TextureInfo specGlosTex = exporter.ExportTextureInfoWithTextureTransform(material, material.GetTexture("_MainTex"), "_MainTex", new GLTFSceneExporter.TextureExportSettings()
                {
                    conversion = GLTFSceneExporter.TextureExportSettings.Conversion.MetalGlossChannelSwap,
                    linear = true,
                    alphaMode = GLTFSceneExporter.TextureExportSettings.AlphaMode.Always,
                });

                if (materialNode.Extensions == null || !materialNode.Extensions.ContainsKey(KHR_materials_pbrSpecularGlossinessExtensionFactory.EXTENSION_NAME))
                {
                    KHR_materials_pbrSpecularGlossinessExtension pbrSpecGlos = new KHR_materials_pbrSpecularGlossinessExtension(
                        material.GetColor("_Color").ToNumericsColorLinear(),
                        exporter.ExportTextureInfoWithTextureTransform(material, material.GetTexture("_MainTex"), "_MainTex"),
                        new GLTF.Math.Vector3(specularness, specularness, specularness),
                        glossiness,
                        texGlos);

                    materialNode.AddExtension(KHR_materials_pbrSpecularGlossinessExtensionFactory.EXTENSION_NAME, pbrSpecGlos);
                }

                if (material.HasProperty("_BumpMap"))
                {
                    var normalTex = material.GetTexture("_BumpMap");
                    if (normalTex)
                    {
                        materialNode.NormalTexture = new NormalTextureInfo();
                        materialNode.NormalTexture.Index = exporter.ExportTextureInfoWithTextureTransform(material, normalTex, "_BumpMap", exporter.GetExportSettingsForSlot(GLTFSceneExporter.TextureMapType.Normal)).Index;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
        }

        return false;
    }
}