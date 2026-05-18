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
    [Tooltip("Radio máximo para que dos briznas pertenezcan a la misma celda")]
    public float clusterRadius = 2f;
    [Tooltip("Mínimo de briznas para que se forme una celda")]
    public int minBladesPerCell = 3;
    [Tooltip("Briznas con height <= este umbral se consideran 'short grass' y no son pastoreables")]
    public float shortGrassThreshold = 0.08f;

    [Header("Pastoreo")]
    public float consumeRate = 0.08f;
    public float regenRate = 0f;
    public float minGrassToGraze = 0.05f;
    public int maxSheepEating = 1;

    [HideInInspector]
    [SerializeField]
    public List<GrassLogicCell> logicCells = new();

    MeshFilter _mf;
    bool _meshDirty = false;

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
        // ? En Play Mode: clusters + mesh con todos los valores del Inspector ya cargados
        RebuildAll();
    }

    void LateUpdate()
    {
        if (!Application.isPlaying) return;
        UpdateGrazing();
        if (_meshDirty) RebuildMesh();
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
        _mf.sharedMesh = GrassMeshBuilder.Build(points, transform);
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
        var assigned = new bool[points.Count];

        for (int i = 0; i < points.Count; i++)
        {
            if (assigned[i]) continue;

            var cell = new GrassLogicCell();

            for (int j = i; j < points.Count; j++)
            {
                if (assigned[j]) continue;
                if (Vector3.Distance(points[i].position, points[j].position) <= clusterRadius)
                {
                    cell.bladeIndices.Add(j);
                    cell.bladeBaseHeights.Add(points[j].height);
                    assigned[j] = true;
                }
            }

            if (cell.bladeIndices.Count < minBladesPerCell)
            {
                foreach (int idx in cell.bladeIndices) assigned[idx] = false;
                continue;
            }

            // Centroide real
            Vector3 sum = Vector3.zero;
            float heightSum = 0f;
            foreach (int idx in cell.bladeIndices)
            {
                sum += points[idx].position;
                heightSum += points[idx].height;
            }
            cell.center = sum / cell.bladeIndices.Count;
            cell.averageBaseHeight = heightSum / cell.bladeIndices.Count;

            // ? Hierba corta = no pastoreable de base
            // grassAmount arranca en 1 siempre, pero IsGrazeable quedará false
            // porque averageBaseHeight <= shortGrassThreshold indica que no hay
            // suficiente hierba que consumir — lo controlamos via minGrassToGraze
            // Para hierba corta directamente no la añadimos a logicCells
            if (cell.averageBaseHeight <= shortGrassThreshold)
                continue;

            ApplyCellParams(cell);
            cell.grassAmount = 1f;

            logicCells.Add(cell);
        }

        Debug.Log($"[GrassRenderer] {logicCells.Count} celdas lógicas pastoreables | " +
                  $"{points.Count} briznas | clusterRadius={clusterRadius}");
    }

    // ?? Pastoreo runtime ?????????????????????????????????????????
    void UpdateGrazing()
    {
        bool dirty = false;

        foreach (var cell in logicCells)
        {
            // Paranoia: garantiza parámetros válidos en runtime
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

            if (Mathf.Abs(cell.grassAmount - prev) > 0.005f)
            {
                ApplyCellHeights(cell);
                dirty = true;
            }
        }

        if (dirty) _meshDirty = true;
    }

    void ApplyCellHeights(GrassLogicCell cell)
    {
        for (int i = 0; i < cell.bladeIndices.Count; i++)
        {
            int idx = cell.bladeIndices[i];
            var point = points[idx];
            point.height = cell.bladeBaseHeights[i] * cell.grassAmount;
            points[idx] = point;
        }
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
                // ? Bug de parámetros
                c = Color.grey;
            }
            else if (cell.averageBaseHeight <= shortGrassThreshold)
            {
                // ?? Hierba corta — no pastoreable de base
                c = new Color(0.8f, 0.1f, 0.1f, 0.8f);
            }
            else
            {
                // ????? según grassAmount
                c = Color.Lerp(
                    new Color(0.85f, 0.15f, 0.05f, 0.8f),
                    new Color(0.1f, 0.85f, 0.15f, 0.8f),
                    cell.grassAmount);
            }

            Gizmos.color = c;
            Gizmos.DrawWireSphere(cell.center, clusterRadius * 0.35f);

            // ?? Punto si hay ovejas comiendo ahora
            if (Application.isPlaying && cell._grazers > 0)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(cell.center + Vector3.up * 0.35f, 0.12f);
            }
        }
    }
}