using System.Collections.Generic;
using System.Reflection;
using GLTF.Schema;
using HarmonyLib;
using TarkinItemExporter;
using UnityEngine;
using UnityGLTF.Extensions;
using static UnityGLTF.GLTFSceneExporter;

namespace UnityGLTF.Plugins
{
	public class TarkovMaterialExport : GLTFExportPlugin
	{
		public override string DisplayName => "Convert Tarkov shaders and textures";
		public override string Description => "";
		public override GLTFExportPluginContext CreateInstance(ExportContext context)
		{
			return new TarkovMaterialExportContext();
		}
	}
	
	public class TarkovMaterialExportContext : GLTFExportPluginContext
    {
        MethodInfo exportTextureTransform = AccessTools.Method(typeof(GLTFSceneExporter), "ExportTextureTransform", [typeof(TextureInfo), typeof(Material), typeof(string)]);
        GLTFSceneExporter _exporter;

        public override void AfterSceneExport(GLTFSceneExporter _, GLTFRoot __)
		{
			RenderTexture.active = null;
		}

        public override bool BeforeMaterialExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot, Material material, GLTFMaterial materialNode)
        {
            _exporter = exporter;

            if (material.shader.name.Contains("SMap") && material.shader.name.Contains("Reflective"))
            {
                bool TransparentCutoff = material.shader.name.Contains("Transparent Cutoff");

                GLTF.Math.Color diffuseFactor = KHR_materials_pbrSpecularGlossinessExtension.DIFFUSE_FACTOR_DEFAULT;
                TextureInfo diffuseTexture = KHR_materials_pbrSpecularGlossinessExtension.DIFFUSE_TEXTURE_DEFAULT;
                GLTF.Math.Vector3 specularFactor = KHR_materials_pbrSpecularGlossinessExtension.SPEC_FACTOR_DEFAULT;
                double glossinessFactor = KHR_materials_pbrSpecularGlossinessExtension.GLOSS_FACTOR_DEFAULT;
                TextureInfo specularGlossinessTexture = KHR_materials_pbrSpecularGlossinessExtension.SPECULAR_GLOSSINESS_TEXTURE_DEFAULT;


                diffuseFactor = material.GetColor("_Color").ToNumericsColorGamma();
                float floatDiffuse = material.GetVector("_DefVals").x;
                diffuseFactor.R *= floatDiffuse;
                diffuseFactor.G *= floatDiffuse;
                diffuseFactor.B *= floatDiffuse;


                Texture texAlbedoSpec = material.GetTexture("_MainTex");
                if (TransparentCutoff)
                    texAlbedoSpec = material.GetTexture("_SpecMap"); // asinine thing, idk why this is
                Texture texGlos = material.GetTexture("_SpecMap");
                if (TransparentCutoff)
                    texGlos = material.GetTexture("_MainTex"); // asinine thing, idk why this is
                if (texGlos == null)
                    texGlos = Texture2D.whiteTexture;
                if (texAlbedoSpec == null)
                    texAlbedoSpec = Texture2D.whiteTexture;
                Texture2D texSpecGlos = TextureConverter.ConvertAlbedoSpecGlosToSpecGloss(texAlbedoSpec, texGlos);
                specularGlossinessTexture = exporter.ExportTextureInfo(texSpecGlos, TextureMapType.Linear);
                ExportTextureTransform(specularGlossinessTexture, material, "_MainTex");


                if (TransparentCutoff)
                    materialNode.AlphaMode = AlphaMode.MASK;

                diffuseTexture = exporter.ExportTextureInfo(texAlbedoSpec, TextureMapType.BaseColor);
                ExportTextureTransform(diffuseTexture, material, "_MainTex");


                Color colorSpec = material.GetColor("_SpecColor");
                float floatSpec = material.GetFloat("_Glossness");
                floatSpec = Mathf.Clamp01(floatSpec);
                floatSpec *= material.GetVector("_SpecVals").x;
                specularFactor.X = colorSpec.r * floatSpec;
                specularFactor.Y = colorSpec.g * floatSpec;
                specularFactor.Z = colorSpec.b * floatSpec;


                float floatGlos = material.GetFloat("_Specularness");
                glossinessFactor = Mathf.Clamp01(floatGlos);


                exporter.DeclareExtensionUsage(KHR_materials_pbrSpecularGlossinessExtensionFactory.EXTENSION_NAME, true);
                if (materialNode.Extensions == null)
                    materialNode.Extensions = new Dictionary<string, IExtension>();
                materialNode.Extensions[KHR_materials_pbrSpecularGlossinessExtensionFactory.EXTENSION_NAME] = new KHR_materials_pbrSpecularGlossinessExtension(
                    diffuseFactor,
                    diffuseTexture,
                    specularFactor,
                    glossinessFactor,
                    specularGlossinessTexture
                );


                var normalTex = material.GetTexture("_BumpMap");
                if (normalTex && normalTex is Texture2D)
                {
                    materialNode.NormalTexture = exporter.ExportNormalTextureInfo(normalTex, TextureMapType.Normal, material);
                    ExportTextureTransform(materialNode.NormalTexture, material, "_BumpMap");
                }

                if (material.HasFloat("_HasTint") && material.GetFloat("_HasTint") > 0.5f)
                {
                    // export the tint mask as occlusion tex. just for the blender convenience.
                    materialNode.OcclusionTexture = new OcclusionTextureInfo();
                    materialNode.OcclusionTexture.Index = exporter.ExportTextureInfo(material.GetTexture("_TintMask"), TextureMapType.Linear).Index;
                    ExportTextureTransform(materialNode.OcclusionTexture, material, "_TintMask");

                    // could potentially bake a new diffuse texture instead. then you won't even need to do further operations in blender
                    // but that seems like a waste and lots of duplicated textures with just the tint being different
                }
                else if (material.shader.name.Contains("Emissive") && material.HasColor("_EmissiveColor"))
                {
                    materialNode.EmissiveTexture = exporter.ExportTextureInfo(material.GetTexture("_EmissionMap"), TextureMapType.BaseColor);
                    ExportTextureTransform(materialNode.EmissiveTexture, material, "_EmissionMap");

                    materialNode.EmissiveFactor = material.GetColor("_EmissiveColor").ToNumericsColorGamma();


                    KHR_materials_emissive_strength emissive = new KHR_materials_emissive_strength();
                    emissive.emissiveStrength = material.GetFloat("_EmissionPower") * material.GetFloat("_EmissionVisibility");

                    exporter.DeclareExtensionUsage(KHR_materials_emissive_strength_Factory.EXTENSION_NAME, true);
                    materialNode.Extensions[KHR_materials_emissive_strength_Factory.EXTENSION_NAME] = emissive;
                }

                return true;
            }
            else if (material.shader.name == "p0/Reflective/Bumped Specular" || material.shader.name == "p0/Reflective/Bumped Emissive Specular")
            {
                GLTF.Math.Color diffuseFactor = KHR_materials_pbrSpecularGlossinessExtension.DIFFUSE_FACTOR_DEFAULT;
                TextureInfo diffuseTexture = KHR_materials_pbrSpecularGlossinessExtension.DIFFUSE_TEXTURE_DEFAULT;
                GLTF.Math.Vector3 specularFactor = KHR_materials_pbrSpecularGlossinessExtension.SPEC_FACTOR_DEFAULT;
                double glossinessFactor = KHR_materials_pbrSpecularGlossinessExtension.GLOSS_FACTOR_DEFAULT;
                TextureInfo specularGlossinessTexture = KHR_materials_pbrSpecularGlossinessExtension.SPECULAR_GLOSSINESS_TEXTURE_DEFAULT;


                diffuseFactor = material.GetColor("_Color").ToNumericsColorGamma();
                float floatDiffuse = material.GetVector("_DefVals").x;
                diffuseFactor.R *= floatDiffuse;
                diffuseFactor.G *= floatDiffuse;
                diffuseFactor.B *= floatDiffuse;


                Texture texAlbedoSpec = material.GetTexture("_MainTex");
                if (texAlbedoSpec == null)
                    texAlbedoSpec = Texture2D.whiteTexture;
                Texture texGlos = Texture2D.whiteTexture;
                Texture2D texSpecGlos = TextureConverter.ConvertAlbedoSpecGlosToSpecGloss(texAlbedoSpec, texGlos);
                specularGlossinessTexture = exporter.ExportTextureInfo(texSpecGlos, TextureMapType.Linear);
                ExportTextureTransform(specularGlossinessTexture, material, "_MainTex");


                diffuseTexture = exporter.ExportTextureInfo(texAlbedoSpec, TextureMapType.BaseColor);
                ExportTextureTransform(diffuseTexture, material, "_MainTex");


                Color colorSpec = material.GetColor("_SpecColor");
                float floatSpec = material.GetFloat("_SpecPower");
                floatSpec = Mathf.Clamp01(floatSpec / 10f);
                specularFactor.X = colorSpec.r * floatSpec;
                specularFactor.Y = colorSpec.g * floatSpec;
                specularFactor.Z = colorSpec.b * floatSpec;


                float floatGlos = material.GetFloat("_SpecPower") * material.GetFloat("_Shininess");
                glossinessFactor = floatGlos;


                exporter.DeclareExtensionUsage(KHR_materials_pbrSpecularGlossinessExtensionFactory.EXTENSION_NAME, true);
                if (materialNode.Extensions == null)
                    materialNode.Extensions = new Dictionary<string, IExtension>();
                materialNode.Extensions[KHR_materials_pbrSpecularGlossinessExtensionFactory.EXTENSION_NAME] = new KHR_materials_pbrSpecularGlossinessExtension(
                    diffuseFactor,
                    diffuseTexture,
                    specularFactor,
                    glossinessFactor,
                    specularGlossinessTexture
                );


                var normalTex = material.GetTexture("_BumpMap");
                if (normalTex && normalTex is Texture2D)
                {
                    materialNode.NormalTexture = exporter.ExportNormalTextureInfo(normalTex, TextureMapType.Normal, material);
                    // ExportTextureTransform(materialNode.NormalTexture, material, "_BumpMap");
                    // the tex tiling isn't used in-game, but some materials have random values, so we omit exporting tex transform
                }

                if (material.HasFloat("_EmissionVisibility"))
                {
                    materialNode.EmissiveTexture = exporter.ExportTextureInfo(material.GetTexture("_EmissionMap"), TextureMapType.BaseColor);
                    ExportTextureTransform(materialNode.EmissiveTexture, material, "_EmissionMap");

                    KHR_materials_emissive_strength emissive = new KHR_materials_emissive_strength();
                    emissive.emissiveStrength = material.GetFloat("_EmissionPower") * material.GetFloat("_EmissionVisibility");

                    exporter.DeclareExtensionUsage(KHR_materials_emissive_strength_Factory.EXTENSION_NAME, true);
                    materialNode.Extensions[KHR_materials_emissive_strength_Factory.EXTENSION_NAME] = emissive;
                }

                return true;
            }
            else if (material.shader.name == "p0/Cutout/Bumped Diffuse")
            {
                material.EnableKeyword("_BUMPMAP");
            }
            else if (material.shader.name == "Global Fog/Transparent Reflective Specular")
            {
                var pbr = new PbrMetallicRoughness() { MetallicFactor = 0, RoughnessFactor = 1.0f };
                pbr.BaseColorTexture = exporter.ExportTextureInfo(material.mainTexture, TextureMapType.BaseColor);
                ExportTextureTransform(pbr.BaseColorTexture, material, "_MainTex");
                pbr.BaseColorFactor = material.GetColor("_Color").ToNumericsColorLinear();
                pbr.BaseColorFactor.A = 1f;

                pbr.MetallicRoughnessTexture = pbr.BaseColorTexture;
                pbr.RoughnessFactor = 1f;
                pbr.MetallicFactor = 0f;

                KHR_materials_transmission transmission = new KHR_materials_transmission();
                transmission.transmissionFactor = 1f;

                exporter.DeclareExtensionUsage(KHR_materials_transmission_Factory.EXTENSION_NAME, true);
                if (materialNode.Extensions == null)
                    materialNode.Extensions = new Dictionary<string, IExtension>();
                materialNode.Extensions[KHR_materials_transmission_Factory.EXTENSION_NAME] = transmission;

                materialNode.PbrMetallicRoughness = pbr;
                materialNode.AlphaMode = AlphaMode.BLEND;

                return true;
            }

            return false;
        }

        private void ExportTextureTransform(TextureInfo def, Material mat, string texName)
        {
            exportTextureTransform.Invoke(_exporter, [def, mat, texName]);
        }
    }
}
