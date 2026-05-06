#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PastureZone))]
public class PasturePainter : Editor
{
    [Header("Brush")]
    private float _brushRadius = 3f;
    private float _brushDensity = 1.5f;   // distancia mínima entre celdas
    private float _brushStrength = 1f;     // 0–1, probabilidad de instanciar
    private bool _paintMode = false;
    private bool _eraseMode = false;
    private int _selectedPrefab = 0;

    private PastureZone _zone;

    void OnEnable() => _zone = (PastureZone)target;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("?? Pasture Painter ??", EditorStyles.boldLabel);

        _brushRadius = EditorGUILayout.Slider("Brush Radius", _brushRadius, 0.5f, 20f);
        _brushDensity = EditorGUILayout.Slider("Cell Spacing", _brushDensity, 0.5f, 5f);
        _brushStrength = EditorGUILayout.Slider("Brush Strength", _brushStrength, 0.1f, 1f);

        // Selector de prefab
        if (_zone.grassPrefabs != null && _zone.grassPrefabs.Length > 0)
        {
            string[] names = new string[_zone.grassPrefabs.Length];
            for (int i = 0; i < names.Length; i++)
                names[i] = _zone.grassPrefabs[i] != null ? _zone.grassPrefabs[i].name : "null";
            _selectedPrefab = EditorGUILayout.Popup("Grass Prefab", _selectedPrefab, names);
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = _paintMode ? Color.green : Color.white;
        if (GUILayout.Button(_paintMode ? "? Painting" : "? Start Paint")) _paintMode = !_paintMode;
        GUI.backgroundColor = _eraseMode ? Color.red : Color.white;
        if (GUILayout.Button(_eraseMode ? "? Erasing" : "? Start Erase")) _eraseMode = !_eraseMode;
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Clear All Cells"))
        {
            if (EditorUtility.DisplayDialog("Clear Pasture", "¿Eliminar todas las celdas?", "Sí", "No"))
                ClearAll();
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"Cells: {_zone.cells.Count}", EditorStyles.miniLabel);
    }

    void OnSceneGUI()
    {
        if (!_paintMode && !_eraseMode) return;

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        // Dibujar pincel
        Handles.color = _eraseMode
            ? new Color(1f, 0.2f, 0.2f, 0.35f)
            : new Color(0.2f, 1f, 0.2f, 0.35f);
        Handles.DrawSolidDisc(hit.point, hit.normal, _brushRadius);
        Handles.color = _eraseMode ? Color.red : Color.green;
        Handles.DrawWireDisc(hit.point, hit.normal, _brushRadius);

        if ((Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseDrag)
            && Event.current.button == 0)
        {
            if (_eraseMode) EraseAt(hit.point);
            else PaintAt(hit.point);
            Event.current.Use();
        }

        SceneView.RepaintAll();
    }

    void PaintAt(Vector3 center)
    {
        if (_zone.grassPrefabs == null || _zone.grassPrefabs.Length == 0) return;
        GameObject prefab = _zone.grassPrefabs[_selectedPrefab];
        if (prefab == null) return;

        // Grid de candidatos con jitter para forma orgánica
        for (float x = -_brushRadius; x <= _brushRadius; x += _brushDensity)
        {
            for (float z = -_brushRadius; z <= _brushRadius; z += _brushDensity)
            {
                Vector2 jitter = Random.insideUnitCircle * (_brushDensity * 0.45f);
                Vector3 candidate = center + new Vector3(x + jitter.x, 0f, z + jitter.y);

                if (Vector3.Distance(candidate, center) > _brushRadius) continue;
                if (Random.value > _brushStrength) continue;
                if (TooClose(candidate)) continue;

                // Raycast para apoyar en terreno real
                if (Physics.Raycast(candidate + Vector3.up * 5f, Vector3.down, out RaycastHit th, 10f))
                    candidate = th.point;

                Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _zone.transform);
                go.transform.SetPositionAndRotation(candidate, rot);
                go.transform.localScale = Vector3.one * Random.Range(0.8f, 1.2f);

                Undo.RegisterCreatedObjectUndo(go, "Paint Grass");

                var cell = go.GetComponent<GrassCell>();
                if (cell == null) cell = go.AddComponent<GrassCell>();
                _zone.cells.Add(cell);
                EditorUtility.SetDirty(_zone);
            }
        }
    }

    void EraseAt(Vector3 center)
    {
        for (int i = _zone.cells.Count - 1; i >= 0; i--)
        {
            var cell = _zone.cells[i];
            if (cell == null) { _zone.cells.RemoveAt(i); continue; }
            if (Vector3.Distance(cell.transform.position, center) <= _brushRadius)
            {
                Undo.DestroyObjectImmediate(cell.gameObject);
                _zone.cells.RemoveAt(i);
                EditorUtility.SetDirty(_zone);
            }
        }
    }

    void ClearAll()
    {
        foreach (var cell in _zone.cells)
            if (cell != null) Undo.DestroyObjectImmediate(cell.gameObject);
        _zone.cells.Clear();
        EditorUtility.SetDirty(_zone);
    }

    bool TooClose(Vector3 pos)
    {
        foreach (var c in _zone.cells)
            if (c != null && Vector3.Distance(c.transform.position, pos) < _brushDensity * 0.85f)
                return true;
        return false;
    }
}
#endif