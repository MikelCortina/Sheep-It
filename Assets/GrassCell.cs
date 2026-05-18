using System.Collections.Generic;
using UnityEngine;

public class GrassCell : MonoBehaviour
{
    public static readonly List<GrassCell> AllCells = new();

    [Header("Grass Settings")]
    [Range(0f, 1f)] public float grassAmount = 1f;
    public float consumeRate = 0.08f;
    public float regenRate = 0f;
    public float minGrassToGraze = 0.05f;
    public float detectionRadius = 4f;

    [Header("Capacity")]
    public int maxSheepEating = 1;

    // Briznas asignadas a esta celda (índices en GrassRenderer.points)
    [HideInInspector] public List<int> bladeIndices = new();
    [HideInInspector] public List<float> bladeBaseHeights = new(); // alturas originales
    [HideInInspector] public GrassRenderer linkedRenderer;

    int _grazersCount = 0;
    int _reservedCount = 0;

    public bool IsGrazeable => grassAmount > minGrassToGraze;
    public int UsedSlots => _grazersCount + _reservedCount;
    public bool HasFreeSlot => UsedSlots < maxSheepEating;
    public bool CanBeReserved => IsGrazeable && HasFreeSlot;

    public float Occupancy01 => maxSheepEating <= 0
        ? 1f : Mathf.Clamp01((float)UsedSlots / maxSheepEating);

    void OnEnable() { if (!AllCells.Contains(this)) AllCells.Add(this); }
    void OnDisable() { AllCells.Remove(this); }

    void Update()
    {
        if (_grazersCount > 0 && grassAmount > 0f)
        {
            grassAmount -= consumeRate * _grazersCount * Time.deltaTime;
            grassAmount = Mathf.Max(0f, grassAmount);
        }
        else if (_grazersCount <= 0 && regenRate > 0f && grassAmount < 1f)
        {
            grassAmount += regenRate * Time.deltaTime;
            grassAmount = Mathf.Min(1f, grassAmount);
        }

        if (grassAmount <= 0f)
        {
            grassAmount = 0f;
            _grazersCount = 0;
            _reservedCount = 0;
        }

        UpdateBladeHeights();
    }

    void UpdateBladeHeights()
    {
        if (linkedRenderer == null || bladeIndices.Count == 0) return;

        bool dirty = false;
        for (int i = 0; i < bladeIndices.Count; i++)
        {
            int idx = bladeIndices[i];
            float baseHeight = bladeBaseHeights[i];
            float targetHeight = baseHeight * grassAmount;

            var point = linkedRenderer.points[idx];
            // Solo marcamos dirty si hay cambio significativo
            if (Mathf.Abs(point.height - targetHeight) > 0.001f)
            {
                point.height = targetHeight;
                linkedRenderer.points[idx] = point;
                dirty = true;
            }
        }

        if (dirty) linkedRenderer.MarkDirty();
    }

    public bool TryReserve()
    {
        if (!CanBeReserved) return false;
        _reservedCount++;
        return true;
    }

    public void CancelReservation()
    {
        _reservedCount = Mathf.Max(0, _reservedCount - 1);
    }

    public bool TryStartGrazing()
    {
        if (!IsGrazeable) return false;
        if (_reservedCount > 0) { _reservedCount--; _grazersCount++; return true; }
        if (!HasFreeSlot) return false;
        _grazersCount++;
        return true;
    }

    public void StopGrazing()
    {
        _grazersCount = Mathf.Max(0, _grazersCount - 1);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.Lerp(Color.red, Color.green, grassAmount);
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}