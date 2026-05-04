using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlowerCannon : MonoBehaviour
{
    [Header("Referencias")]
    public Transform spawnPoint;
    public GameObject flowerProjectilePrefab;   // Prefab del proyectil visible
    public LineRenderer trajectoryLine;          // Parábola principal (LineRenderer en este objeto)

    [Header("Configuración de disparo")]
    public int flowerCount = 5;
    public float spreadAngle = 15f;
    public float maxForce = 30f;
    public float maxDragDistance = 200f;

    [Header("Trayectoria")]
    public int trajectoryPoints = 80;

    [Header("Preview trails al apuntar")]
    public Color previewTrailColor = new Color(1f, 0.6f, 0.8f, 0.4f);
    public float previewTrailWidth = 0.04f;

    private Vector2 dragStartPos;
    private bool isDragging = false;
    private float currentForce = 0f;
    private Vector3 currentLaunchDir;

    // LineRenderers extra para mostrar las trayectorias de cada flor al apuntar
    private List<LineRenderer> previewLines = new List<LineRenderer>();

    void Start()
    {
        // Crear un LineRenderer de preview por cada flor
        for (int i = 0; i < flowerCount; i++)
        {
            GameObject go = new GameObject($"PreviewLine_{i}");
            go.transform.SetParent(transform);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = previewTrailColor;
            lr.endColor = new Color(previewTrailColor.r, previewTrailColor.g, previewTrailColor.b, 0f);
            lr.startWidth = previewTrailWidth;
            lr.endWidth = 0f;
            lr.positionCount = 0;
            lr.useWorldSpace = true;
            previewLines.Add(lr);
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            dragStartPos = Input.mousePosition;
            isDragging = true;
        }

        if (Input.GetMouseButton(0) && isDragging)
        {
            Vector2 dragDelta = (Vector2)Input.mousePosition - dragStartPos;
            float dragDistance = Mathf.Clamp(dragDelta.magnitude, 0f, maxDragDistance);
            currentForce = (dragDistance / maxDragDistance) * maxForce;
            currentLaunchDir = GetSlingshotDirection(dragDelta);

            DrawMainTrajectory(currentLaunchDir, currentForce);
            DrawAllPreviewTrails(currentLaunchDir, currentForce);
        }

        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            isDragging = false;

            // Ocultar todas las líneas de preview
            trajectoryLine.positionCount = 0;
            foreach (var lr in previewLines)
                lr.positionCount = 0;

            if (currentForce > 0.5f)
                FireFlowers(currentLaunchDir, currentForce);

            currentForce = 0f;
        }
    }

    Vector3 GetSlingshotDirection(Vector2 dragDelta)
    {
        Vector2 invertedDelta = -dragDelta.normalized;
        Vector3 camRight = Camera.main.transform.right;
        Vector3 camUp = Camera.main.transform.up;
        Vector3 camForward = Camera.main.transform.forward;

        Vector3 dir = camRight * invertedDelta.x + camUp * invertedDelta.y + camForward * 0.3f;
        return dir.normalized;
    }

    // Parábola central principal
    void DrawMainTrajectory(Vector3 direction, float force)
    {
        trajectoryLine.positionCount = trajectoryPoints;
        Vector3[] points = SimulateTrajectory(direction, force, spawnPoint.position);
        trajectoryLine.SetPositions(points);
    }

    // Un trail de preview por cada flor del abanico
    void DrawAllPreviewTrails(Vector3 baseDir, float force)
    {
        for (int i = 0; i < flowerCount; i++)
        {
            float angleOffset = (i - (flowerCount - 1) / 2f) * spreadAngle;
            Quaternion rotation = Quaternion.AngleAxis(angleOffset, Vector3.up);
            Vector3 spreadDir = rotation * baseDir;

            Vector3[] points = SimulateTrajectory(spreadDir, force, spawnPoint.position);
            previewLines[i].positionCount = trajectoryPoints;
            previewLines[i].SetPositions(points);
        }
    }

    // Simulación física compartida
    Vector3[] SimulateTrajectory(Vector3 direction, float force, Vector3 startPos)
    {
        Vector3[] points = new Vector3[trajectoryPoints];
        Vector3 velocity = direction * force;
        Vector3 pos = startPos;
        float dt = Time.fixedDeltaTime;

        for (int i = 0; i < trajectoryPoints; i++)
        {
            points[i] = pos;
            velocity += Physics.gravity * dt;
            pos += velocity * dt;
        }

        return points;
    }

    void FireFlowers(Vector3 baseDir, float force)
    {
        for (int i = 0; i < flowerCount; i++)
        {
            float angleOffset = (i - (flowerCount - 1) / 2f) * spreadAngle;
            Quaternion rotation = Quaternion.AngleAxis(angleOffset, Vector3.up);
            Vector3 spreadDir = rotation * baseDir;

            StartCoroutine(SpawnFlowerDelayed(spreadDir, force, i * 0.05f));
        }
    }

    IEnumerator SpawnFlowerDelayed(Vector3 direction, float force, float delay)
    {
        yield return new WaitForSeconds(delay);

        GameObject projectile = Instantiate(flowerProjectilePrefab, spawnPoint.position, Random.rotation);
        Rigidbody rb = projectile.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.linearVelocity = direction * force;
        }
    }
}