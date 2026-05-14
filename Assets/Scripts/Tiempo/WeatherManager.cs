using UnityEngine;

public class WeatherManager : MonoBehaviour
{
    public static WeatherManager Instance { get; private set; }

    public enum WeatherType
    {
        Clear,
        Wind,
        Rain
    }

    [Header("Weather State")]
    public WeatherType currentWeather = WeatherType.Clear;

    [Header("Wind")]
    public Vector3 windDirection = new Vector3(1f, 0f, 0f);
    public float windStrength = 1.5f;

    [Header("Wind Gust")]
    public bool useWindGusts = true;
    public float gustInterval = 6f;
    public float gustDuration = 1.2f;
    public float gustStrengthMultiplier = 4f;

    [Header("Debug")]
    public bool showWindGizmo = true;
    public float gizmoLength = 5f;

    private float _gustIntervalTimer;
    private float _gustDurationTimer;
    private bool _isGusting;

    public bool HasWind => currentWeather == WeatherType.Wind && windStrength > 0f;
    public bool IsGusting => HasWind && _isGusting;

    public Vector3 WindDirectionNormalized
    {
        get
        {
            Vector3 dir = windDirection;
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.001f)
            {
                return Vector3.zero;
            }

            return dir.normalized;
        }
    }

    public Vector3 WindForce
    {
        get
        {
            if (!HasWind) return Vector3.zero;

            float multiplier = IsGusting ? gustStrengthMultiplier : 1f;
            return WindDirectionNormalized * windStrength * multiplier;
        }
    }

    private void Awake()
    {
        Instance = this;
        _gustIntervalTimer = gustInterval;
    }

    private void Update()
    {
        if (!HasWind || !useWindGusts)
        {
            _isGusting = false;
            return;
        }

        if (_isGusting)
        {
            _gustDurationTimer -= Time.deltaTime;

            if (_gustDurationTimer <= 0f)
            {
                _isGusting = false;
                _gustIntervalTimer = gustInterval;
            }
        }
        else
        {
            _gustIntervalTimer -= Time.deltaTime;

            if (_gustIntervalTimer <= 0f)
            {
                _isGusting = true;
                _gustDurationTimer = gustDuration;
            }
        }

        if (HasWind)
        {
            Debug.DrawRay(transform.position, WindForce, Color.cyan);
        }
    }

    private void OnDrawGizmos()
    {
        if (!showWindGizmo) return;

        Vector3 dir = windDirection;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + dir.normalized * gizmoLength);
        Gizmos.DrawSphere(transform.position + dir.normalized * gizmoLength, 0.25f);
    }
}