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

                KHR_materials_specular KHRspecular = new KHR_materials_specular();
                var pbr = new PbrMetallicRoughness();
                pbr.MetallicFactor = 0;

                pbr.BaseColorFactor = material.GetColor("_Color").ToNumericsColorGamma();
                float floatDiffuse = material.GetVector("_DefVals").x;
                pbr.BaseColorFactor.R *= floatDiffuse;
                pbr.BaseColorFactor.G *= floatDiffuse;
                pbr.BaseColorFactor.B *= floatDiffuse;

                Texture texAlbedoSpec = material.GetTexture("_MainTex");
                Texture texGlos = material.GetTexture("_SpecMap");
                if (TransparentCutoff)
                {
                    // yes this is the correct setup in the 'Transparent Cutoff' shader
                    texAlbedoSpec = material.GetTexture("_SpecMap");
                    texGlos = material.GetTexture("_MainTex");
                }

                if (texGlos == null)
                    texGlos = Texture2D.whiteTexture;
                if (texAlbedoSpec == null)
                    texAlbedoSpec = Texture2D.whiteTexture;

                Texture2D texRoughness = TextureConverter.Invert(texGlos, false);
                Texture2D texMetRough = TextureConverter.CombineR(Texture2D.blackTexture, texRoughness, Texture2D.blackTexture, Texture2D.whiteTexture);
                texMetRough.name = texGlos.name + "_METROUGH";
                pbr.MetallicRoughnessTexture = exporter.ExportTextureInfo(texMetRough, TextureMapType.Linear);
                ExportTextureTransform(pbr.MetallicRoughnessTexture, material, TransparentCutoff ? "_MainTex" : "_SpecMap");

                Texture2D texAlbedo = TextureConverter.FillAlpha(texAlbedoSpec, 1f);

                if (TransparentCutoff)
                    materialNode.AlphaMode = AlphaMode.MASK;

                if (material.HasFloat("_HasTint") && material.GetFloat("_HasTint") > 0.5f)
                {
                    Color colorTint = material.GetColor("_BaseTintColor");
                    Texture2D texDiffuseWithTint = TextureConverter.BlendOverlay(
                        texAlbedo,
                        TextureConverter.CreateSolidColorTexture(4, 4, colorTint),
                        material.GetTexture("_TintMask") as Texture2D, 1f);

                    texDiffuseWithTint.name = texAlbedoSpec.name + "_" + ColorUtility.ToHtmlStringRGB(colorTint);

                    pbr.BaseColorTexture = exporter.ExportTextureInfo(texDiffuseWithTint, TextureMapType.BaseColor);
                }
                else
                {
                    pbr.BaseColorTexture = exporter.ExportTextureInfo(texAlbedo, TextureMapType.BaseColor);
                }
                ExportTextureTransform(pbr.BaseColorTexture, material, TransparentCutoff ? "_SpecMap" : "_MainTex");

                GLTF.Math.Color specularColor = KHR_materials_specular.COLOR_DEFAULT;

                Color colorSpec = material.GetColor("_SpecColor");
                float floatSpec = material.GetFloat("_Glossness");
                floatSpec = Mathf.Clamp01(floatSpec);
                floatSpec *= material.GetVector("_SpecVals").x;

                KHRspecular.specularFactor = floatSpec;
                KHRspecular.specularColorFactor = colorSpec.ToNumericsColorRaw();
                Texture2D texSpec = TextureConverter.ChannelToGrayscale(texAlbedoSpec, 3);
                texSpec.name = texAlbedoSpec.name + "_SPEC";
                KHRspecular.specularTexture = exporter.ExportTextureInfo(texSpec, TextureMapType.Linear);
                ExportTextureTransform(KHRspecular.specularTexture, material, TransparentCutoff ? "_SpecMap" : "_MainTex");

                float floatGlos = material.GetFloat("_Specularness");
                float glossinessFactor = Mathf.Clamp01(floatGlos);
                pbr.RoughnessFactor = glossinessFactor;

                exporter.DeclareExtensionUsage(KHR_materials_specular_Factory.EXTENSION_NAME, true);
                if (materialNode.Extensions == null)
                    materialNode.Extensions = new Dictionary<string, IExtension>();
                materialNode.Extensions[KHR_materials_specular_Factory.EXTENSION_NAME] = KHRspecular;

                materialNode.PbrMetallicRoughness = pbr;

                var normalTex = material.GetTexture("_BumpMap");
                if (normalTex && normalTex is Texture2D)
                {
                    materialNode.NormalTexture = exporter.ExportNormalTextureInfo(normalTex, TextureMapType.Normal, material);
                    ExportTextureTransform(materialNode.NormalTexture, material, "_BumpMap");
                }

                if (material.shader.name.Contains("Emissive") && material.HasColor("_EmissiveColor"))
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
                KHR_materials_specular KHRspecular = new KHR_materials_specular();
                var pbr = new PbrMetallicRoughness();
                pbr.MetallicFactor = 0;

                Color diffuseFactor = material.GetColor("_Color");
                float floatDiffuse = material.GetVector("_DefVals").x;
                diffuseFactor.r *= floatDiffuse;
                diffuseFactor.g *= floatDiffuse;
                diffuseFactor.b *= floatDiffuse;
                pbr.BaseColorFactor = diffuseFactor.ToNumericsColorGamma();

                Texture texAlbedoSpec = material.GetTexture("_MainTex");
                if (texAlbedoSpec == null)
                    texAlbedoSpec = Texture2D.whiteTexture;
                pbr.BaseColorTexture = exporter.ExportTextureInfo(texAlbedoSpec, TextureMapType.BaseColor);
                ExportTextureTransform(pbr.BaseColorTexture, material, "_MainTex");

                Texture2D texSpec = TextureConverter.ChannelToGrayscale(texAlbedoSpec, 3);
                texSpec.name = texAlbedoSpec.name + "_SPEC";
                KHRspecular.specularTexture = exporter.ExportTextureInfo(texSpec, TextureMapType.Linear);
                ExportTextureTransform(KHRspecular.specularTexture, material, "_MainTex");

                Color colorSpec = material.GetColor("_SpecColor");
                float floatSpec = material.GetFloat("_SpecPower");
                floatSpec = Mathf.Clamp01(floatSpec / 10f);
                KHRspecular.specularFactor = floatSpec;
                KHRspecular.specularColorFactor = colorSpec.ToNumericsColorRaw();

                float floatGlos = material.GetFloat("_SpecPower") * material.GetFloat("_Shininess");
                pbr.RoughnessFactor = 1f - floatGlos;

                materialNode.PbrMetallicRoughness = pbr;

                exporter.DeclareExtensionUsage(KHR_materials_specular_Factory.EXTENSION_NAME, true);
                if (materialNode.Extensions == null)
                    materialNode.Extensions = new Dictionary<string, IExtension>();
                materialNode.Extensions[KHR_materials_specular_Factory.EXTENSION_NAME] = KHRspecular;

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
            else if (material.shader.name.Contains("CW FX/Collimator"))
            {
                var pbr = new PbrMetallicRoughness() { MetallicFactor = 0, RoughnessFactor = 0 };

                KHR_materials_transmission transmission = new KHR_materials_transmission();
                transmission.transmissionFactor = 1f;

                exporter.DeclareExtensionUsage(KHR_materials_transmission_Factory.EXTENSION_NAME, true);
                if (materialNode.Extensions == null)
                    materialNode.Extensions = new Dictionary<string, IExtension>();
                materialNode.Extensions[KHR_materials_transmission_Factory.EXTENSION_NAME] = transmission;

                materialNode.PbrMetallicRoughness = pbr;

                return true;
            }
            else if (material.shader.name.Contains("Custom/OpticGlass"))
            {
                var pbr = new PbrMetallicRoughness() { MetallicFactor = 1f, RoughnessFactor = 0.05f };
                pbr.BaseColorFactor = Color.black.ToNumericsColorRaw();
                materialNode.PbrMetallicRoughness = pbr;

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
