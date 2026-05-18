using UnityEngine;
using Unity.Cinemachine;

public class CameraManager : MonoBehaviour
{
    [Header("Referencias")]
    public CinemachineCamera vCam;
    
    [Header("Configuración de Distancia (Radius)")]
    public float radioApie = 5f;
    public float radioCoche = 12f;
    public float suavizadoZoom = 3f;

    private CinemachineOrbitalFollow orbital;
    private float radioObjetivo;

    void Start()
    {
        // Cogemos el componente que vemos en tu imagen
        orbital = vCam.GetComponent<CinemachineOrbitalFollow>();
        radioObjetivo = radioCoche;
    }

    void Update()
    {
        // Zoom suave modificando el Radius
        orbital.Radius = Mathf.Lerp(orbital.Radius, radioObjetivo, Time.deltaTime * suavizadoZoom);
    }

    // Llama a este método desde el script de tu coche al subir/bajar
    public void CambiarModo(bool enCoche)
    {
        radioObjetivo = enCoche ? radioCoche : radioApie;
    }
}