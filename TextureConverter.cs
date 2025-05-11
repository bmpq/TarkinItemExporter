using System.Collections.Generic;
using UnityEngine;

namespace TarkinItemExporter
{
    public static class TextureConverter
    {
        public static Texture2D Convert(Texture inputTexture, Material mat)
        {
            bool sRGBWrite = GL.sRGBWrite;
            GL.sRGBWrite = false;
            RenderTexture temporary = RenderTexture.GetTemporary(inputTexture.width, inputTexture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

            Graphics.Blit(inputTexture, temporary, mat);

            Texture2D convertedTexture = temporary.ToTexture2D();

            RenderTexture.ReleaseTemporary(temporary);
            GL.sRGBWrite = sRGBWrite;

            convertedTexture.name = inputTexture.name;

            return convertedTexture;
        }

        static Dictionary<Texture, Texture2D> cache = new Dictionary<Texture, Texture2D>();

        public static Texture2D ConvertAlbedoSpecGlosToSpecGloss(Texture inputTextureAlbedoSpec, Texture inputTextureGloss)
        {
            if (cache.ContainsKey(inputTextureAlbedoSpec))
            {
                Texture2D cached = cache[inputTextureAlbedoSpec];
                Plugin.Log.LogInfo($"Using cached converted texture {cached.name}");
                return cached;
            }

            Material mat = new Material(BundleShaders.Find("Hidden/AlbedoSpecGlosToSpecGloss"));
            mat.SetTexture("_AlbedoSpecTex", inputTextureAlbedoSpec);
            mat.SetTexture("_GlossinessTex", inputTextureGloss);

            bool sRGBWrite = GL.sRGBWrite;
            GL.sRGBWrite = false;
            RenderTexture temporary = RenderTexture.GetTemporary(inputTextureAlbedoSpec.width, inputTextureAlbedoSpec.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

            Graphics.Blit(inputTextureAlbedoSpec, temporary, mat);

            Texture2D convertedTexture = temporary.ToTexture2D();
            convertedTexture.name += inputTextureAlbedoSpec.name + "_SPECGLOS";

            RenderTexture.ReleaseTemporary(temporary);
            GL.sRGBWrite = sRGBWrite;

            cache[inputTextureAlbedoSpec] = convertedTexture;

            return convertedTexture;
        }

        public static Texture2D CombineR(Texture texR, Texture texG, Texture texB, Texture texA)
        {
            Material mat = new Material(BundleShaders.Find("Hidden/CombineR"));
            mat.SetTexture("_RTex", texR);
            mat.SetTexture("_GTex", texG);
            mat.SetTexture("_BTex", texB);
            mat.SetTexture("_ATex", texA);

            int maxWidth = Mathf.Max(texR.width, texG.width, texB.width, texA.width);
            int maxHeight = Mathf.Max(texR.height, texG.height, texB.height, texA.height);

            bool sRGBWrite = GL.sRGBWrite;
            GL.sRGBWrite = false;
            RenderTexture temporary = RenderTexture.GetTemporary(maxWidth, maxHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

            Graphics.Blit(Texture2D.blackTexture, temporary, mat);

            Texture2D convertedTexture = temporary.ToTexture2D();

            RenderTexture.ReleaseTemporary(temporary);
            GL.sRGBWrite = sRGBWrite;

            return convertedTexture;
        }

        public static Texture2D Invert(Texture tex, bool invertAlpha = true)
        {
            Material mat = new Material(BundleShaders.Find("Hidden/Invert"));
            mat.SetTexture("_MainTex", tex);

            mat.SetKeyword("INVERT_ALPHA", invertAlpha);

            bool sRGBWrite = GL.sRGBWrite;
            GL.sRGBWrite = false;
            RenderTexture temporary = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

            Graphics.Blit(tex, temporary, mat);

            Texture2D convertedTexture = temporary.ToTexture2D();

            RenderTexture.ReleaseTemporary(temporary);
            GL.sRGBWrite = sRGBWrite;

            return convertedTexture;
        }

        public static Texture2D ChannelToGrayscale(Texture tex, int channel)
        {
            Material mat = new Material(BundleShaders.Find("Hidden/ChannelToGrayscale"));

            mat.SetTexture("_MainTex", tex);
            mat.SetInt("_ChannelSelect", channel);

            bool sRGBWrite = GL.sRGBWrite;
            GL.sRGBWrite = false;
            RenderTexture temporary = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

            Graphics.Blit(tex, temporary, mat);

            Texture2D convertedTexture = temporary.ToTexture2D();

            RenderTexture.ReleaseTemporary(temporary);
            GL.sRGBWrite = sRGBWrite;

            return convertedTexture;
        }

        static Texture2D ToTexture2D(this RenderTexture rTex)
        {
            Texture2D tex = new Texture2D(rTex.width, rTex.height, TextureFormat.RGBA32, false);
            RenderTexture.active = rTex;
            tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
            tex.Apply();

            return tex;
        }

        public static Texture2D CreateSolidColorTexture(int width, int height, float r, float g, float b, float a)
        {
            Texture2D texture = new Texture2D(width, height);

            Color[] pixels = new Color[width * height];
            Color color = new Color(r, g, b, a);

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return texture;
        }

        public static Texture2D CreateSolidColorTexture(int width, int height, float c, float a)
        {
            return CreateSolidColorTexture(width, height, c, c, c, a);
        }
    }
}
