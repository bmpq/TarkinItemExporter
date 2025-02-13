using AssetStudio;
using System.Collections.Generic;

public class AssetItem
{
    public Object Asset;
    public SerializedFile SourceFile;
    public string Container = string.Empty;
    public string TypeString;
    public long m_PathID;
    public long FullSize;
    public ClassIDType Type;
    public string Text;
    public string UniqueID;
    public GameObjectNode Node;

    public AssetItem(Object asset)
    {
        Asset = asset;
        SourceFile = asset.assetsFile;
        Type = asset.type;
        TypeString = Type.ToString();
        m_PathID = asset.m_PathID;
        FullSize = asset.byteSize;
    }
}

public class BaseNode
{
    public List<BaseNode> nodes = new List<BaseNode>();
    public string FullPath = "";
    public readonly string Text;

    public BaseNode(string name)
    {
        Text = name;
    }
}

public class GameObjectNode : BaseNode
{
    public GameObject gameObject;

    public GameObjectNode(GameObject gameObject) : base(gameObject.m_Name)
    {
        this.gameObject = gameObject;
    }
}