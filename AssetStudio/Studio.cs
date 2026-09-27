using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetStudio;

namespace TarkinItemExporter;

public static class Studio
{
    public static bool LoadAssets(HashSet<string> possibleFilePaths, out List<AssetItem> result)
    {
        result = new List<AssetItem>();

        foreach (string filePath in possibleFilePaths.ToList())
        {
            possibleFilePaths.UnionWith(GetPathsBundleDependencies(filePath));
        }

        if (possibleFilePaths.Count == 0)
            return true;

        AssetsManager assetsManager = new AssetsManager();
        bool failed = false;

        try
        {
            assetsManager.SetAssetFilter(ClassIDType.Mesh);
            assetsManager.LoadFilesAndFolders(possibleFilePaths.ToArray());
            List<AssetItem> justLoaded = StudioParser.ParseAssets(assetsManager);

            result.AddRange(justLoaded);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError(ex);
            failed = true;
        }
        finally
        {
            assetsManager.Clear();
        }

        return !failed;
    }

    private static HashSet<string> GetPathsBundleDependencies(string requestPath)
    {
        string directory = Path.GetDirectoryName(requestPath);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(requestPath);

        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileNameWithoutExtension))
        {
            return new HashSet<string>();
        }

        // Standardize paths for reliable comparison
        string streamingAssetsPath = Path.GetFullPath(Path.Combine(UnityEngine.Application.streamingAssetsPath, "Windows"));
        string fullRequestPath = Path.GetFullPath(requestPath);

        BundleDependencyMap bundleDependencyMap = new BundleDependencyMap(Path.Combine(streamingAssetsPath, "Windows.json"));

        if (!fullRequestPath.StartsWith(streamingAssetsPath, StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<string>();
        }

        // turning absolute path back to relative (to streamingassets dir) path
        string relativePath = fullRequestPath.Substring(streamingAssetsPath.Length);
        if (relativePath.StartsWith(Path.DirectorySeparatorChar.ToString()) || relativePath.StartsWith(Path.AltDirectorySeparatorChar.ToString()))
            relativePath = relativePath.Substring(1);

        List<string> dependeciesRelativePaths = bundleDependencyMap.GetDependencies(relativePath);

        HashSet<string> result = new HashSet<string>();

        var exactSkip = new HashSet<string>()
        {
            "shaders",
            "cubemaps"
        };

        var partialSkip = new List<string>
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

        foreach (string depPath in dependeciesRelativePaths)
        {
            if (exactSkip.Contains(depPath))
                continue;

            if (partialSkip.Any(substring => depPath.Contains(substring)))
                continue;

            result.Add(Path.Combine(streamingAssetsPath, depPath));
        }

        return result;
    }

    private static void GenerateFullPath(BaseNode treeNode, string path)
    {
        treeNode.FullPath = path;
        foreach (var node in treeNode.nodes)
        {
            if (node.nodes.Count > 0)
            {
                GenerateFullPath(node, Path.Combine(path, node.Text));
            }
            else
            {
                node.FullPath = Path.Combine(path, node.Text);
            }
        }
    }

}
