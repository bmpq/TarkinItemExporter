using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TarkinItemExporter
{
    public class AssetBundleHandler : IDisposable
    {
        private readonly string pathDirBundles;
        private readonly ManualLogSource logger;

        private readonly Dictionary<string, AssetBundle> loadedBundles = new();

        public AssetBundleHandler(string fullpathBundleDir, ManualLogSource logger = null) 
        {
            this.pathDirBundles = fullpathBundleDir;
            this.logger = logger;
        }

        public AssetBundle LoadBundle(string bundleName)
        {
            string fullBundlePath = Path.Combine(pathDirBundles, bundleName);
            if (!File.Exists(fullBundlePath))
            {
                logger?.LogError($"Asset bundle not found: {fullBundlePath}");
                return null;
            }

            if (loadedBundles.TryGetValue(bundleName, out AssetBundle cachedBundle))
                return cachedBundle;

            AssetBundle bundle = AssetBundle.LoadFromFile(fullBundlePath);
            if (bundle == null)
            {
                logger?.LogError($"Failed to load asset bundle: {fullBundlePath}");
                return null;
            }

            loadedBundles[bundleName] = bundle;

            return bundle;
        }

        public T LoadAsset<T>(string bundleName, string assetName) where T : UnityEngine.Object
        {
            AssetBundle bundle = LoadBundle(bundleName);
            if (bundle == null)
                return null;
            T asset = bundle.LoadAsset<T>(assetName);
            if (asset == null)
            {
                logger?.LogError($"Failed to load asset '{assetName}' from bundle '{bundleName}'");
                return null;
            }
            return asset;
        }

        public void Dispose()
        {
            foreach (var bundle in loadedBundles.Values)
            {
                if (bundle != null)
                    bundle.Unload(false);
            }
            loadedBundles.Clear();
        }
    }
}
