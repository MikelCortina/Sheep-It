using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GrassRenderer : MonoBehaviour
{
    [HideInInspector][SerializeField] public List<GrassPoint> points = new();

    [Header("Visual")]
    public float defaultShortHeight = 0.12f;
    public float defaultTallHeight = 0.45f;
    public float defaultWidth = 0.06f;

    [Header("Clustering — Lógica de pastoreo")]
    public float clusterRadius = 2f;
    public int minBladesPerCell = 3;
    [Tooltip("Briznas con altura media <= este valor se excluyen del pastoreo")]
    public float shortGrassThreshold = 0.08f;

    [Header("Pastoreo")]
    public float consumeRate = 0.08f;
    public float regenRate = 0f;
    public float minGrassToGraze = 0.05f;
    public int maxSheepEating = 1;
    public float minGrassHeightAbs = 0.04f;
    [HideInInspector]
    [SerializeField]
    public List<GrassLogicCell> logicCells = new();

    // Array de escalas actuales por brizna (0..1) — 1 = altura base, 0 = consumida
    private float[] _bladeScales;
    private Mesh _dynamicMesh;
    private bool _meshDirty = false;

    MeshFilter _mf;

    // ?? API pública ??????????????????????????????????????????????
    public void MarkDirty() => _meshDirty = true;

    public GrassLogicCell FindBestCell(Vector3 origin, float searchRadius)
    {
        GrassLogicCell best = null;
        float bestScore = -1f;

        foreach (var cell in logicCells)
        {
            if (!cell.IsGrazeable) continue;
            float d = Vector3.Distance(origin, cell.center);
            if (d > searchRadius) continue;
            float score = cell.grassAmount / Mathf.Max(d, 0.5f);
            if (score > bestScore) { bestScore = score; best = cell; }
        }

        return best;
    }

    // ?? Unity callbacks ??????????????????????????????????????????
    void OnEnable()
    {
        if (!Application.isPlaying)
            RebuildMesh();
    }

    void Start()
    {
        RebuildAll();
    }

    void LateUpdate()
    {
        if (!Application.isPlaying) return;
        UpdateGrazing();
        if (_meshDirty) FlushScalesToMesh();
    }

    // ?? Rebuild ??????????????????????????????????????????????????
    public void RebuildAll()
    {
        RebuildClusters();
        RebuildMesh();
    }

    public void RebuildMesh()
    {
        _mf ??= GetComponent<MeshFilter>();

        _dynamicMesh = GrassMeshBuilder.Build(points, transform);
        _dynamicMesh.MarkDynamic();
        _mf.sharedMesh = _dynamicMesh;

        // Todas las briznas arrancan a escala 1 (altura completa)
        _bladeScales = new float[points.Count];
        for (int i = 0; i < _bladeScales.Length; i++)
            _bladeScales[i] = 1f;

        _meshDirty = false;
    }

    void ApplyCellParams(GrassLogicCell cell)
    {
        cell.consumeRate = consumeRate;
        cell.regenRate = regenRate;
        cell.minGrassToGraze = minGrassToGraze;
        cell.maxSheepEating = Mathf.Max(1, maxSheepEating);
    }

    // ?? Clustering ???????????????????????????????????????????????
    public void RebuildClusters()
    {
        logicCells.Clear();

        if (points.Count == 0) return;

        var assigned = new int[points.Count];
        for (int i = 0; i < assigned.Length; i++) assigned[i] = -1;

        // ?? Paso 1: clusters normales (igual que antes) ??????????????
        var tempCells = new List<GrassLogicCell>();

        for (int i = 0; i < points.Count; i++)
        {
            if (assigned[i] >= 0) continue;

            var cell = new GrassLogicCell();

            for (int j = i; j < points.Count; j++)
            {
                if (assigned[j] >= 0) continue;
                if (Vector3.Distance(points[i].position, points[j].position) <= clusterRadius)
                {
                    cell.bladeIndices.Add(j);
                    assigned[j] = tempCells.Count; // índice provisional
                }
            }

            if (cell.bladeIndices.Count < minBladesPerCell)
            {
                // Devolvemos las briznas al pool — se reasignarán en el paso 2
                foreach (int idx in cell.bladeIndices)
                    assigned[idx] = -1;
                continue;
            }

            // Calcular centroide y altura media
            Vector3 sum = Vector3.zero;
            float heightSum = 0f;
            foreach (int idx in cell.bladeIndices)
            {
                sum += points[idx].position;
                heightSum += points[idx].height;
            }

            cell.center = sum / cell.bladeIndices.Count;
            cell.averageBaseHeight = heightSum / cell.bladeIndices.Count;

            // Hierba corta ? excluida del pastoreo
            if (cell.averageBaseHeight <= shortGrassThreshold)
            {
                foreach (int idx in cell.bladeIndices)
                    assigned[idx] = -2; // marcado como hierba corta — no reasignar
                continue;
            }

            // Actualizar los índices de asignación al índice real en tempCells
            int cellIdx = tempCells.Count;
            foreach (int idx in cell.bladeIndices)
                assigned[idx] = cellIdx;

            ApplyCellParams(cell);
            cell.grassAmount = 1f;
            tempCells.Add(cell);
        }

        // ?? Paso 2: asignar briznas huérfanas a la celda más cercana ??
        // Una brizna es huérfana si assigned[i] == -1
        // (las -2 son hierba corta y se ignoran)
        if (tempCells.Count > 0)
        {
            for (int i = 0; i < points.Count; i++)
            {
                if (assigned[i] != -1) continue; // ya tiene celda o es hierba corta

                float bestDist = float.MaxValue;
                int bestCell = -1;

                for (int c = 0; c < tempCells.Count; c++)
                {
                    float d = Vector3.Distance(points[i].position, tempCells[c].center);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestCell = c;
                    }
                }

                if (bestCell >= 0)
                {
                    tempCells[bestCell].bladeIndices.Add(i);
                    tempCells[bestCell].bladeBaseHeights.Add(points[i].height);
                    assigned[i] = bestCell;
                }
            }
        }
        else
        {
            // ?? Caso extremo: NO hay ningún cluster válido ???????????
            // Creamos una única celda con todas las briznas que no son hierba corta
            var fallbackCell = new GrassLogicCell();
            Vector3 sum = Vector3.zero;
            float heightSum = 0f;
            int count = 0;

            for (int i = 0; i < points.Count; i++)
            {
                if (assigned[i] == -2) continue; // hierba corta
                fallbackCell.bladeIndices.Add(i);
                fallbackCell.bladeBaseHeights.Add(points[i].height);
                sum += points[i].position;
                heightSum += points[i].height;
                count++;
            }

            if (count > 0)
            {
                fallbackCell.center = sum / count;
                fallbackCell.averageBaseHeight = heightSum / count;

                if (fallbackCell.averageBaseHeight > shortGrassThreshold)
                {
                    ApplyCellParams(fallbackCell);
                    fallbackCell.grassAmount = 1f;
                    tempCells.Add(fallbackCell);
                }
            }
        }

        // ?? Recalcular centroides con las briznas huérfanas incluidas ??
        foreach (var cell in tempCells)
        {
            Vector3 sum = Vector3.zero;
            float heightSum = 0f;
            foreach (int idx in cell.bladeIndices)
            {
                sum += points[idx].position;
                heightSum += points[idx].height;
            }
            cell.center = sum / cell.bladeIndices.Count;
            cell.averageBaseHeight = heightSum / cell.bladeIndices.Count;
            // ? Recalcular bladeBaseHeights para que coincida con bladeIndices
            cell.bladeBaseHeights.Clear();
            foreach (int idx in cell.bladeIndices)
                cell.bladeBaseHeights.Add(points[idx].height);
        }

        logicCells.AddRange(tempCells);

        // ?? Verificación de cobertura total ??????????????????????????
        int uncovered = 0;
        for (int i = 0; i < assigned.Length; i++)
            if (assigned[i] == -1) uncovered++;

        Debug.Log($"[GrassRenderer] {logicCells.Count} celdas | " +
                  $"{points.Count} briznas | sin celda: {uncovered} | " +
                  $"shortGrassThreshold={shortGrassThreshold}");
    }

    // ?? Pastoreo runtime ?????????????????????????????????????????
    void UpdateGrazing()
    {
        bool dirty = false;

        foreach (var cell in logicCells)
        {
            if (cell.maxSheepEating < 1) cell.maxSheepEating = 1;

            float prev = cell.grassAmount;

            if (cell._grazers > 0 && cell.grassAmount > 0f)
            {
                cell.grassAmount -= cell.consumeRate * cell._grazers * Time.deltaTime;
                cell.grassAmount = Mathf.Max(0f, cell.grassAmount);
            }
            else if (cell._grazers <= 0 && cell.regenRate > 0f && cell.grassAmount < 1f)
            {
                cell.grassAmount += cell.regenRate * Time.deltaTime;
                cell.grassAmount = Mathf.Min(1f, cell.grassAmount);
            }

            if (cell.grassAmount <= 0f)
            {
                cell._grazers = 0;
                cell._reserved = 0;
            }

            // ? Sin umbral — cualquier cambio actualiza las escalas
            if (cell.grassAmount != prev)
            {
                foreach (int idx in cell.bladeIndices)
                {
                    if (idx < _bladeScales.Length)
                        _bladeScales[idx] = cell.grassAmount;
                }
                dirty = true;
            }
        }

        if (dirty) _meshDirty = true;
    }

    // ? Parcheado de vértices — usa la escala por brizna para comprimir el strip en Y
    void FlushScalesToMesh()
    {
        if (_dynamicMesh == null || _bladeScales == null) { _meshDirty = false; return; }

        Vector3[] verts = _dynamicMesh.vertices;
        int segs = GrassMeshBuilder.SEGMENTS;
        int vpb = GrassMeshBuilder.VerticesPerBlade;

        for (int bladeIdx = 0; bladeIdx < points.Count; bladeIdx++)
        {
            if (bladeIdx >= _bladeScales.Length) break;

            float scale = _bladeScales[bladeIdx];
            var p = points[bladeIdx];
            int baseVert = bladeIdx * vpb;

            // ? Altura escalada nunca baja de minGrassHeightAbs
            // Interpolamos entre minGrassHeightAbs y la altura base completa
            float scaledHeight = Mathf.Max(
                p.height * scale,
                minGrassHeightAbs
            );

            Vector3 up = p.normal.normalized;
            Vector3 right = Vector3.Cross(up, new Vector3(
                Mathf.Sin(p.randomSeed), 0f, Mathf.Cos(p.randomSeed))).normalized;

            if (right.sqrMagnitude < 0.001f)
                right = Vector3.Cross(up, Vector3.forward).normalized;

            Vector3 forward = Vector3.Cross(right, up).normalized;
            float halfW = p.width * 0.5f;

            for (int s = 0; s <= segs; s++)
            {
                float t = s / (float)segs;
                float yOff = t * scaledHeight;
                float zCurve = t * t * scaledHeight * 0.2f;

                Vector3 worldCenter = p.position + up * yOff + forward * zCurve;
                Vector3 localCenter = transform.InverseTransformPoint(worldCenter);
                Vector3 localRight = transform.InverseTransformDirection(right);

                float bellCurve = Mathf.Sin(t * Mathf.PI);
                float w = Mathf.Lerp(halfW, 0.02f, t) + halfW * 0.3f * bellCurve;

                int vi = baseVert + s * 2;
                if (vi + 1 < verts.Length)
                {
                    verts[vi] = localCenter - localRight * w;
                    verts[vi + 1] = localCenter + localRight * w;
                }
            }
        }

        _dynamicMesh.vertices = verts;
        _dynamicMesh.RecalculateBounds();
        _meshDirty = false;
    }
    // ?? Gizmos ???????????????????????????????????????????????????
    void OnDrawGizmos()
    {
        if (logicCells == null) return;

        foreach (var cell in logicCells)
        {
            Color c;

            if (cell.maxSheepEating == 0)
            {
                c = Color.grey;
            }
            else if (cell.averageBaseHeight <= shortGrassThreshold)
            {
                c = new Color(0.8f, 0.1f, 0.1f, 0.85f);
            }
            else
            {
                c = Color.Lerp(
                    new Color(0.85f, 0.15f, 0.05f, 0.85f),
                    new Color(0.1f, 0.85f, 0.15f, 0.85f),
                    cell.grassAmount);
            }

            Gizmos.color = c;

            // ? Radio proporcional a grassAmount — se encoge junto con la hierba
            float radius = clusterRadius * 0.35f * Mathf.Lerp(0.2f, 1f, cell.grassAmount);
            Gizmos.DrawWireSphere(cell.center, radius);

            if (Application.isPlaying && cell._grazers > 0)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(cell.center + Vector3.up * 0.35f, 0.12f);
            }
        }
    }
}