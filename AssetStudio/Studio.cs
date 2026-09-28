using System;
using System.Collections.Generic;
using System.Linq;
using AssetStudio;

namespace TarkinItemExporter;

public static class Studio
{
    public static bool LoadAssets(HashSet<string> possibleFilePaths, out List<AssetItem> result)
    {
        result = new List<AssetItem>();

        HashSet<string> allPathsToLoad = BundleResolver.ResolveAllDependencies(possibleFilePaths);

        if (allPathsToLoad.Count == 0)
            return true;

        AssetsManager assetsManager = new AssetsManager();
        bool failed = false;

        try
        {
            assetsManager.SetAssetFilter(ClassIDType.Mesh);
            assetsManager.LoadFilesAndFolders(allPathsToLoad.ToArray());
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
}