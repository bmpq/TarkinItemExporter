using System;
using System.Collections.Generic;
using UnityEngine;

namespace TarkinItemExporter
{
    internal class BundleShaders
    {
        static Dictionary<string, Shader> shaders;

        public static void Add(Shader add)
        {
            if (shaders == null)
            {
                shaders = new Dictionary<string, Shader>();
            }

            if (add != null && !string.IsNullOrEmpty(add.name))
            {
                if (!shaders.ContainsKey(add.name))
                {
                    Plugin.Log.LogInfo($"Added {add.name} shader to BundleShaders.");
                    shaders[add.name] = add;
                }
                else
                {
                    // Handle duplicate shader names (optional - logging, warning, etc.)
                    Plugin.Log.LogWarning($"Shader with name '{add.name}' already exists in BundleShaders.");
                }
            }
            else
            {
                Plugin.Log.LogError("Cannot add null shader or shader with empty name to BundleShaders.");
            }
        }

        public static void Add(Shader[] add)
        {
            if (add == null)
            {
                Plugin.Log.LogError("Cannot add null shader array to BundleShaders.");
                return;
            }
            foreach (Shader shader in add)
            {
                Add(shader);
            }
        }

        public static Shader Find(string name)
        {
            if (shaders == null)
            {
                Plugin.Log.LogWarning("No shaders have been added to BundleShaders yet.");
                return null;
            }

            if (string.IsNullOrEmpty(name))
            {
                Plugin.Log.LogError("Cannot find shader with null or empty name.");
                return null;
            }

            if (shaders.TryGetValue(name, out Shader shader))
            {
                Plugin.Log.LogInfo($"Shader '{name}' found successfully!");
                return shader;
            }
            else
            {
                return null;
            }
        }
    }
}