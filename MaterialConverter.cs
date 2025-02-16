using AssetBundleLoader;
using System;
using UnityEngine;

namespace gltfmod
{
    public static class MaterialConverter
    {
        public static Material ConvertToSpecGlos(this Material origMat)
        {
            if (!origMat.shader.name.Contains("Bumped Specular"))
                return origMat;

            Plugin.Log.LogInfo($"{origMat.name}: converting to gltf specular-gloss...");

            try
            {
                Color origColor = origMat.color;
                Texture origTexMain = origMat.GetTexture("_MainTex");
                Texture origTexGloss = origMat.GetTexture("_SpecMap");
                Texture origTexNormal = origMat.GetTexture("_BumpMap");

                Texture2D texSpecGlos = TextureConverter.ConvertAlbedoSpecGlosToSpecGloss(origTexMain, origTexGloss);

                // this material is from UnityGLTF package
                Material newMat = UnityEngine.Object.Instantiate(BundleLoader.LoadAssetBundle("unitygltf").LoadAsset<Material>("Standard (Specular setup)"));

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
