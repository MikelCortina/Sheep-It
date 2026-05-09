using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerModeCoordinator : MonoBehaviour
{
    [SerializeField] private PatrolCarController carController;
    [SerializeField] private OnFootController onFootController;
    [SerializeField] private VehicleSeat vehicleSeat;
    [SerializeField] private CarCamera topDownCamera;
    [SerializeField] private FlockManager flockManager;

    private bool isInVehicle = true;
    private float exitCooldown = 0f;
    private const float EXIT_COOLDOWN_TIME = 0.5f;

    void Awake()
    {
        if (carController == null)
            carController = FindObjectOfType<PatrolCarController>();
        if (onFootController == null)
            onFootController = FindObjectOfType<OnFootController>(true);
        if (vehicleSeat == null)
            vehicleSeat = FindObjectOfType<VehicleSeat>();
        if (topDownCamera == null)
            topDownCamera = FindObjectOfType<CarCamera>();
        if (flockManager == null)
            flockManager = FindObjectOfType<FlockManager>();

        // Validar referencias
        if (carController == null) Debug.LogError("[PlayerModeCoordinator] No se encontró PatrolCarController");
        if (onFootController == null) Debug.LogError("[PlayerModeCoordinator] No se encontró OnFootController");
        if (vehicleSeat == null) Debug.LogError("[PlayerModeCoordinator] No se encontró VehicleSeat");
        if (topDownCamera == null) Debug.LogError("[PlayerModeCoordinator] No se encontró TopDownCamera");
    }

    void Start()
    {
        Debug.Log("[PlayerModeCoordinator] Iniciado correctamente.");
        SetMode(true);
    }

    void Update()
    {
        exitCooldown -= Time.deltaTime;

        // F para entrar/salir — igual que E/Q para marchas en PatrolCarController
        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            Debug.Log($"[PlayerModeCoordinator] F pulsada. isInVehicle={isInVehicle}, cooldown={exitCooldown:F2}");

            if (isInVehicle)
            {
                float speed = carController != null ? carController.GetSpeed() : 0f;
                Debug.Log($"[PlayerModeCoordinator] Velocidad del coche: {speed:F1}");
                if (speed < 2f)
                    ExitVehicle();
                else
                    Debug.Log("[PlayerMode] Para el coche antes de salir.");
            }
            else
            {
                bool cerca = vehicleSeat != null && vehicleSeat.IsPlayerNear(onFootController.transform);
                Debug.Log($"[PlayerModeCoordinator] Cerca del coche: {cerca}");
                if (cerca)
                    EnterVehicle();
                else
                    Debug.Log("[PlayerMode] Acércate más al coche para entrar.");
            }
        }
    }

    void EnterVehicle()
    {
        if (isInVehicle || exitCooldown > 0f) return;

        onFootController.transform.position = vehicleSeat.GetEnterPosition();
        SetMode(true);
        Debug.Log("[PlayerMode] ✓ Entrando al coche.");
    }

    void ExitVehicle()
    {
        if (!isInVehicle || exitCooldown > 0f) return;

        Vector3 exitPos = vehicleSeat.GetExitPosition();

        // Comprobar espacio ignorando el propio coche
        Collider[] hits = Physics.OverlapSphere(exitPos, 0.6f);
        foreach (var hit in hits)
        {
            if (hit.transform != carController.transform && !hit.transform.IsChildOf(carController.transform))
            {
                Debug.Log($"[PlayerMode] Salida bloqueada por: {hit.gameObject.name}");
                return;
            }
        }

        SetMode(false);
        onFootController.transform.position = exitPos;
        exitCooldown = EXIT_COOLDOWN_TIME;
        Debug.Log($"[PlayerMode] ✓ Saliendo del coche en {exitPos}");
    }

    void SetMode(bool inVehicle)
    {
        isInVehicle = inVehicle;

        if (isInVehicle)
        {
            carController.enabled = true;
            onFootController.gameObject.SetActive(false);
            topDownCamera.SetTarget(carController.transform);
            if (flockManager != null) flockManager.SetPlayerTarget(carController.transform);
        }
        else
        {
            carController.enabled = false;
            onFootController.gameObject.SetActive(true);
            topDownCamera.SetTarget(onFootController.transform);
            if (flockManager != null) flockManager.SetPlayerTarget(onFootController.transform);
        }
    }

    public bool IsInVehicle => isInVehicle;
}