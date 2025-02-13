using UnityEngine;
using System.Collections.Generic;

public static class UnityMeshConverter
{
    public static Mesh ConvertToUnityMesh(this AssetStudio.Mesh asMesh)
    {
        if (asMesh == null || asMesh.m_VertexCount <= 0)
        {
            Debug.LogError("AssetStudioMesh is null or has no vertices.");
            return null;
        }

        Mesh unityMesh = new Mesh();
        unityMesh.name = asMesh.m_Name; // Assuming AssetStudioMesh has a m_Name property (add if needed)

        #region Vertices
        if (asMesh.m_Vertices == null || asMesh.m_Vertices.Length == 0)
        {
            Debug.LogError("AssetStudioMesh has no vertex data.");
            return null;
        }

        Vector3[] vertices = new Vector3[asMesh.m_VertexCount];
        int vertexComponentCount = 3;
        if (asMesh.m_Vertices.Length == asMesh.m_VertexCount * 4)
        {
            vertexComponentCount = 4;
        }

        for (int v = 0; v < asMesh.m_VertexCount; v++)
        {
            // positional xyz
            vertices[v] = new Vector3(
                asMesh.m_Vertices[v * vertexComponentCount],
                asMesh.m_Vertices[v * vertexComponentCount + 1],
                asMesh.m_Vertices[v * vertexComponentCount + 2]
            );
        }
        unityMesh.vertices = vertices;
        #endregion

        #region Triangles (Indices)
        if (asMesh.m_Indices == null || asMesh.m_Indices.Count == 0)
        {
            Debug.LogError("AssetStudioMesh has no index data.");
            return null;
        }

        int totalIndexCount = asMesh.m_Indices.Count;

        if (asMesh.m_SubMeshes != null && asMesh.m_SubMeshes.Length > 0)
        {
            unityMesh.subMeshCount = asMesh.m_SubMeshes.Length;
            int sumIndices = 0; // Keep track of the starting index for each submesh

            for (int submeshIndex = 0; submeshIndex < asMesh.m_SubMeshes.Length; submeshIndex++)
            {
                AssetStudio.SubMesh subMesh = asMesh.m_SubMeshes[submeshIndex];
                int indexCount = (int)subMesh.indexCount;

                int[] submeshTriangles = new int[indexCount];

                for (int i = 0; i < indexCount; i++)
                {
                    submeshTriangles[i] = (int)asMesh.m_Indices[sumIndices + i];
                }

                unityMesh.SetTriangles(submeshTriangles, submeshIndex);
                sumIndices += indexCount;
            }
        }
        else
        {
            // If no submeshes, assume all indices are for one mesh.  Directly assign.
            int[] triangles = new int[totalIndexCount];
            for (int i = 0; i < totalIndexCount; i++)
            {
                triangles[i] = (int)asMesh.m_Indices[i];
            }
            unityMesh.triangles = triangles;
        }

        #endregion

        #region UVs
        if (asMesh.m_UV0 != null && asMesh.m_UV0.Length > 0)
        {
            Vector2[] uvs = new Vector2[asMesh.m_VertexCount];
            int uvComponentCount = 4;
            if (asMesh.m_UV0.Length == asMesh.m_VertexCount * 2) uvComponentCount = 2;
            else if (asMesh.m_UV0.Length == asMesh.m_VertexCount * 3) uvComponentCount = 3;

            for (int v = 0; v < asMesh.m_VertexCount; v++)
            {
                uvs[v] = new Vector2(
                    asMesh.m_UV0[v * uvComponentCount],
                    asMesh.m_UV0[v * uvComponentCount + 1]
                );
            }
            unityMesh.uv = uvs;
        }

        // Add support for UV1 to UV7 similarly if needed, using unityMesh.uv2, unityMesh.uv3, etc.
        if (asMesh.m_UV1 != null && asMesh.m_UV1.Length > 0) unityMesh.uv2 = ConvertFloatArrayToUV(asMesh.m_UV1, asMesh.m_VertexCount);
        if (asMesh.m_UV2 != null && asMesh.m_UV2.Length > 0) unityMesh.uv3 = ConvertFloatArrayToUV(asMesh.m_UV2, asMesh.m_VertexCount);
        if (asMesh.m_UV3 != null && asMesh.m_UV3.Length > 0) unityMesh.uv4 = ConvertFloatArrayToUV(asMesh.m_UV3, asMesh.m_VertexCount);
        if (asMesh.m_UV4 != null && asMesh.m_UV4.Length > 0) unityMesh.uv5 = ConvertFloatArrayToUV(asMesh.m_UV4, asMesh.m_VertexCount);
        if (asMesh.m_UV5 != null && asMesh.m_UV5.Length > 0) unityMesh.uv6 = ConvertFloatArrayToUV(asMesh.m_UV5, asMesh.m_VertexCount);
        if (asMesh.m_UV6 != null && asMesh.m_UV6.Length > 0) unityMesh.uv7 = ConvertFloatArrayToUV(asMesh.m_UV6, asMesh.m_VertexCount);
        if (asMesh.m_UV7 != null && asMesh.m_UV7.Length > 0) unityMesh.uv8 = ConvertFloatArrayToUV(asMesh.m_UV7, asMesh.m_VertexCount);


        #endregion

        #region Normals
        if (asMesh.m_Normals != null && asMesh.m_Normals.Length > 0)
        {
            Vector3[] normals = new Vector3[asMesh.m_VertexCount];
            int normalComponentCount = 3;
            if (asMesh.m_Normals.Length == asMesh.m_VertexCount * 4) normalComponentCount = 4;

            for (int v = 0; v < asMesh.m_VertexCount; v++)
            {
                normals[v] = new Vector3(
                    asMesh.m_Normals[v * normalComponentCount],
                    asMesh.m_Normals[v * normalComponentCount + 1],
                    asMesh.m_Normals[v * normalComponentCount + 2]
                );
            }
            unityMesh.normals = normals;
        }
        #endregion

        #region Colors
        if (asMesh.m_Colors != null && asMesh.m_Colors.Length > 0)
        {
            Color[] colors = new Color[asMesh.m_VertexCount];
            int colorComponentCount = 4; // Assuming Color is RGBA
            if (asMesh.m_Colors.Length == asMesh.m_VertexCount * 3) colorComponentCount = 3; //RGB

            for (int v = 0; v < asMesh.m_VertexCount; v++)
            {
                if (colorComponentCount == 4)
                {
                    colors[v] = new Color(
                        asMesh.m_Colors[v * colorComponentCount],
                        asMesh.m_Colors[v * colorComponentCount + 1],
                        asMesh.m_Colors[v * colorComponentCount + 2],
                        asMesh.m_Colors[v * colorComponentCount + 3]
                    );
                }
                else if (colorComponentCount == 3)
                {
                    colors[v] = new Color(
                        asMesh.m_Colors[v * colorComponentCount],
                        asMesh.m_Colors[v * colorComponentCount + 1],
                        asMesh.m_Colors[v * colorComponentCount + 2]
                    );
                }
            }
            unityMesh.colors = colors;
        }
        #endregion

        #region Tangents
        if (asMesh.m_Tangents != null && asMesh.m_Tangents.Length > 0)
        {
            Vector4[] tangents = new Vector4[asMesh.m_VertexCount];
            int tangentComponentCount = 4;
            if (asMesh.m_Tangents.Length == asMesh.m_VertexCount * 3) tangentComponentCount = 3; // Maybe not Vector4, but Vector3 + handedness?

            for (int v = 0; v < asMesh.m_VertexCount; v++)
            {
                if (tangentComponentCount == 4)
                {
                    tangents[v] = new Vector4(
                        asMesh.m_Tangents[v * tangentComponentCount],
                        asMesh.m_Tangents[v * tangentComponentCount + 1],
                        asMesh.m_Tangents[v * tangentComponentCount + 2],
                        asMesh.m_Tangents[v * tangentComponentCount + 3]
                    );
                }
                else if (tangentComponentCount == 3)
                {
                    // Assuming tangent.w is 1 or -1 for handedness, if not provided, default to 1
                    tangents[v] = new Vector4(
                       asMesh.m_Tangents[v * tangentComponentCount],
                       asMesh.m_Tangents[v * tangentComponentCount + 1],
                       asMesh.m_Tangents[v * tangentComponentCount + 2],
                       1f // Default handedness
                   );
                }
            }
            unityMesh.tangents = tangents;
        }
        #endregion

        unityMesh.RecalculateBounds();

        return unityMesh;
    }

    // Helper function to convert float array to Vector2 array for UVs
    private static Vector2[] ConvertFloatArrayToUV(float[] floatUVs, int vertexCount)
    {
        Vector2[] uvs = new Vector2[vertexCount];
        int uvComponentCount = 4;
        if (floatUVs.Length == vertexCount * 2) uvComponentCount = 2;
        else if (floatUVs.Length == vertexCount * 3) uvComponentCount = 3;

        for (int v = 0; v < vertexCount; v++)
        {
            uvs[v] = new Vector2(
                floatUVs[v * uvComponentCount],
                floatUVs[v * uvComponentCount + 1]
            );
        }
        return uvs;
    }
}