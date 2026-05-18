using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class GrassPainterTool : EditorWindow
{
    float _brushRadius = 0.8f;
    float _density = 15f;
    float _grassHeight = 0.3f;   // ? altura personalizada en vez de bool _tallGrass
    bool _eraseMode = false;
    Color _paintTint = Color.green;
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
        _density = EditorGUILayout.Slider("Density (m²)", _density, 1f, 60f);
        _eraseMode = EditorGUILayout.Toggle("Erase Mode", _eraseMode);

        EditorGUILayout.Space();

        // ? Slider de altura con botones de preset rápido
        GUILayout.Label("Altura de brizna", EditorStyles.boldLabel);

        // Muestra el valor exacto junto al slider
        EditorGUILayout.BeginHorizontal();
        _grassHeight = EditorGUILayout.Slider(_grassHeight, 0.02f, 2f);
        EditorGUILayout.LabelField($"{_grassHeight:F2} m", GUILayout.Width(50f));
        EditorGUILayout.EndHorizontal();

        // Presets de altura rápidos
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Muy corta\n0.05")) _grassHeight = 0.05f;
        if (GUILayout.Button("Corta\n0.12")) _grassHeight = 0.12f;
        if (GUILayout.Button("Media\n0.30")) _grassHeight = 0.30f;
        if (GUILayout.Button("Alta\n0.50")) _grassHeight = 0.50f;
        if (GUILayout.Button("Muy alta\n0.90")) _grassHeight = 0.90f;
        EditorGUILayout.EndHorizontal();

        // ? Referencia visual: muestra los valores del GrassRenderer si está asignado
        if (_target != null)
        {
            EditorGUILayout.HelpBox(
                $"shortGrassThreshold del renderer: {_target.shortGrassThreshold:F2} m  " +
                $"— briznas <= {_target.shortGrassThreshold:F2} m NO serán pastoreadas.",
                MessageType.None);
        }

        EditorGUILayout.Space();

        // Tinte de zona
        GUILayout.Label("Tinte de zona", EditorStyles.boldLabel);
        _paintTint = EditorGUILayout.ColorField("Color", _paintTint);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("?? Fresco")) _paintTint = new Color(0.2f, 0.75f, 0.2f);
        if (GUILayout.Button("?? Seco")) _paintTint = new Color(0.75f, 0.65f, 0.1f);
        if (GUILayout.Button("?? Otoño")) _paintTint = new Color(0.7f, 0.4f, 0.05f);
        if (GUILayout.Button("?? Pálido")) _paintTint = new Color(0.8f, 0.9f, 0.75f);
        EditorGUILayout.EndHorizontal();

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

        EditorGUILayout.Space();

        if (_target != null)
        {
            int shortCells = 0;
            int grazeable = 0;

            if (_target.logicCells != null)
            {
                foreach (var c in _target.logicCells)
                {
                    if (c.averageBaseHeight <= _target.shortGrassThreshold) shortCells++;
                    else grazeable++;
                }
            }

            EditorGUILayout.LabelField("Celdas pastoreables:", grazeable.ToString());
            EditorGUILayout.LabelField("Celdas hierba corta (no comen):", shortCells.ToString());

            EditorGUILayout.Space();
            if (GUILayout.Button("? Regenerar Celdas Lógicas", GUILayout.Height(30)))
            {
                Undo.RecordObject(_target, "Rebuild Clusters");
                _target.RebuildAll();
                EditorUtility.SetDirty(_target);
            }

            EditorGUILayout.HelpBox(
                $"Cluster radius: {_target.clusterRadius}m  |  " +
                $"Min briznas/celda: {_target.minBladesPerCell}  |  " +
                $"Short grass threshold: {_target.shortGrassThreshold}",
                MessageType.None);
        }
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
                : new Color(_paintTint.r, _paintTint.g, _paintTint.b, 0.8f);
            Handles.DrawWireDisc(rh.point, rh.normal, _brushRadius);
            sv.Repaint();
        }

        bool painting = (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
                     && e.button == 0 && !e.alt;

        if (painting && hit)
        {
            Undo.RecordObject(_target, _eraseMode ? "Erase Grass" : "Paint Grass");

            if (_eraseMode) EraseInRadius(rh.point);
            else PaintInRadius(rh.point, rh.normal);

            _target.RebuildMesh();
            e.Use();
        }
    }

    void PaintInRadius(Vector3 center, Vector3 normal)
    {
        float area = Mathf.PI * _brushRadius * _brushRadius;
        int countToAdd = Mathf.Max(1, Mathf.RoundToInt(_density * area * 0.05f));

        Vector3 tangent = Vector3.Cross(normal,
            Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 bitangent = Vector3.Cross(normal, tangent);

        for (int i = 0; i < countToAdd; i++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float r = _brushRadius * Mathf.Sqrt(Random.value);

            Vector3 offset = (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * r;
            Vector3 pos = center + offset;

            if (TooClose(pos, 0.05f)) continue;

            _target.points.Add(new GrassPoint
            {
                position = pos,
                normal = normal,
                // ? Usa _grassHeight directamente con variación aleatoria de ±20%
                height = _grassHeight * Random.Range(0.8f, 1.2f),
                width = _target.defaultWidth * Random.Range(0.7f, 1.3f),
                randomSeed = Random.value * Mathf.PI * 2f,
                tint = new Color(
                    Mathf.Clamp01(_paintTint.r * Random.Range(0.9f, 1.1f)),
                    Mathf.Clamp01(_paintTint.g * Random.Range(0.9f, 1.1f)),
                    Mathf.Clamp01(_paintTint.b * Random.Range(0.9f, 1.1f)),
                    1f)
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