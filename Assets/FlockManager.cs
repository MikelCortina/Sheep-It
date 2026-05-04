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
    [Tooltip("Fracción mínima de fuerza cuando está muy cerca (evita colapso)")]
    public float flockGravityFalloff = 0.2f;
    [Tooltip("Multiplica la gravedad cuando el rebaño está en Fleeing (mantiene grupo bajo presión)")]
    public float flockGravityPanicMult = 1.8f;

    // Centroide y dispersión calculados una vez por frame para todas las ovejas
    [HideInInspector] public Vector3 FlockCentroid { get; private set; }
    [HideInInspector] public float FlockSpreadRadius { get; private set; } // radio promedio del grupo

    [HideInInspector] public Transform playerTransform;
    [HideInInspector] public List<SheepAgent> allSheep = new();

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
                Random.Range(-spawnBounds.z, spawnBounds.z));

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
            Debug.LogWarning($"[FlockManager] Solo se spawnearon {spawned}/{flockSize} ovejas. " +
                             "Amplía el NavMesh o mueve el FlockManager a una zona navegable.");
    }

    void Update()
    {
        if (allSheep.Count == 0) return;

        // Centroide
        Vector3 sum = Vector3.zero;
        foreach (var sheep in allSheep)
            sum += sheep.transform.position;
        FlockCentroid = sum / allSheep.Count;

        // Radio de dispersión promedio (mide qué tan fragmentado está el grupo)
        float spreadSum = 0f;
        foreach (var sheep in allSheep)
            spreadSum += Vector3.Distance(sheep.transform.position, FlockCentroid);
        FlockSpreadRadius = spreadSum / allSheep.Count;
    }
}