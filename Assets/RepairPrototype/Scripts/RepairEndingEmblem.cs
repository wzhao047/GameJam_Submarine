using UnityEngine;
using UnityEngine.UI;

/// <summary>Resolution-independent fallback artwork for arrival, hull, oxygen and flooding endings.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class RepairEndingEmblem : MaskableGraphic
{
    public int Ending { get; set; }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        const int segments = 64;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            float b = (i + 1) * Mathf.PI * 2f / segments;
            Line(mesh, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.46f,
                new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 0.46f, 0.012f);
        }
        if (Ending == 0)
        {
            // Anchor: the crew made port.
            Line(mesh, new Vector2(0, 0.2f), new Vector2(0, -0.25f));
            Line(mesh, new Vector2(-0.16f, 0.08f), new Vector2(0.16f, 0.08f));
            Line(mesh, new Vector2(-0.25f, -0.05f), new Vector2(-0.19f, -0.2f));
            Line(mesh, new Vector2(-0.19f, -0.2f), new Vector2(0, -0.28f));
            Line(mesh, new Vector2(0, -0.28f), new Vector2(0.19f, -0.2f));
            Line(mesh, new Vector2(0.19f, -0.2f), new Vector2(0.25f, -0.05f));
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI * 2f / 24;
                float b = (i + 1) * Mathf.PI * 2f / 24;
                Line(mesh, new Vector2(Mathf.Cos(a) * 0.065f, 0.24f + Mathf.Sin(a) * 0.065f),
                    new Vector2(Mathf.Cos(b) * 0.065f, 0.24f + Mathf.Sin(b) * 0.065f), 0.018f);
            }
        }
        else if (Ending == 1)
        {
            Line(mesh, new Vector2(-0.26f, 0.2f), new Vector2(-0.2f, -0.2f));
            Line(mesh, new Vector2(-0.2f, -0.2f), new Vector2(0.2f, -0.2f));
            Line(mesh, new Vector2(0.2f, -0.2f), new Vector2(0.26f, 0.2f));
            Line(mesh, new Vector2(-0.26f, 0.2f), new Vector2(0.26f, 0.2f));
            Line(mesh, new Vector2(0.06f, 0.28f), new Vector2(-0.07f, 0.04f));
            Line(mesh, new Vector2(-0.07f, 0.04f), new Vector2(0.06f, -0.02f));
            Line(mesh, new Vector2(0.06f, -0.02f), new Vector2(-0.05f, -0.28f));
        }
        else if (Ending == 2)
        {
            Line(mesh, new Vector2(0, 0.25f), new Vector2(0, 0.07f));
            Line(mesh, new Vector2(0, 0.07f), new Vector2(-0.14f, -0.02f));
            Line(mesh, new Vector2(0, 0.07f), new Vector2(0.14f, -0.02f));
            Line(mesh, new Vector2(-0.14f, 0.18f), new Vector2(-0.26f, -0.19f));
            Line(mesh, new Vector2(-0.26f, -0.19f), new Vector2(-0.06f, -0.23f));
            Line(mesh, new Vector2(-0.06f, -0.23f), new Vector2(-0.06f, -0.04f));
            Line(mesh, new Vector2(0.14f, 0.18f), new Vector2(0.26f, -0.19f));
            Line(mesh, new Vector2(0.26f, -0.19f), new Vector2(0.06f, -0.23f));
            Line(mesh, new Vector2(0.06f, -0.23f), new Vector2(0.06f, -0.04f));
        }
        else
        {
            // Droplet above a rising waterline.
            Line(mesh, new Vector2(0, 0.3f), new Vector2(-0.15f, 0.04f));
            Line(mesh, new Vector2(-0.15f, 0.04f), new Vector2(-0.1f, -0.08f));
            Line(mesh, new Vector2(-0.1f, -0.08f), new Vector2(0.1f, -0.08f));
            Line(mesh, new Vector2(0.1f, -0.08f), new Vector2(0.15f, 0.04f));
            Line(mesh, new Vector2(0.15f, 0.04f), new Vector2(0, 0.3f));
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 20; i++)
                {
                    float x = -0.29f + i * 0.029f;
                    float next = x + 0.029f;
                    Line(mesh, new Vector2(x, -0.2f - row * 0.08f + Mathf.Sin(i * Mathf.PI / 5) * 0.02f),
                        new Vector2(next, -0.2f - row * 0.08f + Mathf.Sin((i + 1) * Mathf.PI / 5) * 0.02f), 0.015f);
                }
        }
    }

    private void Line(VertexHelper mesh, Vector2 start, Vector2 end, float thickness = 0.025f)
    {
        Rect rect = GetPixelAdjustedRect();
        float size = Mathf.Min(rect.width, rect.height);
        Vector2 a = rect.center + start * size;
        Vector2 b = rect.center + end * size;
        Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * thickness * size * 0.5f;
        int index = mesh.currentVertCount;
        mesh.AddVert(a - normal, color, Vector2.zero);
        mesh.AddVert(a + normal, color, Vector2.zero);
        mesh.AddVert(b + normal, color, Vector2.zero);
        mesh.AddVert(b - normal, color, Vector2.zero);
        mesh.AddTriangle(index, index + 1, index + 2);
        mesh.AddTriangle(index, index + 2, index + 3);
    }
}
