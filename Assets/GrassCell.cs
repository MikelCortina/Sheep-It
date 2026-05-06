using UnityEngine;

public class GrassCell : MonoBehaviour
{
    [Header("Grass Settings")]
    [Range(0f, 1f)] public float grassAmount = 1f;
    [Tooltip("Cantidad consumida por segundo mientras una oveja pasta")]
    public float consumeRate = 0.08f;
    [Tooltip("Cantidad regenerada por segundo cuando no está siendo comida")]
    public float regenRate = 0.01f;
    [Tooltip("Cantidad mínima para que la oveja pueda pastar aquí")]
    public float minGrassToGraze = 0.2f;
    [Tooltip("Radio de detección para que una oveja encuentre esta celda")]
    public float detectionRadius = 4f;

    // Visual refs — asigna tus prefabs de hierba aquí
    [Header("Visual")]
    public Renderer grassRenderer;
    [Tooltip("Color cuando la hierba está llena")]
    public Color fullColor = new Color(0.2f, 0.7f, 0.1f);
    [Tooltip("Color cuando la hierba está agotada")]
    public Color emptyColor = new Color(0.55f, 0.45f, 0.2f);
    [Tooltip("Escala máxima de los meshes de hierba hijos")]
    public float maxGrassScale = 1f;

    public bool IsGrazeable => grassAmount >= minGrassToGraze;
    private int _grazersCount = 0;
    private static readonly int ColorProp = Shader.PropertyToID("_BaseColor");

    void Update()
    {
        if (_grazersCount > 0)
            grassAmount = Mathf.Max(0f, grassAmount - consumeRate * _grazersCount * Time.deltaTime);
        else
            grassAmount = Mathf.Min(1f, grassAmount + regenRate * Time.deltaTime);

        UpdateVisuals();
    }

    public bool TryStartGrazing()
    {
        if (!IsGrazeable) return false;
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
            var mat = grassRenderer.material;
            mat.SetColor(ColorProp, Color.Lerp(emptyColor, fullColor, grassAmount));
        }

        // Escalar hijos (meshes de briznas de hierba)
        for (int i = 0; i < transform.childCount; i++)
        {
            float s = Mathf.Lerp(0f, maxGrassScale, grassAmount);
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