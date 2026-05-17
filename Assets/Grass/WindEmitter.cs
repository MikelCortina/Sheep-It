using UnityEngine;

public class WindEmitter : MonoBehaviour
{
    [Header("Dirección y fuerza")]
    public Vector3 windDirection = new Vector3(1f, 0f, 0.3f);
    public float windStrength = 0.4f;
    public float windSpeed = 6f;

    [Header("Ráfagas")]
    public float gustInterval = 3f;
    public float gustDuration = 1.2f;
    public float gustStrengthMul = 2.2f;
    public float gustSpread = 15f;

    [Header("Turbulencia base")]
    public float baseFrequency = 0.6f;
    public float turbulence = 0.15f;

    float _gustTimer;
    float _gustProgress;
    Vector3 _currentGustDir;
    float _currentGustStr;

    static readonly int ID_WindDir = Shader.PropertyToID("_GlobalWindDir");
    static readonly int ID_WindStr = Shader.PropertyToID("_GlobalWindStrength");
    static readonly int ID_WindOrigin = Shader.PropertyToID("_GlobalWindOrigin");
    static readonly int ID_WindSpeed = Shader.PropertyToID("_GlobalWindSpeed");
    static readonly int ID_WindFreq = Shader.PropertyToID("_GlobalWindFrequency");
    static readonly int ID_Turb = Shader.PropertyToID("_GlobalTurbulence");

    void OnEnable()
    {
        _gustProgress = -1f;
        _gustTimer = gustInterval * 0.5f;

        // ? Inicializar globals con valores seguros antes del primer Update
        Vector3 dir = windDirection.sqrMagnitude > 0.001f
                      ? windDirection.normalized
                      : Vector3.right;

        Shader.SetGlobalVector(ID_WindDir, new Vector4(dir.x, dir.y, dir.z, 0));
        Shader.SetGlobalFloat(ID_WindStr, windStrength);
        Shader.SetGlobalVector(ID_WindOrigin, new Vector4(transform.position.x,
                                                           transform.position.y,
                                                           transform.position.z, 0));
        Shader.SetGlobalFloat(ID_WindSpeed, windSpeed);
        Shader.SetGlobalFloat(ID_WindFreq, baseFrequency);
        Shader.SetGlobalFloat(ID_Turb, turbulence);
    }

    void Update()
    {
        // ?? Temporizador de ráfaga ???????????????????
        _gustTimer += Time.deltaTime;
        if (_gustTimer >= gustInterval && _gustProgress < 0f)
        {
            _gustTimer = 0f;
            _gustProgress = 0f;

            float spread = Random.Range(-gustSpread, gustSpread);
            _currentGustDir = (Quaternion.Euler(0, spread, 0) * windDirection.normalized);
            _currentGustStr = windStrength * gustStrengthMul;
        }

        // ?? Envolvente de ráfaga: sin²(?·t) ?????????
        float activeStrength = windStrength;
        Vector3 activeDir = windDirection.normalized;

        if (_gustProgress >= 0f)
        {
            _gustProgress += Time.deltaTime / gustDuration;

            float envelope = Mathf.Sin(_gustProgress * Mathf.PI);
            envelope = envelope * envelope;

            activeStrength = Mathf.Lerp(windStrength, _currentGustStr, envelope);
            activeDir = Vector3.Lerp(windDirection.normalized, _currentGustDir, envelope * 0.4f);

            if (_gustProgress >= 1f)
                _gustProgress = -1f;
        }

        // ?? Turbulencia de micro-variación ??????????
        float turb = (Mathf.PerlinNoise(Time.time * 1.3f, 0.5f) * 2f - 1f) * turbulence;
        activeStrength = Mathf.Max(0f, activeStrength + turb);

        // ?? Enviar al shader ?????????????????????????
        Vector3 d = activeDir.sqrMagnitude > 0.001f ? activeDir.normalized : Vector3.right;
        Shader.SetGlobalVector(ID_WindDir, new Vector4(d.x, d.y, d.z, 0));
        Shader.SetGlobalFloat(ID_WindStr, activeStrength);
        Shader.SetGlobalVector(ID_WindOrigin, new Vector4(transform.position.x,
                                                           transform.position.y,
                                                           transform.position.z, 0));
        Shader.SetGlobalFloat(ID_WindSpeed, windSpeed);
        Shader.SetGlobalFloat(ID_WindFreq, baseFrequency);
        Shader.SetGlobalFloat(ID_Turb, turbulence);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 0.4f);
        Gizmos.DrawRay(transform.position, windDirection.normalized * 2f);

        Vector3 left = Quaternion.Euler(0, gustSpread, 0) * windDirection.normalized;
        Vector3 right = Quaternion.Euler(0, -gustSpread, 0) * windDirection.normalized;
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.3f);
        Gizmos.DrawRay(transform.position, left * 1.5f);
        Gizmos.DrawRay(transform.position, right * 1.5f);
    }
}