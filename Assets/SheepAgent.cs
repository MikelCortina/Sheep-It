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

    private float _stillTimer = 0f;
    private bool _isIdleWandering = false;
    private float _idleWanderCooldown = 0f;

    private Vector3 _personalDriftDir = Vector3.zero;
    private float _personalDriftTimer = 0f;

    private FoodSource _targetFood = null;
    private bool _isEating = false;
    private float _eatTimer = 0f;
    private float _eatDuration = 0f;

    private GrassCell _targetCell = null;
    private bool _isPastureGrazing = false;
    private bool _hasGrassReservation = false;
    private bool _isPastureGrazingRegistered = false;
    private float _pastureGrazeTimer = 0f;
    private float _pastureGrazeDuration = 0f;

    private float _grassDecisionLockTimer = 0f;
    private float _grassDecisionLockDuration = 2.5f;

    private float _grassRepathTimer = 0f;
    private float _grassRepathInterval = 0.75f;

    private float _grassTravelTimer = 0f;
    private float _grassMaxTravelTime = 6f;

    private float _personalDecisionMultiplier = 1f;
    private float _personalRepathMultiplier = 1f;
    private float _personalTravelMultiplier = 1f;

    private Transform _externalThreat;
    private float _externalThreatTimer = 0f;
    private float _externalThreatDuration = 1.5f;

    private Vector3 _socialPanicDirection = Vector3.zero;
    private float _socialPanicTimer = 0f;
    private float _socialPanicDuration = 2f;

    private bool _isCaughtByWolf = false;

    private bool AgentReady => _agent != null
                            && _agent.isActiveAndEnabled
                            && _agent.isOnNavMesh;

    private float FleeRadius => _flock.PlayerTargetTag == "Coche"
        ? _flock.fleeRadiusCar
        : _flock.fleeRadius;

    private float FlockRadius => _flock.PlayerTargetTag == "Coche"
        ? _flock.flockRadiusCar
        : _flock.flockRadius;

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

        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        _personalDriftDir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        _personalDriftTimer = Random.Range(0f, 5f);

        _personalDecisionMultiplier = Random.Range(0.8f, 1.35f);
        _personalRepathMultiplier = Random.Range(0.75f, 1.45f);
        _personalTravelMultiplier = Random.Range(0.85f, 1.3f);
    }

    void Update()
    {
        if (!AgentReady) return;

        if (_isCaughtByWolf)
        {
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
            return;
        }

        if (!_initialized)
        {
            SetGrazing();
            _initialized = true;
        }

        if (_flock == null || _flock.playerTransform == null) return;

        float distToPlayer = Vector3.Distance(transform.position, _flock.playerTransform.position);

        if (_externalThreatTimer > 0f)
        {
            _externalThreatTimer -= Time.deltaTime;
        }
        else
        {
            _externalThreat = null;
        }

        if (_socialPanicTimer > 0f)
        {
            _socialPanicTimer -= Time.deltaTime;
        }
        else
        {
            _socialPanicDirection = Vector3.zero;
        }

        if (_grassDecisionLockTimer > 0f)
        {
            _grassDecisionLockTimer -= Time.deltaTime;
        }

        if (_grassRepathTimer > 0f)
        {
            _grassRepathTimer -= Time.deltaTime;
        }

        UpdateArousal(distToPlayer);
        UpdateState(distToPlayer);
        UpdateAnimator();
    }

    public void AddThreat(Transform threat, float fearAmount)
    {
        if (threat == null) return;

        _externalThreat = threat;
        _externalThreatTimer = _externalThreatDuration;

        Arousal = Mathf.Clamp01(Mathf.Max(Arousal, fearAmount));

        if (CurrentState != SheepState.Fleeing)
        {
            SetFleeing();
        }
    }

    public void CatchByWolf()
    {
        if (_isCaughtByWolf) return;

        _isCaughtByWolf = true;

        CurrentState = SheepState.Fleeing;
        Arousal = 1f;

        if (_targetFood != null)
        {
            _targetFood.Release();
            _targetFood = null;
        }

        _isEating = false;
        StopPastureGrazing();

        if (AgentReady)
        {
            _agent.ResetPath();
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
        }

        if (_animator != null)
        {
            _animator.SetFloat("Speed", 0f);
            _animator.SetBool("IsFleeing", false);
            _animator.SetBool("IsGrazing", false);
            _animator.SetBool("IsEating", false);
            _animator.SetBool("IsPastureGrazing", false);
        }
    }

    public void DisableMovementCompletely()
    {
        if (AgentReady)
        {
            _agent.ResetPath();
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
            _agent.enabled = false;
        }

        Collider col = GetComponent<Collider>();

        if (col != null)
        {
            col.enabled = false;
        }

        if (_animator != null)
        {
            _animator.SetFloat("Speed", 0f);
            _animator.SetBool("IsFleeing", false);
            _animator.SetBool("IsGrazing", false);
            _animator.SetBool("IsEating", false);
            _animator.SetBool("IsPastureGrazing", false);
        }
    }

    void UpdateArousal(float distToPlayer)
    {
        bool playerThreat = distToPlayer < FleeRadius;
        bool externalThreat = _externalThreat != null && _externalThreatTimer > 0f;
        bool socialPanic = _socialPanicTimer > 0f;

        if (playerThreat || externalThreat || socialPanic)
        {
            Arousal = Mathf.MoveTowards(Arousal, 1f, Time.deltaTime * _flock.arousalRiseSpeed);
        }
        else
        {
            Arousal = Mathf.MoveTowards(Arousal, 0f, Time.deltaTime / _flock.arousalDecayTime);
        }

        foreach (var other in _flock.allSheep)
        {
            if (other == null || other == this) continue;

            float d = Vector3.Distance(transform.position, other.transform.position);

            if (d < _flock.separationRadius * 3f && other.Arousal > 0.7f)
            {
                Arousal = Mathf.MoveTowards(
                    Arousal,
                    other.Arousal,
                    Time.deltaTime * _flock.arousalRiseSpeed * 0.8f
                );

                Vector3 panicDir = other.AgentVelocity;

                if (panicDir.sqrMagnitude < 0.01f)
                {
                    panicDir = transform.position - other.transform.position;
                }

                panicDir.y = 0f;

                if (panicDir.sqrMagnitude > 0.01f)
                {
                    _socialPanicDirection = panicDir.normalized;
                    _socialPanicTimer = _socialPanicDuration;
                }
            }
        }
    }

    void UpdateState(float distToPlayer)
    {
        switch (CurrentState)
        {
            case SheepState.Grazing:
                if (distToPlayer < FleeRadius || _externalThreat != null || _socialPanicTimer > 0f || Arousal > 0.75f)
                {
                    SetFleeing();
                    return;
                }

                if (distToPlayer < FlockRadius || Arousal > 0.3f)
                {
                    SetFlocking();
                    return;
                }

                GrazingUpdate();
                break;

            case SheepState.Flocking:
                if (distToPlayer < FleeRadius || _externalThreat != null || _socialPanicTimer > 0f || Arousal > 0.75f)
                {
                    SetFleeing();
                    return;
                }

                if (distToPlayer > FlockRadius + 3f && Arousal < 0.1f)
                {
                    SetGrazing();
                    return;
                }

                FlockingUpdate();
                break;

            case SheepState.Fleeing:
                if (_externalThreat == null && _socialPanicTimer <= 0f && distToPlayer > FleeRadius + 2f && Arousal < 0.35f)
                {
                    _agent.speed = _flock.minSpeed + 1f;
                    SetFlocking();
                    return;
                }

                FleeingUpdate();
                break;
        }
    }

    void SetGrazing()
    {
        CurrentState = SheepState.Grazing;
        _grazingDuration = Random.Range(3f, 8f);
        _grazingTimer = 0f;
        _stillTimer = 0f;

        _isIdleWandering = false;
        _idleWanderCooldown = Random.Range(0f, _flock.idleWanderInterval);

        if (AgentReady)
        {
            _agent.isStopped = true;
        }
    }

    void GrazingUpdate()
    {
        bool playerFar = Vector3.Distance(transform.position, _flock.playerTransform.position) > FlockRadius;
        bool calmEnough = Arousal < 0.15f;

        if (!_isIdleWandering)
        {
            if (playerFar && calmEnough)
            {
                _stillTimer += Time.deltaTime;

                if (_stillTimer >= _flock.idleRelaxTime)
                {
                    _isIdleWandering = true;
                    _idleWanderCooldown = 0f;
                }
            }
            else
            {
                _stillTimer = 0f;
            }
        }

        if (_isIdleWandering && (!playerFar || !calmEnough))
        {
            _isIdleWandering = false;
            _stillTimer = 0f;
            _agent.isStopped = true;
            StopPastureGrazing();
        }

        if (_targetFood != null && !_isEating)
        {
            float distToFood = Vector3.Distance(transform.position, _targetFood.transform.position);
            bool arrived = distToFood < 1.5f || (!_agent.pathPending && _agent.remainingDistance < 1.5f);

            if (arrived)
            {
                _isEating = true;
                _eatTimer = 0f;
                _eatDuration = Random.Range(3f, 7f);
                _agent.isStopped = true;
            }
        }

        if (_isIdleWandering)
        {
            IdleWanderUpdate();
            return;
        }

        _grazingTimer += Time.deltaTime;

        if (_grazingTimer >= _grazingDuration)
        {
            _grazingTimer = 0f;
            _grazingDuration = Random.Range(3f, 8f);

            Vector3 randomDir = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
            Vector3 wander = transform.position + randomDir * Random.Range(2f, 5f);

            if (NavMesh.SamplePosition(wander, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                _agent.isStopped = false;
                _agent.speed = _flock.minSpeed;
                _agent.SetDestination(hit.position);
            }
        }
    }

    void IdleWanderUpdate()
    {
        if (_isEating)
        {
            EatingUpdate();
            return;
        }

        FoodSource nearestFood = FindBestFood();

        if (nearestFood != null && _targetFood == null)
        {
            StopPastureGrazing();
            MoveToFood(nearestFood);
            return;
        }

        if (_isPastureGrazing)
        {
            PastureGrazingUpdate();
            return;
        }

        _idleWanderCooldown -= Time.deltaTime;

        if (_idleWanderCooldown > 0f) return;

        _idleWanderCooldown = _flock.idleWanderInterval + Random.Range(-0.3f, 0.8f);

        GrassCell cell = FindBestGrassCell();

        if (cell != null)
        {
            StartPastureGrazing(cell);
            return;
        }

        DoIdleWanderStep();
    }

    void DoIdleWanderStep()
    {
        if (Random.value < 0.25f)
        {
            _agent.isStopped = true;
            return;
        }

        float distFromCentroid = Vector3.Distance(transform.position, _flock.FlockCentroid);
        bool overLimit = distFromCentroid > _flock.idleMaxSpreadRadius;

        Vector3 driftDir;

        if (overLimit)
        {
            Vector3 backDir = (_flock.FlockCentroid - transform.position).normalized;
            Vector3 randomDir = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
            driftDir = Vector3.Lerp(backDir, randomDir, 0.2f).normalized;
        }
        else
        {
            _personalDriftTimer -= Time.deltaTime;

            if (_personalDriftTimer <= 0f || _personalDriftDir == Vector3.zero)
            {
                _personalDriftTimer = Random.Range(4f, 7f);

                Vector3 awayFromCenter = transform.position - _flock.FlockCentroid;

                if (awayFromCenter.magnitude > 1.5f)
                {
                    Vector3 awayDir = awayFromCenter.normalized;
                    Vector3 randPerp = Vector3.Cross(awayDir, Vector3.up).normalized * Random.Range(-0.6f, 0.6f);
                    _personalDriftDir = (awayDir + randPerp).normalized;
                }
                else
                {
                    float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    _personalDriftDir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                }
            }

            float proximityFactor = Mathf.InverseLerp(
                _flock.idleMaxSpreadRadius,
                _flock.idleMaxSpreadRadius * 0.6f,
                distFromCentroid
            );

            float effectiveBias = _flock.idleSpreadBias * proximityFactor;

            Vector3 randomDir = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
            driftDir = Vector3.Lerp(randomDir, _personalDriftDir, effectiveBias).normalized;
        }

        float stepDist = Random.Range(_flock.idleWanderRadius * 0.6f, _flock.idleWanderRadius);
        Vector3 candidate = transform.position + driftDir * stepDist;

        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, stepDist * 0.5f + 1f, NavMesh.AllAreas))
        {
            _agent.isStopped = false;
            _agent.speed = _flock.idleWanderSpeed * Random.Range(0.8f, 1.2f);
            _agent.SetDestination(hit.position);
        }
        else
        {
            Vector3 fallback = transform.position
                             + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized * 2f;

            if (NavMesh.SamplePosition(fallback, out NavMeshHit fallbackHit, 3f, NavMesh.AllAreas))
            {
                _agent.isStopped = false;
                _agent.speed = _flock.idleWanderSpeed;
                _agent.SetDestination(fallbackHit.position);
            }
            else
            {
                _agent.isStopped = true;
            }
        }
    }

    GrassCell FindBestGrassCell()
    {
        GrassCell best = null;
        float bestScore = -1f;
        float radius = _flock.pastureSearchRadius;

        foreach (GrassCell cell in GrassCell.AllCells)
        {
            if (cell == null) continue;
            if (!cell.CanBeReserved) continue;

            float d = Vector3.Distance(transform.position, cell.transform.position);

            if (d > radius) continue;
            if (d > cell.detectionRadius) continue;

            float distanceScore = cell.grassAmount / Mathf.Max(d, 0.5f);
            float freeSlotBonus = 1f - cell.Occupancy01;

            float score = distanceScore + freeSlotBonus * 2f;

            if (score > bestScore)
            {
                bestScore = score;
                best = cell;
            }
        }

        return best;
    }

    void StartPastureGrazing(GrassCell cell)
    {
        if (cell == null) return;

        if (!cell.TryReserve())
        {
            return;
        }

        _targetCell = cell;
        _hasGrassReservation = true;
        _isPastureGrazingRegistered = false;
        _isPastureGrazing = true;

        _grassDecisionLockTimer = _grassDecisionLockDuration * _personalDecisionMultiplier;
        _grassTravelTimer = 0f;

        Vector3 dirToCell = (cell.transform.position - transform.position).normalized;
        float distToCell = Vector3.Distance(transform.position, cell.transform.position);
        float stopOffset = Mathf.Min(_flock.pastureStopOffset, distToCell * 0.8f);
        Vector3 destination = cell.transform.position - dirToCell * stopOffset;

        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            _agent.isStopped = false;
            _agent.speed = _flock.idleWanderSpeed * 0.8f;
            _agent.SetDestination(hit.position);
        }
        else
        {
            StopPastureGrazing();
        }
    }
    void PastureGrazingUpdate()
    {
        if (_targetCell == null)
        {
            StopPastureGrazing();
            return;
        }

        _grassTravelTimer += Time.deltaTime;

        if (!_targetCell.IsGrazeable)
        {
            StopPastureGrazing();
            TryFindNewGrassWithCooldown();
            return;
        }

        float dist = Vector3.Distance(transform.position, _targetCell.transform.position);
        bool arrived = dist < 1.5f || (!_agent.pathPending && _agent.remainingDistance < 1.2f);

        if (!arrived)
        {
            if (_grassTravelTimer > _grassMaxTravelTime * _personalTravelMultiplier && _grassDecisionLockTimer <= 0f)
            {
                StopPastureGrazing();
                TryFindNewGrassWithCooldown();
                return;
            }

            return;
        }

        if (!_isPastureGrazingRegistered)
        {
            if (!_targetCell.TryStartGrazing())
            {
                StopPastureGrazing();
                TryFindNewGrassWithCooldown();
                return;
            }

            _hasGrassReservation = false;
            _isPastureGrazingRegistered = true;
        }

        _agent.isStopped = true;

        Vector3 dirToCell = _targetCell.transform.position - transform.position;
        dirToCell.y = 0f;

        if (dirToCell.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dirToCell.normalized);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                _flock.pastureRotateSpeed * Time.deltaTime
            );
        }
    }

    void TryFindNewGrassWithCooldown()
    {
        if (_grassRepathTimer > 0f)
        {
            _idleWanderCooldown = _flock.idleWanderInterval;
            return;
        }

        _grassRepathTimer = _grassRepathInterval * _personalRepathMultiplier;

        GrassCell next = FindBestGrassCell();

        if (next != null)
        {
            StartPastureGrazing(next);
        }
        else
        {
            _idleWanderCooldown = _flock.idleWanderInterval;
        }
    }

    void StopPastureGrazing()
    {
        if (_targetCell != null)
        {
            if (_isPastureGrazingRegistered)
            {
                _targetCell.StopGrazing();
            }
            else if (_hasGrassReservation)
            {
                _targetCell.CancelReservation();
            }
        }

        _targetCell = null;
        _isPastureGrazing = false;
        _isPastureGrazingRegistered = false;
        _hasGrassReservation = false;
        _pastureGrazeTimer = 0f;
    }

    FoodSource FindBestFood()
    {
        FoodSource best = null;
        float bestScore = -1f;

        foreach (var food in _flock.foodSources)
        {
            if (food == null || !food.IsAvailable) continue;

            float d = Vector3.Distance(transform.position, food.transform.position);

            if (d > food.detectionRadius) continue;

            float score = food.attractionPriority / Mathf.Max(d, 0.1f);

            if (score > bestScore)
            {
                bestScore = score;
                best = food;
            }
        }

        return best;
    }

    Vector3 CalculateFlowerAttraction(float flowerBiasFactor)
    {
        FoodSource best = null;
        float bestScore = -1f;

        foreach (var food in _flock.foodSources)
        {
            if (food == null || !food.IsAvailable) continue;
            if (food.attractionPriority < 2f) continue;

            float d = Vector3.Distance(transform.position, food.transform.position);

            if (d > food.detectionRadius) continue;

            float score = food.attractionPriority / Mathf.Max(d, 0.1f);

            if (score > bestScore)
            {
                bestScore = score;
                best = food;
            }
        }

        if (best == null) return Vector3.zero;

        float dist = Vector3.Distance(transform.position, best.transform.position);
        Vector3 toFood = (best.transform.position - transform.position).normalized;
        float proximity = Mathf.InverseLerp(best.detectionRadius, 0f, dist);

        return toFood * proximity * flowerBiasFactor * best.attractionPriority;
    }

    void MoveToFood(FoodSource food)
    {
        if (!food.TryOccupy()) return;

        _targetFood = food;

        if (NavMesh.SamplePosition(food.transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            _agent.isStopped = false;
            _agent.speed = _flock.idleWanderSpeed * 1.5f;
            _agent.SetDestination(hit.position);
        }
    }

    void EatingUpdate()
    {
        _eatTimer += Time.deltaTime;

        if (_eatTimer >= _eatDuration)
        {
            if (_targetFood != null)
            {
                _targetFood.Release();
                _targetFood = null;
            }

            _isEating = false;
            _eatTimer = 0f;
            _idleWanderCooldown = _flock.idleWanderInterval;
        }
    }

    void SetFlocking()
    {
        CurrentState = SheepState.Flocking;

        if (AgentReady)
        {
            _agent.isStopped = false;
        }
    }

    void FlockingUpdate()
    {
        if (_isEating)
        {
            EatingUpdate();
            return;
        }

        if (_targetFood != null)
        {
            float distToFood = Vector3.Distance(transform.position, _targetFood.transform.position);
            bool arrived = distToFood < 1.5f || (!_agent.pathPending && _agent.remainingDistance < 1.5f);

            if (arrived)
            {
                _isEating = true;
                _eatTimer = 0f;
                _eatDuration = Random.Range(2f, 5f);
                _agent.isStopped = true;
                return;
            }
        }

        _agent.speed = Mathf.Lerp(_flock.minSpeed, _flock.maxSpeed, Arousal);

        Vector3 boidForce = CalculateBoids();
        Vector3 gravityForce = CalculateFlockGravity(false);
        Vector3 antiSplit = CalculateAntiSplit();

        float flowerBias = Mathf.Lerp(0.4f, 0f, Arousal);
        Vector3 flowerForce = CalculateFlowerAttraction(flowerBias);

        Vector3 combined = boidForce + gravityForce + antiSplit + flowerForce;

        if (combined.sqrMagnitude < 0.001f)
        {
            combined = transform.forward;
        }

        Vector3 target = transform.position + combined.normalized * 2f;

        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            _agent.SetDestination(hit.position);
        }

        FoodSource urgentFood = FindBestFood();

        if (urgentFood != null && Arousal < 0.3f && _targetFood == null)
        {
            MoveToFood(urgentFood);
        }
    }

    void SetFleeing()
    {
        CurrentState = SheepState.Fleeing;

        if (_targetFood != null)
        {
            _targetFood.Release();
            _targetFood = null;
        }

        _isEating = false;
        StopPastureGrazing();

        if (AgentReady)
        {
            _agent.isStopped = false;
            _agent.speed = _flock.maxSpeed * _flock.panicSpeedMultiplier;
        }
    }

    void FleeingUpdate()
    {
        Vector3 fleeDir = Vector3.zero;

        if (_externalThreat != null)
        {
            Vector3 threatPosition = _externalThreat.position;
            fleeDir = transform.position - threatPosition;
        }
        else if (_socialPanicDirection != Vector3.zero)
        {
            fleeDir = _socialPanicDirection;
        }
        else
        {
            Vector3 threatPosition = _flock.playerTransform.position;
            fleeDir = transform.position - threatPosition;
        }

        fleeDir.y = 0f;

        if (fleeDir.sqrMagnitude < 0.001f)
        {
            fleeDir = transform.forward;
        }

        fleeDir.Normalize();

        Vector3 boidForce = CalculateBoids();
        Vector3 gravityForce = CalculateFlockGravity(true);
        Vector3 antiSplit = CalculateAntiSplit();

        float flowerBias = Mathf.Lerp(0.15f, 0f, Arousal);
        Vector3 flowerForce = CalculateFlowerAttraction(flowerBias);

        float panicFactor = Mathf.InverseLerp(0.3f, 1f, Arousal);
        float dynFleeWeight = _flock.fleeWeight * Arousal * Mathf.Lerp(1f, 1.8f, panicFactor);

        Vector3 panicScatter = Vector3.zero;

        if (Arousal > 0.6f)
        {
            Vector3 awayFromCenter = transform.position - _flock.FlockCentroid;
            awayFromCenter.y = 0f;

            if (awayFromCenter.sqrMagnitude > 0.01f)
            {
                panicScatter = awayFromCenter.normalized * _flock.panicDispersionMultiplier;
            }
        }

        Vector3 combined = fleeDir * dynFleeWeight
                         + panicScatter
                         + boidForce
                         + gravityForce
                         + antiSplit
                         + flowerForce;

        if (combined.sqrMagnitude < 0.001f)
        {
            combined = fleeDir;
        }

        Vector3 target = transform.position + combined.normalized * 5f;

        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            _agent.SetDestination(hit.position);
        }
    }

    Vector3 CalculateAntiSplit()
    {
        if (Arousal < _flock.antiSplitArousalThreshold) return Vector3.zero;

        float dist = Vector3.Distance(transform.position, _flock.FlockCentroid);

        if (dist < _flock.antiSplitRadius) return Vector3.zero;

        Vector3 toCenter = (_flock.FlockCentroid - transform.position).normalized;

        float distanceFactor = Mathf.InverseLerp(
            _flock.antiSplitRadius,
            _flock.antiSplitRadius * 2f,
            dist
        );

        float arousalFactor = Mathf.InverseLerp(
            _flock.antiSplitArousalThreshold,
            1f,
            Arousal
        );

        return toCenter * _flock.antiSplitWeight * distanceFactor * arousalFactor;
    }

    Vector3 CalculateFlockGravity(bool panicMode)
    {
        if (_isIdleWandering) return Vector3.zero;

        Vector3 centroid = _flock.FlockCentroid;
        float dist = Vector3.Distance(transform.position, centroid);

        if (dist < _flock.flockGravityRadius) return Vector3.zero;

        Vector3 toCenter = (centroid - transform.position).normalized;

        float distanceFactor = Mathf.InverseLerp(
            _flock.flockGravityRadius,
            _flock.flockGravityRadius * 3f,
            dist
        );

        distanceFactor = Mathf.Max(distanceFactor, _flock.flockGravityFalloff);

        float panicFactor = 1f;

        if (panicMode)
        {
            panicFactor = Mathf.Lerp(1f, _flock.flockGravityPanicMult, Arousal);
        }
        else if (Arousal > _flock.postPanicArousalThreshold)
        {
            panicFactor = _flock.postPanicCohesionMultiplier;
        }

        float spreadFactor = Mathf.Clamp(
            _flock.FlockSpreadRadius / (_flock.flockGravityRadius * 2f),
            1f,
            2f
        );

        return toCenter * _flock.flockGravityWeight * distanceFactor * panicFactor * spreadFactor;
    }

    Vector3 CalculateBoids()
    {
        float cohesionMult = _isIdleWandering ? 0f : 1f;
        float alignmentMult = _isIdleWandering ? 0.2f : 1f;

        Vector3 sep = Vector3.zero;
        Vector3 ali = Vector3.zero;
        Vector3 coh = Vector3.zero;

        float totalInfluence = 0f;

        float perception = Mathf.Lerp(
            _flock.perceptionRadiusMin,
            _flock.perceptionRadiusMax,
            Arousal
        );

        int densityCount = 0;
        float expectedDensity = _flock.flockSize * 0.3f;

        foreach (var other in _flock.allSheep)
        {
            if (other == null || other == this) continue;

            float d = Vector3.Distance(transform.position, other.transform.position);

            if (d > perception) continue;

            float influence = 1f + other.Arousal;
            totalInfluence += influence;

            coh += other.transform.position * influence;
            ali += other.AgentVelocity * influence;

            if (d < _flock.separationRadius)
            {
                sep += (transform.position - other.transform.position) / Mathf.Max(d, 0.01f);
            }

            if (d < _flock.densityStressRadius)
            {
                densityCount++;
            }
        }

        if (totalInfluence == 0f)
        {
            return transform.forward;
        }

        coh = (coh / totalInfluence - transform.position).normalized * _flock.cohesionWeight * cohesionMult;
        ali = (ali / totalInfluence).normalized * _flock.alignmentWeight * alignmentMult;

        float panicFactor = Mathf.InverseLerp(0.3f, 1f, Arousal);

        float separationMultiplier = CurrentState == SheepState.Fleeing
            ? Mathf.Lerp(1f, _flock.panicSeparationMultiplier, panicFactor)
            : 1f;

        sep = sep.normalized * _flock.separationWeight * separationMultiplier;

        Vector3 steering = sep + ali + coh;

        float localDensity = densityCount / Mathf.Max(1f, expectedDensity);
        float panicMult = Mathf.Lerp(1f, _flock.densityStressPanicMult, Arousal);
        float effectiveThresh = _flock.densityStressThreshold * panicMult;

        if (localDensity > effectiveThresh)
        {
            Vector3 crowdCenter = GetLocalCrowdCenter(_flock.densityStressRadius);
            Vector3 disperseDir = (transform.position - crowdCenter).normalized;
            float intensity = Mathf.InverseLerp(effectiveThresh, 1f, localDensity);

            float dispersionMultiplier = CurrentState == SheepState.Fleeing
                ? Mathf.Lerp(1f, _flock.panicDispersionMultiplier, panicFactor)
                : 1f;

            steering += disperseDir * _flock.dispersionWeight * intensity * dispersionMultiplier;
        }

        return steering.normalized;
    }

    Vector3 GetLocalCrowdCenter(float radius)
    {
        Vector3 center = Vector3.zero;
        int count = 0;

        foreach (var sheep in _flock.allSheep)
        {
            if (sheep == null || sheep == this) continue;

            if (Vector3.Distance(transform.position, sheep.transform.position) < radius)
            {
                center += sheep.transform.position;
                count++;
            }
        }

        return count > 0 ? center / count : transform.position;
    }

    public Vector3 AgentVelocity => AgentReady ? _agent.velocity : Vector3.zero;

    void UpdateAnimator()
    {
        if (_animator == null || !AgentReady) return;

        _animator.SetFloat("Speed", _agent.velocity.magnitude);
        _animator.SetFloat("Arousal", Arousal);
        _animator.SetBool("IsFleeing", CurrentState == SheepState.Fleeing);
        _animator.SetBool("IsGrazing", CurrentState == SheepState.Grazing);
        _animator.SetBool("IsIdleWandering", _isIdleWandering);
        _animator.SetBool("IsEating", _isEating);
        _animator.SetBool("IsPastureGrazing", _isPastureGrazing);
    }
}

public static class Vector2Ext
{
    public static Vector3 ToVector3XZ(this Vector2 v)
    {
        return new Vector3(v.x, 0f, v.y);
    }
}