using AssetStudio;
using System.IO;
using System.Linq;
using System.Text;

public class Exporter
{
    public static bool ExportMesh(AssetItem item, string exportPath)
    {
        var m_Mesh = (Mesh)item.Asset;
        if (m_Mesh.m_VertexCount <= 0)
            return false;
        if (!TryExportFile(exportPath, item, ".obj", out var exportFullPath))
            return false;
        var sb = new StringBuilder();
        sb.AppendLine("g " + m_Mesh.m_Name);

        #region Vertices

        if (m_Mesh.m_Vertices == null || m_Mesh.m_Vertices.Length == 0)
        {
            return false;
        }

        int c = 3;
        if (m_Mesh.m_Vertices.Length == m_Mesh.m_VertexCount * 4)
        {
            c = 4;
        }

        for (int v = 0; v < m_Mesh.m_VertexCount; v++)
        {
            sb.Append($"v {-m_Mesh.m_Vertices[v * c]} {m_Mesh.m_Vertices[v * c + 1]} {m_Mesh.m_Vertices[v * c + 2]}\r\n");
        }

        #endregion

        #region UV

        if (m_Mesh.m_UV0?.Length > 0)
        {
            c = 4;
            if (m_Mesh.m_UV0.Length == m_Mesh.m_VertexCount * 2)
            {
                c = 2;
            }
            else if (m_Mesh.m_UV0.Length == m_Mesh.m_VertexCount * 3)
            {
                c = 3;
            }

            for (int v = 0; v < m_Mesh.m_VertexCount; v++)
            {
                sb.AppendFormat("vt {0} {1}\r\n", m_Mesh.m_UV0[v * c], m_Mesh.m_UV0[v * c + 1]);
            }
        }

        #endregion

        #region Normals

        if (m_Mesh.m_Normals?.Length > 0)
        {
            if (m_Mesh.m_Normals.Length == m_Mesh.m_VertexCount * 3)
            {
                c = 3;
            }
            else if (m_Mesh.m_Normals.Length == m_Mesh.m_VertexCount * 4)
            {
                c = 4;
            }

            for (int v = 0; v < m_Mesh.m_VertexCount; v++)
            {
                sb.AppendFormat("vn {0} {1} {2}\r\n", -m_Mesh.m_Normals[v * c], m_Mesh.m_Normals[v * c + 1], m_Mesh.m_Normals[v * c + 2]);
            }
        }

        #endregion

        #region Face

        int sum = 0;
        for (var i = 0; i < m_Mesh.m_SubMeshes.Length; i++)
        {
            sb.AppendLine($"g {m_Mesh.m_Name}_{i}");
            int indexCount = (int)m_Mesh.m_SubMeshes[i].indexCount;
            var end = sum + indexCount / 3;
            for (int f = sum; f < end; f++)
            {
                sb.AppendFormat("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\r\n", m_Mesh.m_Indices[f * 3 + 2] + 1, m_Mesh.m_Indices[f * 3 + 1] + 1, m_Mesh.m_Indices[f * 3] + 1);
            }

            sum = end;
        }

        #endregion

        sb.Replace("NaN", "0");
        File.WriteAllText(exportFullPath, sb.ToString());
        Logger.Debug($"{item.TypeString} \"{item.Text}\" exported to \"{exportFullPath}\"");
        return true;
    }


    private static bool TryExportFile(string dir, AssetItem item, string extension, out string fullPath, string mode = "Export")
    {
        var fileName = FixFileName(item.Text);
        fileName = $"{fileName} @{item.m_PathID}";

        fullPath = Path.Combine(dir, fileName + extension);
        if (!File.Exists(fullPath))
        {
            Directory.CreateDirectory(dir);
            return true;
        }
        if (true)
        {
            fullPath = Path.Combine(dir, fileName + item.UniqueID + extension);
            if (!File.Exists(fullPath))
            {
                Directory.CreateDirectory(dir);
                return true;
            }
        }
        Logger.Error($"{mode} error. File \"{fullPath.Color(ColorConsole.BrightRed)}\" already exist");
        return false;
    }

    public static string FixFileName(string str)
    {
        return str.Length >= 260
            ? Path.GetRandomFileName()
            : Path.GetInvalidFileNameChars().Aggregate(str, (current, c) => current.Replace(c, '_'));
    }
}
