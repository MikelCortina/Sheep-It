using UnityEngine;

public class GrassInteractor : MonoBehaviour
{
    // Radio de influencia en metros
    public float radius = 0.8f;

    // Fuerza del aplastamiento: 1 = completamente plano, 0 = sin efecto
    public float strength = 1f;

    // Registro global de todos los interactores activos en escena
    static readonly Vector4[] _interactors = new Vector4[10];
    static int _interactorCount = 0;
    static readonly int ID_Interactors = Shader.PropertyToID("_GrassInteractors");
    static readonly int ID_InteractorCount = Shader.PropertyToID("_GrassInteractorCount");

    int _myIndex = -1;

    void OnEnable()
    {
        if (_interactorCount >= 10) return;
        _myIndex = _interactorCount++;
    }

    void OnDisable()
    {
        // Al desactivarse libera su slot poniendo radio 0
        if (_myIndex >= 0 && _myIndex < 10)
        {
            _interactors[_myIndex] = Vector4.zero;
            _interactorCount = Mathf.Max(0, _interactorCount - 1);
            _myIndex = -1;
        }
    }

    void LateUpdate()
    {
        if (_myIndex < 0) return;

        // XYZ = posición world, W = radio * strength (el shader lo usa todo en uno)
        _interactors[_myIndex] = new Vector4(
            transform.position.x,
            transform.position.y,
            transform.position.z,
            radius * strength
        );

        Shader.SetGlobalVectorArray(ID_Interactors, _interactors);
        Shader.SetGlobalInt(ID_InteractorCount, _interactorCount);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}