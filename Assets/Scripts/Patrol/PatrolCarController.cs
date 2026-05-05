using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

[RequireComponent(typeof(Rigidbody))]
public class PatrolCarController : MonoBehaviour
{
    [Header("Motor")]
    [SerializeField] float maxSpeedForward = 10f;
    [SerializeField] float accelerationDecel = 8f;
    [SerializeField] float brakingDecel = 10f;
    [SerializeField] float engineBrakingDecel = 1.5f;

    [Header("Steering")]
    [SerializeField] float maxTorque = 150f;
    [SerializeField] float steeringSpeedFactor = 0.15f;
    [SerializeField] float minSteerMultiplier = 0.15f;
    [SerializeField] float minSteerSpeed = 1.5f; // velocidad mínima para poder girar
    [SerializeField] float noTurnAtRestSpeed = 0.35f; // evita girar en su propio eje estando casi quieto
    [SerializeField] float lowSpeedSteerMultiplier = 0.5f; // reduce giro brusco a muy baja velocidad
    [SerializeField] float coastingSteerMultiplier = 0.6f; // reduce giro al soltar acelerador
    [SerializeField] float steerTorqueSmoothing = 6f; // suaviza transiciones bruscas de torque de giro

    [Header("Tracción lateral")]
    [SerializeField] float lateralFriction = 20f;
    [SerializeField] float angularDamping = 25f;

    [Header("Inclinación visual")]
    [SerializeField] Transform carBody;
    [SerializeField] float maxPitchAngle = 8f;
    [SerializeField] float maxRollAngle = 12f;
    [SerializeField] float pitchFactor = 0.35f;
    [SerializeField] float rollFactor = 1.2f;
    [SerializeField] float tiltSmoothness = 8f;

    [Header("Polvo de tierra")]
    [SerializeField] ParticleSystem dustLeft;
    [SerializeField] ParticleSystem dustRight;
    [SerializeField] float dustMinSpeed = 2.5f;
    [SerializeField] float dustMaxRate = 45f;
    [SerializeField] float accelDustWeight = 1.1f;
    [SerializeField] float accelDustMaxSpeed = 4f;
    [SerializeField] float turnDustWeight = 0.7f;
    [SerializeField] float slipDustWeight = 1.2f;

    [Header("Marchas")]
    [SerializeField] float[] gearSpeeds = { 5f, 10f, 17f, 25f, 35f };
    [SerializeField] TextMeshProUGUI gearText;
    int currentGear = 0;

    
    [SerializeField] InputActionAsset inputActions;

    Rigidbody rb;
    InputAction moveAction;
    float groundedTimer; // segundos desde el último contacto con suelo
    const float groundedGrace = 0.12f; // margen de tiempo antes de considerar en el aire
    bool IsGrounded => groundedTimer > 0f;

    float prevLocalVelZ;
    float currentLongAccel;
    float currentThrottle;
    float currentSteer;
    float currentSpeed;
    float currentLateralSpeed;
    float smoothedSteerTorque;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.3f, 0);

        var playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);
        moveAction = playerMap.FindAction("Move", throwIfNotFound: true);
        playerMap.Enable();
    }

    void Update()
    {
        if (Keyboard.current.eKey.wasPressedThisFrame)
            currentGear = Mathf.Min(currentGear + 1, gearSpeeds.Length - 1);
        if (Keyboard.current.qKey.wasPressedThisFrame)
            currentGear = Mathf.Max(currentGear - 1, 0);

        if (gearText != null)
            gearText.text = $"MARCHA  {currentGear + 1}";
    }

    void FixedUpdate()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        float throttle = input.y;
        float steer = input.x;

        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float localVelZ = localVelocity.z;
        float speed = rb.linearVelocity.magnitude;

        currentThrottle = throttle;
        currentSteer = steer;
        currentSpeed = speed;
        currentLateralSpeed = Mathf.Abs(localVelocity.x);

        currentLongAccel = (localVelZ - prevLocalVelZ) / Time.fixedDeltaTime;
        prevLocalVelZ = localVelZ;

        groundedTimer -= Time.fixedDeltaTime;

        ApplyDrive(throttle, localVelZ);
        ApplySteering(steer, speed, throttle);
        ApplyLateralFriction();
        ApplyAngularDamping();
    }

    void OnCollisionStay(Collision col) { groundedTimer = groundedGrace; }
    void OnCollisionExit(Collision col) { } // el timer se encarga solo

    void ApplyDrive(float throttle, float localVelZ)
    {
        // En el aire no se puede acelerar, frenar ni ir en reversa
        if (!IsGrounded) return;

        bool pressingS = throttle < -0.1f;
        bool pressingW = throttle > 0.1f;

        if (pressingW)
        {
            float target = localVelZ < -0.15f ? 0f : gearSpeeds[currentGear];
            float rate   = localVelZ < -0.15f ? brakingDecel : accelerationDecel;
            SetForwardVelocity(Mathf.MoveTowards(localVelZ, target, rate * Time.fixedDeltaTime));
            return;
        }

        if (pressingS)
        {
            SetForwardVelocity(Mathf.MoveTowards(localVelZ, 0f, brakingDecel * Time.fixedDeltaTime));
            return;
        }

        SetForwardVelocity(Mathf.MoveTowards(localVelZ, 0f, engineBrakingDecel * Time.fixedDeltaTime));
    }

    // Aplica una velocidad local en Z preservando la Y mundial (gravedad + colinas)
    void SetForwardVelocity(float newLocalZ)
    {
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        localVel.z = newLocalZ;
        Vector3 worldVel = transform.TransformDirection(localVel);
        worldVel.y = rb.linearVelocity.y; // la gravedad y la fisica del terreno mandan en Y
        rb.linearVelocity = worldVel;
    }

    void ApplySteering(float steer, float speed, float throttle)
    {
        if (Mathf.Abs(steer) < 0.05f || speed < noTurnAtRestSpeed)
        {
            // Sin input de giro o parado: desvanece el torque suavemente hasta cero
            smoothedSteerTorque = Mathf.Lerp(smoothedSteerTorque, 0f, steerTorqueSmoothing * Time.fixedDeltaTime);
            if (Mathf.Abs(smoothedSteerTorque) > 0.5f)
                rb.AddTorque(transform.up * smoothedSteerTorque, ForceMode.Force);
            else
                smoothedSteerTorque = 0f;
            return;
        }

        float moveFactor = Mathf.InverseLerp(minSteerSpeed, minSteerSpeed + 2f, speed);
        if (currentGear == 0)
        {
            float firstGearMinMove = Mathf.Lerp(
                0.08f,
                0.35f,
                Mathf.InverseLerp(noTurnAtRestSpeed, minSteerSpeed, speed)
            );
            moveFactor = Mathf.Max(moveFactor, firstGearMinMove); // en 1ª permite giro sin ser brusco al arrancar
        }
        else if (moveFactor < 0.01f)
            return;

        float speedRatio = Mathf.Clamp01(speed * steeringSpeedFactor);
        float steerMult  = Mathf.Lerp(1f, minSteerMultiplier, speedRatio);

        float lowSpeedRatio = Mathf.InverseLerp(noTurnAtRestSpeed, minSteerSpeed + 1.5f, speed);
        float lowSpeedMult = Mathf.Lerp(lowSpeedSteerMultiplier, 1f, lowSpeedRatio);

        float throttleRatio = Mathf.InverseLerp(0.05f, 0.35f, Mathf.Abs(throttle));
        float throttleMult = Mathf.Lerp(coastingSteerMultiplier, 1f, throttleRatio);

        // Solo en 1ª marcha se reduce el torque para evitar giro excesivo a baja velocidad
        float gearTorqueMult = currentGear == 0 ? 0.65f : 1f;

        float targetTorque = steer * maxTorque * steerMult * moveFactor * gearTorqueMult * lowSpeedMult * throttleMult;
        smoothedSteerTorque = Mathf.Lerp(smoothedSteerTorque, targetTorque, steerTorqueSmoothing * Time.fixedDeltaTime);

        rb.AddTorque(transform.up * smoothedSteerTorque, ForceMode.Force);
    }

    void LateUpdate()
    {
        UpdateBodyTilt();
        UpdateDustEffects();
    }

    void UpdateBodyTilt()
    {
        if (carBody == null) return;

        float localVelZ = transform.InverseTransformDirection(rb.linearVelocity).z;
        float turnRate  = rb.angularVelocity.y;

        float targetPitch = Mathf.Clamp(-currentLongAccel * pitchFactor, -maxPitchAngle, maxPitchAngle);
        float targetRoll  = Mathf.Clamp(-turnRate * Mathf.Abs(localVelZ) * rollFactor, -maxRollAngle, maxRollAngle);

        Quaternion targetRot = Quaternion.Euler(targetPitch, 0f, targetRoll);
        carBody.localRotation = Quaternion.Lerp(carBody.localRotation, targetRot, tiltSmoothness * Time.deltaTime);
    }

    void UpdateDustEffects()
    {
        if (dustLeft == null && dustRight == null) return;

        float accelFade   = 1f - Mathf.Clamp01(currentSpeed / accelDustMaxSpeed);
        float accelAmount = Mathf.Clamp01(Mathf.Abs(currentThrottle)) * accelFade;
        float slipAmount  = Mathf.Clamp01(currentLateralSpeed / 2.5f);
        float speedFactor = Mathf.InverseLerp(dustMinSpeed, maxSpeedForward, currentSpeed);

        float baseIntensity = (accelAmount * accelDustWeight + slipAmount * slipDustWeight) * speedFactor;

        float turnIntensity = Mathf.Clamp01(Mathf.Abs(currentSteer)) * turnDustWeight * speedFactor;
        float leftTurn  = Mathf.Clamp01(-currentSteer);
        float rightTurn = Mathf.Clamp01( currentSteer);

        SetDustEmitter(dustLeft,  Mathf.Clamp01(baseIntensity + turnIntensity * leftTurn));
        SetDustEmitter(dustRight, Mathf.Clamp01(baseIntensity + turnIntensity * rightTurn));
    }

    void SetDustEmitter(ParticleSystem emitter, float intensity)
    {
        if (emitter == null) return;
        var emission = emitter.emission;
        emission.rateOverTimeMultiplier = dustMaxRate * intensity;
        if (intensity > 0.02f)
        {
            if (!emitter.isPlaying) emitter.Play();
        }
        else if (emitter.isPlaying)
        {
            emitter.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    void ApplyLateralFriction()
    {
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        rb.AddForce(-transform.right * localVel.x * lateralFriction, ForceMode.Force);
    }

    void ApplyAngularDamping()
    {
        rb.angularVelocity = Vector3.Lerp(
            rb.angularVelocity,
            Vector3.zero,
            angularDamping * Time.fixedDeltaTime
        );
    }
}