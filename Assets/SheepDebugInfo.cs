using UnityEngine;

public class SheepDebugGizmo : MonoBehaviour
{
    private SheepAgent _sheep;

    [Header("Display")]
    public float labelHeight = 2.5f;
    public float sphereRadius = 0.2f;

    private static readonly Color ColorGrazing = new Color(0.2f, 0.9f, 0.2f);
    private static readonly Color ColorIdleWandering = new Color(0.1f, 0.6f, 1.0f);
    private static readonly Color ColorPastureGrazing = new Color(0.0f, 0.4f, 0.0f);
    private static readonly Color ColorFlocking = new Color(1.0f, 0.8f, 0.0f);
    private static readonly Color ColorFleeing = new Color(1.0f, 0.1f, 0.1f);
    private static readonly Color ColorEating = new Color(1.0f, 0.5f, 0.0f);

    void OnEnable()
    {
        _sheep = GetComponent<SheepAgent>();
    }

    void OnDrawGizmosSelected()
    {
        if (_sheep == null) return;

        // Leer estados via reflection para no romper encapsulación
        bool isIdleWandering = GetPrivateBool("_isIdleWandering");
        bool isPastureGrazing = GetPrivateBool("_isPastureGrazing");
        bool isEating = GetPrivateBool("_isEating");
        float pastureTimer = GetPrivateFloat("_pastureGrazeTimer");
        float pastureDuration = GetPrivateFloat("_pastureGrazeDuration");
        float eatTimer = GetPrivateFloat("_eatTimer");
        float eatDuration = GetPrivateFloat("_eatDuration");
        float stillTimer = GetPrivateFloat("_stillTimer");

        // ?? Color de la esfera según estado ??????????????????????
        Color stateColor;
        string stateLabel;

        if (_sheep.CurrentState == SheepAgent.SheepState.Fleeing)
        {
            stateColor = ColorFleeing;
            stateLabel = "FLEEING";
        }
        else if (_sheep.CurrentState == SheepAgent.SheepState.Flocking)
        {
            stateColor = ColorFlocking;
            stateLabel = "FLOCKING";
        }
        else if (isEating)
        {
            stateColor = ColorEating;
            stateLabel = $"EATING {eatTimer:F1}/{eatDuration:F1}s";
        }
        else if (isPastureGrazing)
        {
            stateColor = ColorPastureGrazing;
            stateLabel = $"PASTURE {pastureTimer:F1}/{pastureDuration:F1}s";
        }
        else if (isIdleWandering)
        {
            stateColor = ColorIdleWandering;
            stateLabel = "IDLE WANDER";
        }
        else
        {
            stateColor = ColorGrazing;
            stateLabel = $"GRAZING (still {stillTimer:F1}s)";
        }

        // ?? Esfera de estado ??????????????????????????????????????
        Gizmos.color = stateColor;
        Gizmos.DrawSphere(transform.position + Vector3.up * labelHeight, sphereRadius);

        // ?? Línea vertical desde la oveja hasta la esfera ?????????
        Gizmos.color = new Color(stateColor.r, stateColor.g, stateColor.b, 0.4f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * labelHeight);

#if UNITY_EDITOR
        // ?? Label de texto ????????????????????????????????????????
        GUIStyle style = new GUIStyle();
        style.normal.textColor = stateColor;
        style.fontSize = 11;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;

        string fullLabel = $"{stateLabel}\n" +
                           $"Arousal: {_sheep.Arousal:F2}\n" +
                           $"Speed: {_sheep.AgentVelocity.magnitude:F1}";

        UnityEditor.Handles.Label(
            transform.position + Vector3.up * (labelHeight + sphereRadius + 0.15f),
            fullLabel,
            style
        );
#endif
    }

    // ?? Helpers de reflection ?????????????????????????????????????
    bool GetPrivateBool(string fieldName)
    {
        var field = typeof(SheepAgent).GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field != null && (bool)field.GetValue(_sheep);
    }

    float GetPrivateFloat(string fieldName)
    {
        var field = typeof(SheepAgent).GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field != null ? (float)field.GetValue(_sheep) : 0f;
    }
}