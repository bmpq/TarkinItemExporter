using AssetStudio;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TarkinItemExporter;

public class Studio
{
    static Dictionary<string, List<AssetItem>> fileCache = new Dictionary<string, List<AssetItem>>();
    static BundleDependencyMap bundleDependencyMap;

    public static bool LoadAssets(HashSet<string> possibleFilePaths, out List<AssetItem> result)
    {
        result = new List<AssetItem>();

        foreach (string filePath in possibleFilePaths.ToList())
        {
            possibleFilePaths.UnionWith(GetPathsBundleDependencies(filePath));
        }

        List<string> fileAlreadyLoaded = new List<string>();
        foreach (var path in possibleFilePaths)
        {
            string normPath = Path.GetFullPath(path);
            if (fileCache.ContainsKey(normPath))
            {
                result.AddRange(fileCache[normPath]);
                fileAlreadyLoaded.Add(path);
            }
        }
        possibleFilePaths.RemoveWhere(fileAlreadyLoaded.Contains);

        if (possibleFilePaths.Count == 0)
            return true;

        AssetsManager assetsManager = new AssetsManager();

        try
        {
            assetsManager.LoadFilesAndFolders(possibleFilePaths.ToArray());
            List<AssetItem> justLoaded = ParseAssets(assetsManager);

            foreach (AssetItem asset in justLoaded)
            {
                string normPath = Path.GetFullPath(asset.SourceFile.originalPath);
                if (!fileCache.ContainsKey(normPath))
                    fileCache[normPath] = new List<AssetItem>();
                fileCache[normPath].Add(asset);
            }

            result.AddRange(justLoaded);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError(ex);
            return false;
        }

        return true;
    }

    static HashSet<string> GetPathsBundleDependencies(string requestPath)
    {
        string directory = Path.GetDirectoryName(requestPath);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(requestPath);

        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileNameWithoutExtension))
        {
            return new HashSet<string>();
        }

        HashSet<string> result = new HashSet<string>();

        string streamingAssetsPath = Path.Combine(UnityEngine.Application.streamingAssetsPath, "Windows");

        if (bundleDependencyMap == null)
            bundleDependencyMap = new BundleDependencyMap(Path.Combine(streamingAssetsPath, "Windows.json"));

        // turning absolute path back to relative (to streamingassets dir) path
        string relativePath = requestPath.Substring(streamingAssetsPath.Length);
        if (relativePath.StartsWith(Path.DirectorySeparatorChar.ToString()) || relativePath.StartsWith(Path.AltDirectorySeparatorChar.ToString()))
            relativePath = relativePath.Substring(1);

        List<string> dependeciesRelativePaths = bundleDependencyMap.GetDependencies(relativePath);

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

    // this method is taken from AssetStudioCLI.Studio, stripped CLI filter options and skipping tree building
    private static List<AssetItem> ParseAssets(AssetsManager assetsManager)
    {
        Logger.Info("Parse assets...");

        List<AssetItem> parsedAssetsList = new List<AssetItem>();

        var fileAssetsList = new List<AssetItem>();
        var tex2dArrayAssetList = new List<AssetItem>();
        var objectCount = assetsManager.assetsFileList.Sum(x => x.Objects.Count);
        var objectAssetItemDic = new Dictionary<AssetStudio.Object, AssetItem>(objectCount);
        Dictionary<AssetStudio.Object, string> containers = new Dictionary<AssetStudio.Object, string>();

        Progress.Reset();
        var i = 0;
        foreach (var assetsFile in assetsManager.assetsFileList)
        {
            var preloadTable = Array.Empty<PPtr<AssetStudio.Object>>();
            foreach (AssetStudio.Object asset in assetsFile.Objects)
            {
                var assetItem = new AssetItem(asset);
                objectAssetItemDic.Add(asset, assetItem);
                assetItem.UniqueID = "_#" + i;
                switch (asset)
                {
                    case PreloadData m_PreloadData:
                        preloadTable = m_PreloadData.m_Assets;
                        break;
                    case AssetBundle m_AssetBundle:
                        var isStreamedSceneAssetBundle = m_AssetBundle.m_IsStreamedSceneAssetBundle;
                        if (!isStreamedSceneAssetBundle)
                        {
                            preloadTable = m_AssetBundle.m_PreloadTable;
                        }
                        assetItem.Text = string.IsNullOrEmpty(m_AssetBundle.m_AssetBundleName) ? m_AssetBundle.m_Name : m_AssetBundle.m_AssetBundleName;

                        foreach (var m_Container in m_AssetBundle.m_Container)
                        {
                            var preloadIndex = m_Container.Value.preloadIndex;
                            var preloadSize = isStreamedSceneAssetBundle ? preloadTable.Length : m_Container.Value.preloadSize;
                            var preloadEnd = preloadIndex + preloadSize;
                            for (var k = preloadIndex; k < preloadEnd; k++)
                            {
                                var pptr = preloadTable[k];
                                if (pptr.TryGet(out var obj))
                                {
                                    containers[obj] = m_Container.Key;
                                }
                            }
                        }
                        break;
                    case ResourceManager m_ResourceManager:
                        foreach (var m_Container in m_ResourceManager.m_Container)
                        {
                            if (m_Container.Value.TryGet(out var obj))
                            {
                                containers[obj] = m_Container.Key;
                            }
                        }
                        break;
                    case Texture2D m_Texture2D:
                        if (!string.IsNullOrEmpty(m_Texture2D.m_StreamData?.path))
                            assetItem.FullSize = asset.byteSize + m_Texture2D.m_StreamData.size;
                        assetItem.Text = m_Texture2D.m_Name;
                        break;
                    case Texture2DArray m_Texture2DArray:
                        if (!string.IsNullOrEmpty(m_Texture2DArray.m_StreamData?.path))
                            assetItem.FullSize = asset.byteSize + m_Texture2DArray.m_StreamData.size;
                        assetItem.Text = m_Texture2DArray.m_Name;
                        tex2dArrayAssetList.Add(assetItem);
                        break;
                    case AudioClip m_AudioClip:
                        if (!string.IsNullOrEmpty(m_AudioClip.m_Source))
                            assetItem.FullSize = asset.byteSize + m_AudioClip.m_Size;
                        assetItem.Text = m_AudioClip.m_Name;
                        break;
                    case VideoClip m_VideoClip:
                        if (!string.IsNullOrEmpty(m_VideoClip.m_OriginalPath))
                            assetItem.FullSize = asset.byteSize + m_VideoClip.m_ExternalResources.m_Size;
                        assetItem.Text = m_VideoClip.m_Name;
                        break;
                    case Shader m_Shader:
                        assetItem.Text = m_Shader.m_ParsedForm?.m_Name ?? m_Shader.m_Name;
                        break;
                    case MonoBehaviour m_MonoBehaviour:
                        var assetName = m_MonoBehaviour.m_Name;
                        if (m_MonoBehaviour.m_Script.TryGet(out var m_Script))
                        {
                            assetName = assetName == "" ? m_Script.m_ClassName : assetName;
                        }
                        assetItem.Text = assetName;
                        break;
                    case GameObject m_GameObject:
                        assetItem.Text = m_GameObject.m_Name;
                        break;
                    case Animator m_Animator:
                        if (m_Animator.m_GameObject.TryGet(out var gameObject))
                        {
                            assetItem.Text = gameObject.m_Name;
                        }
                        break;
                    case NamedObject m_NamedObject:
                        assetItem.Text = m_NamedObject.m_Name;
                        break;
                }
                if (string.IsNullOrEmpty(assetItem.Text))
                {
                    assetItem.Text = assetItem.TypeString + assetItem.UniqueID;
                }

                fileAssetsList.Add(assetItem);

                //asset.Name = assetItem.Text;
                Progress.Report(++i, objectCount);
            }

            foreach (var tex2dAssetItem in tex2dArrayAssetList)
            {
                var m_Texture2DArray = (Texture2DArray)tex2dAssetItem.Asset;
                for (var layer = 0; layer < m_Texture2DArray.m_Depth; layer++)
                {
                    var fakeObj = new Texture2D(m_Texture2DArray, layer);
                    m_Texture2DArray.TextureList.Add(fakeObj);
                }
            }
            parsedAssetsList.AddRange(fileAssetsList);
            fileAssetsList.Clear();
            tex2dArrayAssetList.Clear();
        }

        var log = $"Finished loading {assetsManager.assetsFileList.Count} files with {parsedAssetsList.Count} exportable assets";
        var unityVer = assetsManager.assetsFileList[0].version;
        long m_ObjectsCount;
        if (unityVer > 2020)
        {
            m_ObjectsCount = assetsManager.assetsFileList.Sum(x => x.m_Objects.LongCount(y =>
                y.classID != (int)ClassIDType.Shader)
            );
        }
        else
        {
            m_ObjectsCount = assetsManager.assetsFileList.Sum(x => x.m_Objects.LongCount());
        }
        var objectsCount = assetsManager.assetsFileList.Sum(x => x.Objects.LongCount());
        if (m_ObjectsCount != objectsCount)
        {
            log += $" and {m_ObjectsCount - objectsCount} assets failed to read";
        }
        Logger.Info(log);

        return parsedAssetsList;
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
