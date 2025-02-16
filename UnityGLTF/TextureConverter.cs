using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace gltfmod.UnityGLTF
{
    internal static class TextureConverter
    {
        public static Texture2D Convert(Texture inputTexture, Shader shader)
        {
            Material mat = new Material(shader);

            bool sRGBWrite = GL.sRGBWrite;
            GL.sRGBWrite = false;
            RenderTexture temporary = RenderTexture.GetTemporary(inputTexture.width, inputTexture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

            Graphics.Blit(inputTexture, temporary, mat);

            Texture2D convertedTexture = temporary.ToTexture2D();

            RenderTexture.ReleaseTemporary(temporary);
            GL.sRGBWrite = sRGBWrite;

            return convertedTexture;
        }

        static Dictionary<Texture, Texture2D> cache = new Dictionary<Texture, Texture2D>();

        public static Texture2D ConvertAlbedoSpecGlosToSpecGloss(Texture inputTextureA, Texture inputTextureB)
        {
            if (cache.ContainsKey(inputTextureA))
            {
                Texture2D cached = cache[inputTextureA];
                Plugin.Log.LogInfo($"Using cached converted texture {cached.name}");
                return cached;
            }

            Material mat = new Material(BundleShaders.Find("Hidden/AlbedoSpecGlosToSpecGloss"));
            mat.SetTexture("_AlbedoSpecTex", inputTextureA);
            mat.SetTexture("_GlossinessTex", inputTextureB);

            bool sRGBWrite = GL.sRGBWrite;
            GL.sRGBWrite = false;
            RenderTexture temporary = RenderTexture.GetTemporary(inputTextureA.width, inputTextureA.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

            Graphics.Blit(inputTextureA, temporary, mat);

            Texture2D convertedTexture = temporary.ToTexture2D();
            convertedTexture.name = ReplaceLastWord(inputTextureA.name, '_', "SPECGLOS");

            RenderTexture.ReleaseTemporary(temporary);
            GL.sRGBWrite = sRGBWrite;

            cache[inputTextureA] = convertedTexture;

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

        static string ReplaceLastWord(string input, char separator, string replacement)
        {
            int lastIndex = input.LastIndexOf(separator);
            if (lastIndex == -1)
            {
                return replacement;
            }
            return input.Substring(0, lastIndex + 1) + replacement;
        }
    }
}
