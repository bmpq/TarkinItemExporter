using AssetStudio;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public static class Studio
{
    public static List<AssetItem> parsedAssetsList = new List<AssetItem>();
    private static Dictionary<AssetStudio.Object, string> containers = new Dictionary<AssetStudio.Object, string>();
    public static AssetsManager assetsManager;
    public static List<BaseNode> gameObjectTree = new List<BaseNode>();

    static Studio()
    {
        Logger.Default = new BepinexLogger();
    }

    public static List<AssetItem> LoadAssets(string bundlePath)
    {
        Logger.Info("Attempting to load: " + bundlePath);

        parsedAssetsList.Clear();
        containers.Clear();
        gameObjectTree.Clear();

        assetsManager = new AssetsManager();

        assetsManager.SpecifyUnityVersion = new UnityVersion(UnityEngine.Application.unityVersion);
        assetsManager.LoadFilesAndFolders(bundlePath);

        ParseAssets();

        return parsedAssetsList;
    }

    public static void ParseAssets()
    {
        Logger.Info("Parse assets...");

        var fileAssetsList = new List<AssetItem>();
        var tex2dArrayAssetList = new List<AssetItem>();
        var objectCount = assetsManager.assetsFileList.Sum(x => x.Objects.Count);
        var objectAssetItemDic = new Dictionary<AssetStudio.Object, AssetItem>(objectCount);

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

        BuildTreeStructure(objectAssetItemDic);

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
    }


    public static void BuildTreeStructure(Dictionary<AssetStudio.Object, AssetItem> objectAssetItemDic)
    {
        Logger.Info("Building tree structure...");

        var treeNodeDictionary = new Dictionary<GameObject, GameObjectNode>();
        var assetsFileCount = assetsManager.assetsFileList.Count;
        int j = 0;
        Progress.Reset();
        foreach (var assetsFile in assetsManager.assetsFileList)
        {
            var fileNode = new BaseNode(assetsFile.fileName);  //RootNode

            foreach (var obj in assetsFile.Objects)
            {
                if (obj is GameObject m_GameObject)
                {
                    if (!treeNodeDictionary.TryGetValue(m_GameObject, out var currentNode))
                    {
                        currentNode = new GameObjectNode(m_GameObject);
                        treeNodeDictionary.Add(m_GameObject, currentNode);
                    }

                    foreach (var pptr in m_GameObject.m_Components)
                    {
                        if (pptr.TryGet(out var m_Component))
                        {
                            objectAssetItemDic[m_Component].Node = currentNode;
                            if (m_Component is MeshFilter m_MeshFilter)
                            {
                                if (m_MeshFilter.m_Mesh.TryGet(out var m_Mesh))
                                {
                                    objectAssetItemDic[m_Mesh].Node = currentNode;
                                }
                            }
                            else if (m_Component is SkinnedMeshRenderer m_SkinnedMeshRenderer)
                            {
                                if (m_SkinnedMeshRenderer.m_Mesh.TryGet(out var m_Mesh))
                                {
                                    objectAssetItemDic[m_Mesh].Node = currentNode;
                                }
                            }
                        }
                    }

                    var parentNode = fileNode;
                    if (m_GameObject.m_Transform != null)
                    {
                        if (m_GameObject.m_Transform.m_Father.TryGet(out var m_Father))
                        {
                            if (m_Father.m_GameObject.TryGet(out var parentGameObject))
                            {
                                if (!treeNodeDictionary.TryGetValue(parentGameObject, out var parentGameObjectNode))
                                {
                                    parentGameObjectNode = new GameObjectNode(parentGameObject);
                                    treeNodeDictionary.Add(parentGameObject, parentGameObjectNode);
                                }
                                parentNode = parentGameObjectNode;
                            }
                        }
                    }
                    parentNode.nodes.Add(currentNode);
                }
            }

            if (fileNode.nodes.Count > 0)
            {
                GenerateFullPath(fileNode, fileNode.Text);
                gameObjectTree.Add(fileNode);
            }

            Progress.Report(++j, assetsFileCount);
        }

        treeNodeDictionary.Clear();
        objectAssetItemDic.Clear();
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
