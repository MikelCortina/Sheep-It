using System.Collections.Generic;
using UnityEngine;

public class GrassCell : MonoBehaviour
{
    public static readonly List<GrassCell> AllCells = new List<GrassCell>();

    [Header("Grass Settings")]
    [Range(0f, 1f)] public float grassAmount = 1f;

    [Tooltip("Cantidad consumida por segundo por cada oveja")]
    public float consumeRate = 0.08f;

    [Tooltip("Regeneración. Pon 0 si no quieres que vuelva a crecer")]
    public float regenRate = 0f;

    [Tooltip("Cantidad mínima para poder comer")]
    public float minGrassToGraze = 0.05f;

    [Tooltip("Radio de detección")]
    public float detectionRadius = 4f;

    [Header("Capacity")]
    [Tooltip("Número máximo de ovejas comiendo o yendo hacia esta celda")]
    public int maxSheepEating = 1;

    [Header("Visual")]
    public Renderer grassRenderer;
    public Color fullColor = new Color(0.2f, 0.7f, 0.1f);
    public Color emptyColor = new Color(0.55f, 0.45f, 0.2f);
    public float maxGrassScale = 1f;

    private int _grazersCount = 0;
    private int _reservedCount = 0;

    private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProp = Shader.PropertyToID("_Color");

    public bool IsGrazeable => grassAmount > minGrassToGraze;

    public int UsedSlots => _grazersCount + _reservedCount;

    public bool HasFreeSlot => UsedSlots < maxSheepEating;

    public bool CanBeReserved => IsGrazeable && HasFreeSlot;

    public float Occupancy01
    {
        get
        {
            if (maxSheepEating <= 0) return 1f;
            return Mathf.Clamp01((float)UsedSlots / maxSheepEating);
        }
    }

    private void OnEnable()
    {
        if (!AllCells.Contains(this))
        {
            AllCells.Add(this);
        }
    }

    private void OnDisable()
    {
        AllCells.Remove(this);
    }

    void Update()
    {
        if (_grazersCount > 0 && grassAmount > 0f)
        {
            grassAmount -= consumeRate * _grazersCount * Time.deltaTime;
            grassAmount = Mathf.Max(0f, grassAmount);
        }
        else if (_grazersCount <= 0 && regenRate > 0f && grassAmount > 0f)
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

        UpdateVisuals();
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

        if (_reservedCount > 0)
        {
            _reservedCount--;
            _grazersCount++;
            return true;
        }

        if (!HasFreeSlot) return false;

        _grazersCount++;
        return true;
    }

    public void StopGrazing()
    {
        _grazersCount = Mathf.Max(0, _grazersCount - 1);
    }

    void UpdateVisuals()
    {
        if (grassRenderer != null)
        {
            Material mat = grassRenderer.material;
            Color currentColor = Color.Lerp(emptyColor, fullColor, grassAmount);

            if (mat.HasProperty(BaseColorProp))
            {
                mat.SetColor(BaseColorProp, currentColor);
            }
            else if (mat.HasProperty(ColorProp))
            {
                mat.SetColor(ColorProp, currentColor);
            }
        }

        float s = Mathf.Lerp(0f, maxGrassScale, grassAmount);

        for (int i = 0; i < transform.childCount; i++)
        {
            transform.GetChild(i).localScale = Vector3.one * s;
        }
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