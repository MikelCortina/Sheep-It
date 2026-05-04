using UnityEngine;

public class FlowerProjectile : MonoBehaviour
{
    [Header("Flor visual al impacto")]
    public GameObject flowerVisualPrefab;   // Asignar el prefab de la flor visual

    [Header("Trail")]
    public Color trailStartColor = new Color(1f, 0.4f, 0.7f, 1f);
    public Color trailEndColor = new Color(1f, 0.8f, 0.2f, 0f);
    public float trailTime = 0.4f;
    public float trailStartWidth = 0.08f;

    private bool hasLanded = false;
    private TrailRenderer trail;

    void Start()
    {
        trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = trailTime;
        trail.startWidth = trailStartWidth;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.05f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = trailStartColor;
        trail.endColor = trailEndColor;

        Destroy(gameObject, 8f);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasLanded) return;
        hasLanded = true;

        if (flowerVisualPrefab != null)
        {
            Vector3 spawnPos;

            // Raycast hacia abajo desde el proyectil para encontrar la superficie real del suelo
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 5f))
            {
                spawnPos = hit.point; // punto exacto en la superficie del suelo
            }
            else
            {
                // Fallback por si el raycast falla
                spawnPos = collision.contacts[0].point;
            }

            GameObject flowerVisual = Instantiate(flowerVisualPrefab, spawnPos, Quaternion.identity);
            flowerVisual.AddComponent<FlowerGrow>();
        }

        if (trail != null)
        {
            trail.transform.SetParent(null);
            Destroy(trail.gameObject, trailTime + 0.1f);
        }

        Destroy(gameObject);
    }
}