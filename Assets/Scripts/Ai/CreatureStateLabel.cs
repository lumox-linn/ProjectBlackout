using UnityEngine;

// ---------------------------------------------------------------------------
// CreatureStateLabel  (Task B2, debug tool)
//
// Shows the creature's current state name above its head:
// - in the Game view (OnGUI, so it also works in a build for the demo)
// - in the Scene view (Handles.Label, Editor only)
// Colour changes per state so it's easy to read during the presentation.
//
// Untick "Show In Game" to hide it for the final build.
// ---------------------------------------------------------------------------
[RequireComponent(typeof(CreatureController))]
public class CreatureStateLabel : MonoBehaviour
{
    [SerializeField] private bool showInGame = true;

    [Tooltip("Metres above the creature's pivot (its middle). 1.5 = just above the head.")]
    [SerializeField] private float heightOffset = 1.5f;

    [SerializeField] private int fontSize = 20;

    private CreatureController creature;
    private Camera cam;
    private GUIStyle textStyle;
    private GUIStyle shadowStyle;

    private void Awake()
    {
        creature = GetComponent<CreatureController>();
    }

    private void OnGUI()
    {
        if (!showInGame || creature == null)
        {
            return;
        }

        if (cam == null)
        {
            cam = Camera.main; // the camera tagged "MainCamera"
            if (cam == null)
            {
                return;
            }
        }

        // World position above the head -> screen position.
        Vector3 worldPos = transform.position + Vector3.up * heightOffset;
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        // z < 0 means the creature is behind the camera: don't draw.
        if (screenPos.z < 0f)
        {
            return;
        }

        // Create the styles once (not every frame).
        if (textStyle == null)
        {
            textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            shadowStyle = new GUIStyle(textStyle);
            shadowStyle.normal.textColor = Color.black;
        }

        string stateName = creature.CurrentStateName;
        textStyle.normal.textColor = ColorFor(stateName);

        // Screen y starts at the bottom, GUI y starts at the top, so flip it.
        Rect rect = new Rect(screenPos.x - 100f, Screen.height - screenPos.y - 15f, 200f, 30f);

        // Black shadow first, then the coloured text on top, so it's readable in the dark.
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), stateName, shadowStyle);
        GUI.Label(rect, stateName, textStyle);
    }

    private static Color ColorFor(string stateName)
    {
        switch (stateName)
        {
            case "Patrol":      return Color.green;
            case "Investigate": return Color.yellow;
            case "Chase":       return new Color(1f, 0.5f, 0f); // orange
            case "Attack":      return Color.red;
            case "Stunned":     return Color.cyan;
            default:            return Color.white;
        }
    }

#if UNITY_EDITOR
    // Scene view label (Editor only, not included in builds).
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (creature == null)
        {
            creature = GetComponent<CreatureController>();
        }

        string stateName = creature.CurrentStateName;
        GUIStyle style = new GUIStyle { fontSize = 14, fontStyle = FontStyle.Bold };
        style.normal.textColor = ColorFor(stateName);
        UnityEditor.Handles.Label(transform.position + Vector3.up * heightOffset, stateName, style);
    }
#endif
}
