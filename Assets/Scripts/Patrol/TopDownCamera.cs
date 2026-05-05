using UnityEngine;

public class TopDownCamera : MonoBehaviour
{
    [Header("Objetivo")]
    [SerializeField] Transform target;

    [Header("Posición")]
    [SerializeField] float heightAbove = 14f;       // altura sobre el coche
    [SerializeField] float distanceBehind = 8f;     // distancia detrás del coche
    [SerializeField] float followSmoothness = 6f;   // qué rápido sigue la posición

    [Header("Rotación")]
    [SerializeField] float rotationSmoothness = 4f; // qué rápido gira detrás del coche
    [SerializeField] float tiltAngle = 55f;         // inclinación (90=cenital, 50-60=semitrasera)

    Quaternion currentRotation;

    void Start()
    {
        if (target != null)
            currentRotation = Quaternion.Euler(tiltAngle, target.eulerAngles.y, 0);
        else
            currentRotation = Quaternion.Euler(tiltAngle, 0, 0);

        transform.rotation = currentRotation;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Rotación objetivo: apunta desde detrás del coche con la inclinación elegida
        Quaternion targetRotation = Quaternion.Euler(tiltAngle, target.eulerAngles.y, 0);
        currentRotation = Quaternion.Slerp(currentRotation, targetRotation, rotationSmoothness * Time.deltaTime);

        // Posición: detrás y arriba en el espacio local del coche
        Vector3 back = currentRotation * Vector3.back;
        Vector3 desiredPos = target.position + back * distanceBehind + Vector3.up * heightAbove;

        transform.position = Vector3.Lerp(transform.position, desiredPos, followSmoothness * Time.deltaTime);
        transform.rotation = currentRotation;
    }
}