using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class DepthDrumUVTool
{
    private const string OutputFolder =
        "Assets/Generated/DepthDrums";

    [MenuItem("Tools/Submarine/生成选中滚筒的数字UV")]
    private static void Generate()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected == null)
        {
            Debug.LogError("请先在层级里选中一个滚筒。");
            return;
        }

        MeshFilter filter = selected.GetComponent<MeshFilter>();
        MeshRenderer renderer = selected.GetComponent<MeshRenderer>();

        if (filter == null || renderer == null ||
            filter.sharedMesh == null)
        {
            Debug.LogError("选中的物体必须有 Mesh Filter 和 Mesh Renderer。");
            return;
        }

        Mesh source = filter.sharedMesh;

        if (!source.isReadable)
        {
            Debug.LogError("请先在原始模型导入设置中开启读取/写入。");
            return;
        }

        Material originalMaterial = renderer.sharedMaterial;

        if (originalMaterial == null ||
            !originalMaterial.HasProperty("_BaseMap"))
        {
            Debug.LogError("请先给滚筒使用 M_DepthDrum 材质。");
            return;
        }

        Texture originalTexture =
            originalMaterial.GetTexture("_BaseMap");

        string texturePath =
            AssetDatabase.GetAssetPath(originalTexture);

        if (string.IsNullOrEmpty(texturePath) ||
            !File.Exists(texturePath))
        {
            Debug.LogError("请确认材质的基础贴图已连接数字PNG。");
            return;
        }

        // 从原PNG读取，避免导入压缩或缩放影响数字排列。
        Texture2D horizontal = new Texture2D(
            2, 2, TextureFormat.RGBA32, false
        );

        if (!horizontal.LoadImage(File.ReadAllBytes(texturePath)))
        {
            Object.DestroyImmediate(horizontal);
            Debug.LogError("无法读取数字图片。");
            return;
        }

        if (horizontal.width % 10 != 0 ||
            horizontal.width <= horizontal.height)
        {
            Object.DestroyImmediate(horizontal);
            Debug.LogError("需要之前生成的横排0–9数字条。");
            return;
        }

        Directory.CreateDirectory(OutputFolder);
        AssetDatabase.Refresh();

        string prefix = OutputFolder + "/" + selected.name;

        string imagePath = AssetDatabase.GenerateUniqueAssetPath(
            prefix + "_Digits.png"
        );

        // 把横排数字改成竖排，匹配横向滚筒的圆柱UV。
        int cellWidth = horizontal.width / 10;
        int cellHeight = horizontal.height;

        Color32[] sourcePixels = horizontal.GetPixels32();
        Color32[] destinationPixels =
            new Color32[cellWidth * cellHeight * 10];

        for (int digit = 0; digit < 10; digit++)
        {
            for (int y = 0; y < cellHeight; y++)
            {
                for (int x = 0; x < cellWidth; x++)
                {
                    int sourceIndex =
                        y * horizontal.width +
                        digit * cellWidth + x;

                    int destinationIndex =
                        (digit * cellHeight + y) * cellWidth + x;

                    destinationPixels[destinationIndex] =
                        sourcePixels[sourceIndex];
                }
            }
        }

        Texture2D vertical = new Texture2D(
            cellWidth,
            cellHeight * 10,
            TextureFormat.RGBA32,
            false
        );

        vertical.SetPixels32(destinationPixels);
        vertical.Apply();

        File.WriteAllBytes(imagePath, vertical.EncodeToPNG());

        Object.DestroyImmediate(horizontal);
        Object.DestroyImmediate(vertical);

        AssetDatabase.ImportAsset(imagePath);

        TextureImporter importer =
            AssetImporter.GetAtPath(imagePath) as TextureImporter;

        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression =
                TextureImporterCompression.Uncompressed;

            importer.SaveAndReimport();
        }

        Mesh generatedMesh = BuildCylinderMesh(source);
        generatedMesh.name = selected.name + "_DigitUV";

        string meshPath = AssetDatabase.GenerateUniqueAssetPath(
            prefix + "_DigitUV.asset"
        );

        AssetDatabase.CreateAsset(generatedMesh, meshPath);

        Material generatedMaterial = new Material(originalMaterial);
        generatedMaterial.name = selected.name + "_Digits";

        Texture2D digitTexture =
            AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);

        generatedMaterial.SetTexture("_BaseMap", digitTexture);
        generatedMaterial.SetTextureScale("_BaseMap", Vector2.one);
        generatedMaterial.SetTextureOffset("_BaseMap", Vector2.zero);
        generatedMaterial.SetColor("_BaseColor", Color.white);

        string materialPath = AssetDatabase.GenerateUniqueAssetPath(
            prefix + "_Digits.mat"
        );

        AssetDatabase.CreateAsset(generatedMaterial, materialPath);

        Undo.RecordObject(filter, "生成滚筒数字UV");
        Undo.RecordObject(renderer, "连接滚筒数字材质");

        filter.sharedMesh = generatedMesh;

        Material[] materials = renderer.sharedMaterials;

        if (materials.Length == 0)
            materials = new Material[1];

        materials[0] = generatedMaterial;
        renderer.sharedMaterials = materials;

        PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);

        EditorUtility.SetDirty(filter);
        EditorUtility.SetDirty(renderer);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            selected.scene
        );

        AssetDatabase.SaveAssets();

        Debug.Log(
            "滚筒数字UV已生成。请查看数字方向，并保存场景。",
            selected
        );
    }

    private static Mesh BuildCylinderMesh(Mesh source)
    {
        Vector3[] sourceVertices = source.vertices;
        Vector3[] sourceNormals = source.normals;

        bool hasNormals =
            sourceNormals.Length == sourceVertices.Length;

        Bounds bounds = source.bounds;
        Vector3 center = bounds.center;

        float width = Mathf.Max(0.0001f, bounds.size.x);

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();

        List<int[]> submeshTriangles = new List<int[]>();

        for (int submesh = 0;
            submesh < source.subMeshCount;
            submesh++)
        {
            int[] triangles = source.GetTriangles(submesh);
            List<int> newTriangles = new List<int>();

            for (int triangle = 0;
                triangle < triangles.Length;
                triangle += 3)
            {
                Vector3 a = sourceVertices[triangles[triangle]];
                Vector3 b = sourceVertices[triangles[triangle + 1]];
                Vector3 c = sourceVertices[triangles[triangle + 2]];

                Vector3 faceNormal =
                    Vector3.Cross(b - a, c - a).normalized;

                // 两端盖不显示数字。
                bool endCap =
                    Mathf.Abs(faceNormal.x) > 0.8f;

                Vector2[] triangleUV = new Vector2[3];

                for (int corner = 0; corner < 3; corner++)
                {
                    int originalIndex = triangles[triangle + corner];
                    Vector3 position = sourceVertices[originalIndex];

                    float u = (position.x - bounds.min.x) / width;

                    float angle = Mathf.Atan2(
                        position.y - center.y,
                        -(position.z - center.z)
                    );

                    float v = Mathf.Repeat(
                        angle / (Mathf.PI * 2f) + 0.05f,
                        1f
                    );

                    triangleUV[corner] = endCap
                        ? Vector2.zero
                        : new Vector2(u, v);
                }

                // 跨越贴图接缝的三角形单独展开，避免数字拉成长条。
                float minimumV = Mathf.Min(
                    triangleUV[0].y,
                    Mathf.Min(triangleUV[1].y, triangleUV[2].y)
                );

                float maximumV = Mathf.Max(
                    triangleUV[0].y,
                    Mathf.Max(triangleUV[1].y, triangleUV[2].y)
                );

                if (!endCap && maximumV - minimumV > 0.5f)
                {
                    for (int corner = 0; corner < 3; corner++)
                    {
                        if (triangleUV[corner].y < 0.5f)
                            triangleUV[corner].y += 1f;
                    }
                }

                for (int corner = 0; corner < 3; corner++)
                {
                    int originalIndex = triangles[triangle + corner];

                    newTriangles.Add(vertices.Count);
                    vertices.Add(sourceVertices[originalIndex]);
                    uv.Add(triangleUV[corner]);

                    normals.Add(
                        hasNormals
                            ? sourceNormals[originalIndex]
                            : faceNormal
                    );
                }
            }

            submeshTriangles.Add(newTriangles.ToArray());
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt32;

        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uv);

        mesh.subMeshCount = submeshTriangles.Count;

        for (int i = 0; i < submeshTriangles.Count; i++)
            mesh.SetTriangles(submeshTriangles[i], i);

        mesh.RecalculateBounds();
        mesh.RecalculateTangents();

        return mesh;
    }
}