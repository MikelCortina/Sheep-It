using UnityEngine;

public class VehicleSeat : MonoBehaviour
{
    [SerializeField] private Transform vehicle;
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private Vector3 enterLocalOffset = new Vector3(0, 1f, -0.5f);
    [SerializeField] private Vector3 exitLocalOffset = new Vector3(1.5f, 0, 0);

    void OnDrawGizmosSelected()
    {
        if (vehicle == null)
            vehicle = GetComponent<Transform>();

        // Visualizar punto de entrada
        Vector3 enterPos = vehicle.position + vehicle.TransformDirection(enterLocalOffset);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(enterPos, 0.5f);
        Gizmos.DrawLine(vehicle.position, enterPos);

        // Visualizar punto de salida
        Vector3 exitPos = vehicle.position + vehicle.TransformDirection(exitLocalOffset);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(exitPos, 0.5f);
        Gizmos.DrawLine(vehicle.position, exitPos);

        // Radio de interacción
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(vehicle.position, interactionDistance);
    }

    public bool IsPlayerNear(Transform playerTransform)
    {
        float distance = Vector3.Distance(playerTransform.position, vehicle.position);
        return distance < interactionDistance;
    }

    public Vector3 GetEnterPosition()
    {
        return vehicle.position + vehicle.TransformDirection(enterLocalOffset);
    }

    public Vector3 GetExitPosition()
    {
        Vector3 baseExit = vehicle.position + vehicle.TransformDirection(exitLocalOffset);
        
        // Buscar punto seguro dentro de un radio alrededor del punto de salida
        for (int i = 0; i < 8; i++)
        {
            float angle = i * (360f / 8f) * Mathf.Deg2Rad;
            Vector3 testPos = baseExit + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 1.5f;
            
            if (!Physics.CheckSphere(testPos, 0.5f))
                return testPos;
        }
        
        return baseExit;
    }
}