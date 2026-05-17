using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class GrassPainterTool : EditorWindow
{
    float _brushRadius = 0.8f;
    float _density = 15f;
    bool _tallGrass = false;
    bool _eraseMode = false;
    LayerMask _paintLayer = ~0;

    GrassRenderer _target;

    [MenuItem("Tools/Grass Painter")]
    static void Open() => GetWindow<GrassPainterTool>("Grass Painter");

    void OnEnable() => SceneView.duringSceneGui += OnSceneGUI;
    void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

    void OnGUI()
    {
        GUILayout.Label("Grass Painter", EditorStyles.boldLabel);

        _target = (GrassRenderer)EditorGUILayout.ObjectField(
            "Target Renderer", _target, typeof(GrassRenderer), true);

        EditorGUILayout.Space();
        _brushRadius = EditorGUILayout.Slider("Brush Radius", _brushRadius, 0.1f, 10f);
        _density = EditorGUILayout.Slider("Density (m²)", _density, 1f, 200f);
        _tallGrass = EditorGUILayout.Toggle("Tall Grass", _tallGrass);
        _eraseMode = EditorGUILayout.Toggle("Erase Mode", _eraseMode);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            _eraseMode
            ? "LMB: Borrar briznas en el radio del pincel"
            : "LMB + arrastrar: Pintar hierba sobre cualquier collider",
            MessageType.Info);

        if (_target != null && GUILayout.Button("Limpiar todo"))
        {
            Undo.RecordObject(_target, "Clear Grass");
            _target.points.Clear();
            _target.RebuildMesh();
        }

        EditorGUILayout.LabelField("Briznas totales:",
            _target != null ? _target.points.Count.ToString() : "—");
    }

    void OnSceneGUI(SceneView sv)
    {
        if (_target == null) return;

        Event e = Event.current;
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        bool hit = Physics.Raycast(ray, out RaycastHit rh, 500f, _paintLayer);

        if (hit)
        {
            Handles.color = _eraseMode
                ? new Color(1f, 0.2f, 0.2f, 0.8f)
                : new Color(0.2f, 1f, 0.4f, 0.8f);
            Handles.DrawWireDisc(rh.point, rh.normal, _brushRadius);
            sv.Repaint();
        }

        bool painting = (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
                     && e.button == 0 && !e.alt;

        if (painting && hit)
        {
            Undo.RecordObject(_target, _eraseMode ? "Erase Grass" : "Paint Grass");

            if (_eraseMode)
                EraseInRadius(rh.point);
            else
                PaintInRadius(rh.point, rh.normal);

            _target.RebuildMesh();
            e.Use();
        }
    }

    void PaintInRadius(Vector3 center, Vector3 normal)
    {
        // Calcula cuántas briznas añadir según densidad y área del pincel
        float area = Mathf.PI * _brushRadius * _brushRadius;
        int countToAdd = Mathf.Max(1, Mathf.RoundToInt(_density * area * 0.05f));

        // Construir base ortonormal para distribuir puntos en el disco
        Vector3 tangent = Vector3.Cross(normal,
            Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 bitangent = Vector3.Cross(normal, tangent);

        for (int i = 0; i < countToAdd; i++)
        {
            // Distribución uniforme dentro del círculo
            float angle = Random.value * Mathf.PI * 2f;
            float r = _brushRadius * Mathf.Sqrt(Random.value);

            Vector3 offset = (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * r;
            Vector3 pos = center + offset;

            // Evitar briznas demasiado juntas
            if (TooClose(pos, 0.05f)) continue;

            _target.points.Add(new GrassPoint
            {
                position = pos,   // ? siempre world space; el builder hace la conversión
                normal = normal,
                height = (_tallGrass
                             ? _target.defaultTallHeight
                             : _target.defaultShortHeight) * Random.Range(0.8f, 1.2f),
                width = _target.defaultWidth * Random.Range(0.7f, 1.3f),
                randomSeed = Random.value * Mathf.PI * 2f
            });
        }
    }

    void EraseInRadius(Vector3 center)
    {
        float r2 = _brushRadius * _brushRadius;
        _target.points.RemoveAll(p => (p.position - center).sqrMagnitude < r2);
    }

    bool TooClose(Vector3 pos, float minDist)
    {
        float d2 = minDist * minDist;
        foreach (var p in _target.points)
            if ((p.position - pos).sqrMagnitude < d2) return true;
        return false;
    }
}