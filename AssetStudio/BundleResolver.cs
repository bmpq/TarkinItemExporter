using Diz.Utils;
using EFT.AssetsManager;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TarkinItemExporter
{
    public static class BundleResolver
    {
        private static bool _initialized = false;
        private static readonly object _lock = new object();

        private static string _streamingAssetsWindows;
        private static BundleDependencyMap _vanillaDepMap;

        // Key: Normalized key / filename -> Absolute path on disk
        private static readonly Dictionary<string, string> _bundlePathLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Key: Normalized absolute path -> List of dependency keys
        private static readonly Dictionary<string, List<string>> _dependenciesLookup = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> ExactSkips = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "shaders",
            "cubemaps"
        };

        private static readonly string[] PartialSkips = new[]
        {
            "content/audio",
            "animations/",
            "textures/",
            "weapon_root_anim_fix",
            "additional_hands",
            "assets/systems",
            "muzzlejets_templates",
            "physicsmaterials"
        };

        private class ModManifestRoot
        {
            [JsonProperty("manifest")]
            public List<ModManifestEntry> Manifest { get; set; }
        }

        private class ModManifestEntry
        {
            [JsonProperty("key")]
            public string Key { get; set; }

            [JsonProperty("dependencyKeys")]
            public List<string> DependencyKeys { get; set; }
        }

        public static void Initialize()
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;

                BuildRegistry();
                _initialized = true;
            }
        }

        private static void BuildRegistry()
        {
            _streamingAssetsWindows = Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, "Windows"));
            string windowsJson = Path.Combine(_streamingAssetsWindows, "Windows.json");
            if (File.Exists(windowsJson))
            {
                try
                {
                    _vanillaDepMap = new BundleDependencyMap(windowsJson);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Failed to load vanilla Windows.json: {ex.Message}");
                }
            }

            DirectoryInfo gameRootDir = new DirectoryInfo(Application.streamingAssetsPath).Parent.Parent;

            string[] potentialModRoots = new[]
            {
                Path.Combine(gameRootDir.FullName, "SPT_Runtime", "user", "mods")
            };

            foreach (string modRoot in potentialModRoots)
            {
                if (!Directory.Exists(modRoot)) continue;

                foreach (string modDir in Directory.GetDirectories(modRoot))
                {
                    IndexModDirectory(modDir);
                }
            }

            // Index client/Fika bundle caches
            string[] potentialCacheRoots = new[]
            {
                Path.Combine(gameRootDir.FullName, "SPT_Runtime", "user", "cache", "bundles"),
                Path.Combine(gameRootDir.FullName, "user", "cache", "bundles")
            };

            foreach (string cacheRoot in potentialCacheRoots)
            {
                if (!Directory.Exists(cacheRoot)) continue;

                foreach (string file in Directory.GetFiles(cacheRoot, "*.bundle", SearchOption.AllDirectories))
                {
                    RegisterBundleFile(file, null);
                }
            }
        }

        private static void IndexModDirectory(string modDir)
        {
            string bundlesJsonPath = Path.Combine(modDir, "bundles.json");
            string bundlesFolderPath = Path.Combine(modDir, "bundles");

            if (File.Exists(bundlesJsonPath))
            {
                try
                {
                    string json = File.ReadAllText(bundlesJsonPath);
                    ModManifestRoot manifestRoot = JsonConvert.DeserializeObject<ModManifestRoot>(json);

                    if (manifestRoot?.Manifest != null)
                    {
                        foreach (var entry in manifestRoot.Manifest)
                        {
                            if (string.IsNullOrEmpty(entry.Key)) continue;

                            string candidatePath = Path.Combine(bundlesFolderPath, entry.Key);
                            if (!File.Exists(candidatePath))
                            {
                                candidatePath = Path.Combine(bundlesFolderPath, Path.GetFileName(entry.Key));
                            }

                            if (!File.Exists(candidatePath) && !candidatePath.EndsWith(".bundle"))
                            {
                                if (File.Exists(candidatePath + ".bundle"))
                                    candidatePath += ".bundle";
                            }

                            if (File.Exists(candidatePath))
                            {
                                RegisterBundleFile(candidatePath, entry.DependencyKeys, entry.Key);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"Failed to parse {bundlesJsonPath}: {ex.Message}");
                }
            }

            // Also index any loose bundles not explicitly listed in bundles.json
            if (Directory.Exists(bundlesFolderPath))
            {
                foreach (string file in Directory.GetFiles(bundlesFolderPath, "*.bundle", SearchOption.AllDirectories))
                {
                    RegisterBundleFile(file, null);
                }
            }
        }

        private static void RegisterBundleFile(string fullPath, List<string> dependencies, string explicitKey = null)
        {
            fullPath = Path.GetFullPath(fullPath);

            if (!_dependenciesLookup.ContainsKey(fullPath))
            {
                _dependenciesLookup[fullPath] = dependencies ?? new List<string>();
            }

            if (!string.IsNullOrEmpty(explicitKey))
            {
                RegisterKey(explicitKey, fullPath);
            }

            // Register by filename and relative pathing
            RegisterKey(Path.GetFileName(fullPath), fullPath);
            RegisterKey(Path.GetFileNameWithoutExtension(fullPath), fullPath);
        }

        private static void RegisterKey(string key, string fullPath)
        {
            string normalized = NormalizeKey(key);
            if (!_bundlePathLookup.ContainsKey(normalized))
                _bundlePathLookup[normalized] = fullPath;

            if (normalized.EndsWith(".bundle"))
            {
                string withoutExt = normalized.Substring(0, normalized.Length - 7);
                if (!_bundlePathLookup.ContainsKey(withoutExt))
                    _bundlePathLookup[withoutExt] = fullPath;
            }
            else
            {
                string withExt = normalized + ".bundle";
                if (!_bundlePathLookup.ContainsKey(withExt))
                    _bundlePathLookup[withExt] = fullPath;
            }
        }

        public static string NormalizeKey(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            string normalized = path.Replace('\\', '/').Trim().ToLowerInvariant();
            if (normalized.StartsWith("/")) normalized = normalized.Substring(1);
            return normalized;
        }

        public static string FindBundlePath(string resourcePath)
        {
            Initialize();

            if (string.IsNullOrEmpty(resourcePath))
                return null;

            if (File.Exists(resourcePath))
                return Path.GetFullPath(resourcePath);

            // 1. Check Vanilla StreamingAssets
            string vanillaPath = Path.GetFullPath(Path.Combine(_streamingAssetsWindows, resourcePath));
            if (File.Exists(vanillaPath))
                return vanillaPath;

            if (!vanillaPath.EndsWith(".bundle") && File.Exists(vanillaPath + ".bundle"))
                return vanillaPath + ".bundle";

            // 2. Check Mod / Cache Registry
            string normalized = NormalizeKey(resourcePath);
            if (_bundlePathLookup.TryGetValue(normalized, out string found))
                return found;

            // 3. Fallback: match by filename
            string fileName = NormalizeKey(Path.GetFileName(resourcePath));
            if (_bundlePathLookup.TryGetValue(fileName, out found))
                return found;

            return null;
        }

        public static HashSet<string> ResolveAllDependencies(IEnumerable<string> rootPaths, System.Text.StringBuilder traceLog = null)
        {
            Initialize();

            HashSet<string> results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Queue<string> queue = new Queue<string>();
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string root in rootPaths)
            {
                string resolved = FindBundlePath(root);
                if (!string.IsNullOrEmpty(resolved) && visited.Add(resolved))
                {
                    results.Add(resolved);
                    queue.Enqueue(resolved);
                    traceLog?.AppendLine($"[+] Root bundle found: {resolved}");
                }
                else
                {
                    traceLog?.AppendLine($"[-] Could not locate bundle for: {root}");
                }
            }

            while (queue.Count > 0)
            {
                string currentBundle = queue.Dequeue();
                List<string> dependencies = GetDependencies(currentBundle);

                foreach (string depKey in dependencies)
                {
                    if (ShouldSkipDependency(depKey))
                        continue;

                    string resolvedDep = FindBundlePath(depKey);
                    if (!string.IsNullOrEmpty(resolvedDep))
                    {
                        if (visited.Add(resolvedDep))
                        {
                            results.Add(resolvedDep);
                            queue.Enqueue(resolvedDep);
                            traceLog?.AppendLine($"  -> Dependency resolved: {depKey} => {resolvedDep}");
                        }
                    }
                    else
                    {
                        traceLog?.AppendLine($"  -> Missing dependency: {depKey} (required by {Path.GetFileName(currentBundle)})");
                    }
                }
            }

            return results;
        }

        private static List<string> GetDependencies(string fullBundlePath)
        {
            // Check mod manifest dependencies
            if (_dependenciesLookup.TryGetValue(fullBundlePath, out var modDeps) && modDeps.Count > 0)
            {
                return modDeps;
            }

            // Check vanilla dependencies
            if (fullBundlePath.StartsWith(_streamingAssetsWindows, StringComparison.OrdinalIgnoreCase) && _vanillaDepMap != null)
            {
                string relativePath = fullBundlePath.Substring(_streamingAssetsWindows.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/');
                return _vanillaDepMap.GetDependencies(relativePath) ?? new List<string>();
            }

            return new List<string>();
        }

        private static bool ShouldSkipDependency(string depKey)
        {
            if (string.IsNullOrWhiteSpace(depKey)) return true;

            string normalized = depKey.Replace('\\', '/').ToLowerInvariant();

            if (ExactSkips.Contains(normalized)) return true;

            foreach (var skip in PartialSkips)
            {
                if (normalized.Contains(skip))
                    return true;
            }

            return false;
        }
    }
}
