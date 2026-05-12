using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class FlockManager : MonoBehaviour
{
    [Header("Spawn")]
    public GameObject sheepPrefab;
    public int flockSize = 20;
    public Vector3 spawnBounds = new Vector3(10f, 0f, 10f);

    [Header("Boids Weights")]
    public float separationWeight = 1.2f;
    public float alignmentWeight = 1.0f;
    public float cohesionWeight = 1.8f;
    public float fleeWeight = 2.5f;
    public float dispersionWeight = 1.0f;

    [Header("Boids Ranges")]
    public float separationRadius = 2f;
    public float perceptionRadiusMin = 6f;
    public float perceptionRadiusMax = 10f;

    [Header("Movement")]
    public float minSpeed = 1.5f;
    public float maxSpeed = 4.5f;

    [Header("Arousal")]
    public float arousalDecayTime = 25f;
    public float arousalRiseSpeed = 5f;

    [Header("State Thresholds")]
    public float fleeRadius = 6f;
    public float flockRadius = 12f;

    [Header("Density Stress")]
    public float densityStressRadius = 3f;
    public float densityStressThreshold = 0.65f;
    public float densityStressPanicMult = 0.75f;

    [Header("Flock Gravity")]
    [Tooltip("Fuerza de atracción hacia el centroide del rebaño")]
    public float flockGravityWeight = 1.5f;

    [Tooltip("Solo actúa si la oveja está a más de esta distancia del centroide")]
    public float flockGravityRadius = 8f;

    [Tooltip("Fracción mínima de fuerza cuando está muy cerca")]
    public float flockGravityFalloff = 0.2f;

    [Tooltip("Multiplica la gravedad cuando el rebaño está en Fleeing")]
    public float flockGravityPanicMult = 1.8f;

    [Header("Anti-Split")]
    [Tooltip("Distancia del centroide a partir de la cual se activa el magnetismo fuerte")]
    public float antiSplitRadius = 14f;

    [Tooltip("Fuerza extra de atracción al centroide cuando la oveja se está separando")]
    public float antiSplitWeight = 3.5f;

    [Tooltip("Solo actúa si el Arousal supera este valor")]
    [Range(0f, 1f)]
    public float antiSplitArousalThreshold = 0.4f;

    [Header("Idle Wander")]
    [Tooltip("Segundos quieta antes de dispersarse")]
    public float idleRelaxTime = 0f;

    [Tooltip("Radio máximo de cada paso de campeo desde la posición actual")]
    public float idleWanderRadius = 10f;

    [Tooltip("Velocidad al campear")]
    public float idleWanderSpeed = 1.0f;

    [Tooltip("Segundos base entre cada paso de campeo")]
    public float idleWanderInterval = 1.2f;

    [Tooltip("Cuánto tiende la oveja a alejarse del centroide")]
    [Range(0f, 1f)]
    public float idleSpreadBias = 0.75f;

    [Tooltip("Radio máximo desde el centroide dentro del que pueden campear")]
    public float idleMaxSpreadRadius = 20f;

    [Header("Pasture Grazing")]
    [Tooltip("Radio de búsqueda de celdas de hierba alrededor de cada oveja")]
    public float pastureSearchRadius = 12f;

    [Tooltip("Tiempo mínimo que una oveja pasa pastando en una celda")]
    public float pastureGrazeMinTime = 5f;

    [Tooltip("Tiempo máximo que una oveja pasa pastando en una celda")]
    public float pastureGrazeMaxTime = 12f;

    [Tooltip("Distancia antes de la celda donde la oveja se detiene a pastar")]
    public float pastureStopOffset = 1.2f;

    [Tooltip("Velocidad de rotación en grados/segundo hacia la celda de hierba")]
    public float pastureRotateSpeed = 120f;

    [Header("Panic Behaviour")]
    public float panicSpeedMultiplier = 1.8f;
    public float panicSeparationMultiplier = 2.5f;
    public float panicDispersionMultiplier = 2.2f;
    public float postPanicCohesionMultiplier = 2.0f;
    public float postPanicArousalThreshold = 0.45f;

    [HideInInspector] public Vector3 FlockCentroid { get; private set; }
    [HideInInspector] public float FlockSpreadRadius { get; private set; }

    [HideInInspector] public Transform playerTransform;
    [HideInInspector] public List<SheepAgent> allSheep = new();
    [HideInInspector] public List<FoodSource> foodSources = new();

    void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            Debug.LogError("[FlockManager] No se encontró ningún objeto con tag 'Player'.");
            return;
        }

        playerTransform = player.transform;

        int spawned = 0;
        int attempts = 0;
        int maxAttempts = flockSize * 5;

        while (spawned < flockSize && attempts < maxAttempts)
        {
            attempts++;

            Vector3 rawPos = transform.position + new Vector3(
                Random.Range(-spawnBounds.x, spawnBounds.x),
                0f,
                Random.Range(-spawnBounds.z, spawnBounds.z)
            );

            if (NavMesh.SamplePosition(rawPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                var go = Instantiate(sheepPrefab, hit.position, Quaternion.identity);
                var agent = go.GetComponent<SheepAgent>();

                if (agent != null)
                {
                    float scale = Random.Range(0.85f, 1.15f);
                    go.transform.localScale = Vector3.one * scale;

                    allSheep.Add(agent);
                    agent.Init(this);
                    spawned++;
                }
            }
        }

        if (spawned < flockSize)
        {
            Debug.LogWarning(
                $"[FlockManager] Solo se spawnearon {spawned}/{flockSize} ovejas. " +
                "Amplía el NavMesh o mueve el FlockManager a una zona navegable."
            );
        }

        foodSources.AddRange(FindObjectsByType<FoodSource>(FindObjectsSortMode.None));
    }

    void Update()
    {
        foodSources.RemoveAll(fs => fs == null);
        allSheep.RemoveAll(sheep => sheep == null);

        if (allSheep.Count == 0) return;

        Vector3 sum = Vector3.zero;

        foreach (var sheep in allSheep)
        {
            sum += sheep.transform.position;
        }

        FlockCentroid = sum / allSheep.Count;

        float spreadSum = 0f;

        foreach (var sheep in allSheep)
        {
            spreadSum += Vector3.Distance(sheep.transform.position, FlockCentroid);
        }

        FlockSpreadRadius = spreadSum / allSheep.Count;
    }

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(FlockCentroid, 0.4f);

        Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
        Gizmos.DrawSphere(FlockCentroid, idleMaxSpreadRadius);

        Gizmos.color = Color.green;
        DrawWireCircle(FlockCentroid, idleMaxSpreadRadius, 32);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.1f);
        DrawWireCircle(FlockCentroid, flockGravityRadius, 32);

        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        DrawWireCircle(FlockCentroid, antiSplitRadius, 32);

        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.08f);

        foreach (var sheep in allSheep)
        {
            if (sheep != null)
            {
                Gizmos.DrawWireSphere(sheep.transform.position, pastureSearchRadius);
            }
        }
    }

    void DrawWireCircle(Vector3 center, float radius, int segments)
    {
        float step = 360f / segments;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * step * Mathf.Deg2Rad;
            Vector3 next = center + new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius
            );

            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}