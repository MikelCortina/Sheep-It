using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GrassRenderer : MonoBehaviour
{
    // ? SerializeField para que la lista sobreviva recompilaciones
    [HideInInspector][SerializeField] public List<GrassPoint> points = new();

    public float defaultShortHeight = 0.12f;
    public float defaultTallHeight = 0.45f;
    public float defaultWidth = 0.06f;

    MeshFilter _mf;

    public void RebuildMesh()
    {
        _mf ??= GetComponent<MeshFilter>();
        _mf.sharedMesh = GrassMeshBuilder.Build(points, transform);
    }

    void OnEnable() => RebuildMesh();
}