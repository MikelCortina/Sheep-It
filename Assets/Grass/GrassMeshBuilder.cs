using System.Collections.Generic;
using UnityEngine;

public static class GrassMeshBuilder
{
    const int SEGMENTS = 4;

    public static Mesh Build(List<GrassPoint> points, Transform origin)
    {
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var uv2s = new List<Vector2>(); // tinte RG
        var uv3s = new List<Vector4>(); // tinte B + world XZ para noise
        var triangles = new List<int>();
        var colors = new List<Color>();

        foreach (var p in points)
        {
            int baseIdx = vertices.Count;

            Vector3 up = p.normal.normalized;
            Vector3 right = Vector3.Cross(up, new Vector3(
                Mathf.Sin(p.randomSeed), 0f, Mathf.Cos(p.randomSeed))).normalized;

            if (right.sqrMagnitude < 0.001f)
                right = Vector3.Cross(up, Vector3.forward).normalized;

            Vector3 forward = Vector3.Cross(right, up).normalized;
            float halfW = p.width * 0.5f;

            for (int s = 0; s <= SEGMENTS; s++)
            {
                float t = s / (float)SEGMENTS;
                float yOff = t * p.height;
                float zCurve = t * t * p.height * 0.2f;

                Vector3 worldCenter = p.position + up * yOff + forward * zCurve;
                Vector3 localCenter = origin.InverseTransformPoint(worldCenter);
                Vector3 localRight = origin.InverseTransformDirection(right);

                float bellCurve = Mathf.Sin(t * Mathf.PI);
                float w = Mathf.Lerp(halfW, 0.02f, t) + halfW * 0.3f * bellCurve;

                vertices.Add(localCenter - localRight * w);
                vertices.Add(localCenter + localRight * w);

                float windMask = t;
                colors.Add(new Color(windMask, p.randomSeed / (Mathf.PI * 2f), p.height, 1f));
                colors.Add(new Color(windMask, p.randomSeed / (Mathf.PI * 2f), p.height, 1f));

                uvs.Add(new Vector2(0f, t));
                uvs.Add(new Vector2(1f, t));

                // ? UV2 = tinte RG
                uv2s.Add(new Vector2(p.tint.r, p.tint.g));
                uv2s.Add(new Vector2(p.tint.r, p.tint.g));

                // ? UV3 = tinte B (xy) + world XZ para noise (zw)
                uv3s.Add(new Vector4(p.tint.b, 0f, p.position.x, p.position.z));
                uv3s.Add(new Vector4(p.tint.b, 0f, p.position.x, p.position.z));
            }

            for (int s = 0; s < SEGMENTS; s++)
            {
                int i = baseIdx + s * 2;
                triangles.Add(i); triangles.Add(i + 3); triangles.Add(i + 1);
                triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3);
            }
        }

        var mesh = new Mesh { name = "GrassMesh" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetUVs(1, uv2s);
        mesh.SetUVs(2, uv3s);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}