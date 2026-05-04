using UnityEngine;

public class FlowerProjectile : MonoBehaviour
{
    [Header("Flor visual al impacto")]
    public GameObject flowerVisualPrefab;

    [Header("Trail")]
    public Color trailStartColor = new Color(1f, 0.4f, 0.7f, 1f);
    public Color trailEndColor = new Color(1f, 0.8f, 0.2f, 0f);
    public float trailTime = 0.4f;
    public float trailStartWidth = 0.08f;

    [HideInInspector] public float gravityScale = 0.3f;

    private bool hasLanded = false;
    private TrailRenderer trail;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;

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

    void FixedUpdate()
    {
        if (!hasLanded)
            rb.AddForce(Physics.gravity * gravityScale, ForceMode.Acceleration);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasLanded) return;
        hasLanded = true;

        if (flowerVisualPrefab != null)
        {
            Vector3 spawnPos;
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 5f))
                spawnPos = hit.point;
            else
                spawnPos = collision.contacts[0].point;

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