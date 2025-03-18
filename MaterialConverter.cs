﻿using tarkin;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TarkinItemExporter
{
    public static class MaterialConverter
    {
        // this caching makes caching the texture in `ConvertAlbedoSpecGlosToSpecGloss` redundant, I guess.
        // caching also could result in wrong materials export if the base color texture is the same but with different glossiness or normal. Can't imagine why that would ever happen though.
        // caching is required if we want meshes in gltf with the same material to actually reference that material, and not have their own instance, having duplicated materials seems wrong
        static Dictionary<Texture, Material> cache = new Dictionary<Texture, Material>();

        public static Material ConvertToUnityGLTFCompatible(this Material origMat)
        {
            if (origMat.shader.name.Contains("CW FX/BackLens"))
            {
                origMat.shader = Shader.Find("Sprites/Default");
                origMat.color = Color.black;

                return origMat;
            }

            if (origMat.shader.name.Contains("Custom/OpticGlass"))
            {
                origMat.color = Color.black;

                return origMat;
            }

            if (!origMat.shader.name.Contains("Bumped Specular"))
                return origMat;

            Plugin.Log.LogInfo($"{origMat.name}: converting to gltf specular-gloss...");

            try
            {
                Texture origTexMain = origMat.GetTexture("_MainTex");
                if (origTexMain == null)
                    return origMat;

                if (cache.ContainsKey(origTexMain))
                {
                    Material cached = cache[origTexMain];
                    Plugin.Log.LogInfo($"Using cached converted material {cached.name}");
                    return cached;
                }

                Texture origTexGloss;
                // this shader doesn't work with glos or spec textures, but with floats
                // so we create replacement solid color textures
                if (origMat.shader.name == "p0/Reflective/Bumped Specular")
                {
                    float glos = origMat.GetFloat("_Shininess");
                    origTexGloss = TextureConverter.CreateSolidColorTexture(origTexMain.width, origTexMain.height, glos, 1f);
                    origTexGloss.name = origTexMain.name.ReplaceLastWord('_', "GLOSSINESS");

                    Material mat = new Material(BundleShaders.Find("Hidden/SetAlpha"));
                    mat.SetFloat("_Alpha", origMat.GetColor("_SpecColor").r);
                    origTexMain = TextureConverter.Convert(origTexMain, mat);
                }
                else
                    origTexGloss = origMat.GetTexture("_SpecMap");

                Texture origTexNormal;
                if (!origMat.HasProperty("_BumpMap"))
                    origTexNormal = Texture2D.normalTexture;
                else
                    origTexNormal = origMat.GetTexture("_BumpMap");

                Texture2D texSpecGlos = TextureConverter.ConvertAlbedoSpecGlosToSpecGloss(origTexMain, origTexGloss);

                Color origColor = origMat.color;

                // this material is from UnityGLTF package
                Material newMat = UnityEngine.Object.Instantiate(AssetBundleLoader.LoadAssetBundle("unitygltf").LoadAsset<Material>("Standard (Specular setup)"));

                newMat.SetColor("_Color", origColor);
                newMat.SetTexture("_MainTex", origTexMain);
                newMat.SetTexture("_SpecGlossMap", texSpecGlos);
                newMat.SetColor("_SpecColor", Color.white);
                newMat.SetTexture("_BumpMap", origTexNormal);

                newMat.EnableKeyword("_SPECGLOSSMAP");
                newMat.EnableKeyword("_NORMALMAP");

                if (origMat.HasProperty("_EmissionMap"))
                {
                    newMat.SetTexture("_EmissionMap", origMat.GetTexture("_EmissionMap"));
                    Color color = Color.white * origMat.GetFloat("_EmissionPower");
                    newMat.SetColor("_EmissionColor", color);
                }
                else
                {
                    newMat.SetColor("_EmissionColor", Color.black);
                }

                newMat.name = origMat.name;

                cache[origTexMain] = newMat;

                return newMat;
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                return origMat;
            }
        }
    }
}
