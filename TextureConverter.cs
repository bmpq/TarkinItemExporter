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

        public static Texture2D BlendOverlay(Texture2D texBase, Texture2D texTop, Texture2D texMask, float factor)
        {
            Material mat = new Material(BundleShaders.Find("Hidden/BlendOverlay"));
            mat.SetTexture("_MainTex", texBase);
            mat.SetTexture("_TopTex", texTop);
            mat.SetTexture("_MaskTex", texMask);
            mat.SetFloat("_Factor", factor);

            bool sRGBWrite = GL.sRGBWrite;
            GL.sRGBWrite = false;
            RenderTexture temporary = RenderTexture.GetTemporary(texBase.width, texBase.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

            Graphics.Blit(texBase, temporary, mat);

            Texture2D convertedTexture = temporary.ToTexture2D();

            RenderTexture.ReleaseTemporary(temporary);
            GL.sRGBWrite = sRGBWrite;

            return convertedTexture;
        }

        public static Texture2D FillAlpha(Texture tex, float alpha = 1.0f)
        {
            Material mat = new Material(BundleShaders.Find("Hidden/FillAlpha"));

            return Convert(tex, mat);
        }

        static Texture2D ToTexture2D(this RenderTexture rTex)
        {
            Texture2D tex = new Texture2D(rTex.width, rTex.height, TextureFormat.RGBA32, false);
            RenderTexture.active = rTex;
            tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
            tex.Apply();

            return tex;
        }

        public static Texture2D CreateSolidColorTexture(int width, int height, Color color)
        {
            Texture2D texture = new Texture2D(width, height);

            Color[] pixels = new Color[width * height];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return texture;
        }
    }
}
