using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class SheepAgent : MonoBehaviour
{
    public enum SheepState { Grazing, Flocking, Fleeing }
    public SheepState CurrentState { get; private set; } = SheepState.Grazing;
    public float Arousal { get; private set; } = 0f;

    private FlockManager _flock;
    private NavMeshAgent _agent;
    private Animator _animator;

    private float _grazingTimer;
    private float _grazingDuration;
    private bool _initialized = false;

    private bool AgentReady => _agent != null
                            && _agent.isActiveAndEnabled
                            && _agent.isOnNavMesh;

    // ?????????????????????????????????????????????
    public void Init(FlockManager manager)
    {
        _flock = manager;
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();

        _agent.speed = Random.Range(_flock.minSpeed, _flock.maxSpeed);
        _agent.angularSpeed = 200f;
        _agent.stoppingDistance = 0.5f;
        _agent.autoBraking = false;
        _agent.acceleration = 4f;
    }

    // ?????????????????????????????????????????????
    void Update()
    {
        if (!AgentReady) return;

        if (!_initialized)
        {
            SetGrazing();
            _initialized = true;
        }

        float distToPlayer = Vector3.Distance(transform.position, _flock.playerTransform.position);

        UpdateArousal(distToPlayer);
        UpdateState(distToPlayer);
        UpdateAnimator();
    }

    // ?????????????????????????????????????????????
    // AROUSAL
    // ?????????????????????????????????????????????
    void UpdateArousal(float distToPlayer)
    {
        if (distToPlayer < _flock.fleeRadius)
            Arousal = Mathf.MoveTowards(Arousal, 1f, Time.deltaTime * _flock.arousalRiseSpeed);
        else
            Arousal = Mathf.MoveTowards(Arousal, 0f, Time.deltaTime / _flock.arousalDecayTime);

        if (Arousal < 0.5f)
        {
            foreach (var other in _flock.allSheep)
            {
                if (other == this) continue;
                float d = Vector3.Distance(transform.position, other.transform.position);
                if (d < _flock.separationRadius * 2f && other.Arousal > 0.7f)
                {
                    Arousal = Mathf.MoveTowards(Arousal, other.Arousal,
                                  Time.deltaTime * _flock.arousalRiseSpeed * 0.5f);
                    break;
                }
            }
        }
    }

    // ?????????????????????????????????????????????
    // ESTADOS
    // ?????????????????????????????????????????????
    void UpdateState(float distToPlayer)
    {
        switch (CurrentState)
        {
            case SheepState.Grazing:
                if (distToPlayer < _flock.fleeRadius) { SetFleeing(); return; }
                if (distToPlayer < _flock.flockRadius || Arousal > 0.3f) { SetFlocking(); return; }
                GrazingUpdate();
                break;

            case SheepState.Flocking:
                if (distToPlayer < _flock.fleeRadius) { SetFleeing(); return; }
                if (distToPlayer > _flock.flockRadius + 3f && Arousal < 0.1f) { SetGrazing(); return; }
                FlockingUpdate();
                break;

            case SheepState.Fleeing:
                if (distToPlayer > _flock.fleeRadius + 2f && Arousal < 0.5f)
                {
                    _agent.speed = _flock.minSpeed + 1f;
                    SetFlocking();
                    return;
                }
                FleeingUpdate();
                break;
        }
    }

    // ?????????????????????????????????????????????
    // GRAZING
    // ?????????????????????????????????????????????
    void SetGrazing()
    {
        CurrentState = SheepState.Grazing;
        _grazingDuration = Random.Range(3f, 8f);
        _grazingTimer = 0f;
        if (AgentReady) _agent.isStopped = true;
    }

    void GrazingUpdate()
    {
        _grazingTimer += Time.deltaTime;
        if (_grazingTimer >= _grazingDuration)
        {
            Vector3 wander = transform.position
                           + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized
                           * Random.Range(2f, 5f);

            if (NavMesh.SamplePosition(wander, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                _agent.isStopped = false;
                _agent.speed = _flock.minSpeed;
                _agent.SetDestination(hit.position);
            }
            SetGrazing();
        }
    }

    // ?????????????????????????????????????????????
    // FLOCKING
    // ?????????????????????????????????????????????
    void SetFlocking()
    {
        CurrentState = SheepState.Flocking;
        if (AgentReady) _agent.isStopped = false;
    }

    void FlockingUpdate()
    {
        _agent.speed = Mathf.Lerp(_flock.minSpeed, _flock.maxSpeed, Arousal);

        Vector3 boidForce = CalculateBoids();
        Vector3 gravityForce = CalculateFlockGravity(panicMode: false);

        Vector3 combined = (boidForce + gravityForce).normalized;
        Vector3 target = transform.position + combined * 2f;

        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            _agent.SetDestination(hit.position);
    }

    // ?????????????????????????????????????????????
    // FLEEING
    // ?????????????????????????????????????????????
    void SetFleeing()
    {
        CurrentState = SheepState.Fleeing;
        if (AgentReady)
        {
            _agent.isStopped = false;
            _agent.speed = _flock.maxSpeed * 1.3f;
        }
    }

    void FleeingUpdate()
    {
        Vector3 fleeDir = (transform.position - _flock.playerTransform.position).normalized;
        Vector3 boidForce = CalculateBoids();
        Vector3 gravityForce = CalculateFlockGravity(panicMode: true);

        float dynFleeWeight = _flock.fleeWeight * Arousal;
        Vector3 combined = (fleeDir * dynFleeWeight + boidForce + gravityForce).normalized;

        Vector3 target = transform.position + combined * 5f;
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            _agent.SetDestination(hit.position);
    }

    // ?????????????????????????????????????????????
    // FLOCK GRAVITY (sistema nuevo)
    // ?????????????????????????????????????????????
    Vector3 CalculateFlockGravity(bool panicMode)
    {
        Vector3 centroid = _flock.FlockCentroid;
        float dist = Vector3.Distance(transform.position, centroid);

        // Solo actúa si la oveja está suficientemente lejos del centro
        if (dist < _flock.flockGravityRadius) return Vector3.zero;

        Vector3 toCenter = (centroid - transform.position).normalized;

        // La fuerza crece con la distancia al centroide (ovejas más alejadas, más atraídas)
        // pero tiene un mínimo para no colapsar el grupo cuando están muy cerca
        float distanceFactor = Mathf.InverseLerp(_flock.flockGravityRadius,
                                                   _flock.flockGravityRadius * 3f,
                                                   dist);
        distanceFactor = Mathf.Max(distanceFactor, _flock.flockGravityFalloff);

        // En pánico el multiplicador aumenta para contrarrestar la dispersión por huida
        float panicFactor = panicMode
            ? Mathf.Lerp(1f, _flock.flockGravityPanicMult, Arousal)
            : 1f;

        // Si el rebaño está muy disperso (FlockSpreadRadius grande), la gravedad se amplifica
        float spreadFactor = Mathf.Clamp(
            _flock.FlockSpreadRadius / (_flock.flockGravityRadius * 2f),
            1f, 2f);

        return toCenter * _flock.flockGravityWeight * distanceFactor * panicFactor * spreadFactor;
    }

    // ?????????????????????????????????????????????
    // BOIDS CORE
    // ?????????????????????????????????????????????
    Vector3 CalculateBoids()
    {
        Vector3 sep = Vector3.zero, ali = Vector3.zero, coh = Vector3.zero;
        float totalInfluence = 0f;

        float perception = Mathf.Lerp(_flock.perceptionRadiusMin,
                                       _flock.perceptionRadiusMax, Arousal);

        // Densidad local
        int densityCount = 0;
        float expectedDensity = _flock.flockSize * 0.3f;

        foreach (var other in _flock.allSheep)
        {
            if (other == this) continue;
            float d = Vector3.Distance(transform.position, other.transform.position);
            if (d > perception) continue;

            float influence = 1f + other.Arousal;
            totalInfluence += influence;

            coh += other.transform.position * influence;
            ali += other.AgentVelocity * influence;

            if (d < _flock.separationRadius)
                sep += (transform.position - other.transform.position) / Mathf.Max(d, 0.01f);

            if (d < _flock.densityStressRadius) densityCount++;
        }

        if (totalInfluence == 0f) return transform.forward;

        coh = (coh / totalInfluence - transform.position).normalized * _flock.cohesionWeight;
        ali = (ali / totalInfluence).normalized * _flock.alignmentWeight;
        sep = sep.normalized * _flock.separationWeight;

        Vector3 steering = sep + ali + coh;

        // Dispersión por densidad
        float localDensity = densityCount / Mathf.Max(1f, expectedDensity);
        float panicMult = Mathf.Lerp(1f, _flock.densityStressPanicMult, Arousal);
        float effectiveThresh = _flock.densityStressThreshold * panicMult;

        if (localDensity > effectiveThresh)
        {
            Vector3 crowdCenter = GetLocalCrowdCenter(_flock.densityStressRadius);
            Vector3 disperseDir = (transform.position - crowdCenter).normalized;
            float intensity = Mathf.InverseLerp(effectiveThresh, 1f, localDensity);
            steering += disperseDir * _flock.dispersionWeight * intensity;
        }

        return steering.normalized;
    }

    Vector3 GetLocalCrowdCenter(float radius)
    {
        Vector3 center = Vector3.zero;
        int count = 0;

        foreach (var sheep in _flock.allSheep)
        {
            if (sheep == this) continue;
            if (Vector3.Distance(transform.position, sheep.transform.position) < radius)
            {
                center += sheep.transform.position;
                count++;
            }
        }
        return count > 0 ? center / count : transform.position;
    }

    public Vector3 AgentVelocity => AgentReady ? _agent.velocity : Vector3.zero;

    // ?????????????????????????????????????????????
    // ANIMACIÓN
    // ?????????????????????????????????????????????
    void UpdateAnimator()
    {
        if (_animator == null || !AgentReady) return;
        _animator.SetFloat("Speed", _agent.velocity.magnitude);
        _animator.SetFloat("Arousal", Arousal);
        _animator.SetBool("IsFleeing", CurrentState == SheepState.Fleeing);
        _animator.SetBool("IsGrazing", CurrentState == SheepState.Grazing);
    }
}