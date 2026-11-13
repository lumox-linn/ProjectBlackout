using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// shows the creature's current state name above its head, in a different colour per state
// (always in the Scene view; in the Game view only if showInGame is ticked, e.g. for the presentation demo,
// because in the real game it would show the player where the creature is)
[RequireComponent(typeof(CreatureController))]
public class CreatureStateLabel : MonoBehaviour
{
    // tick to also show the label in the Game view (off for the real game)
    public bool showInGame = false;
    // metres above the creature's middle (1.5 = just above the head)
    public float heightOffset = 1.5f;
    // size of the label text
    public int fontSize = 20;

    // the creature whose state is shown
    private CreatureController creature;
    // the camera the label is drawn for
    private Camera cam;
    // the text styles (created once, not every frame)
    private GUIStyle textStyle;
    private GUIStyle shadowStyle;

    void Awake()
    {
        creature = GetComponent<CreatureController>();
    }

    // draws the label in the Game view
    void OnGUI()
    {
        // label turned off
        if (!showInGame || creature == null)
        {
            return;
        }

        // find the camera tagged "MainCamera"
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null)
            {
                return;
            }
        }

        // turn the position above the head into a screen position
        Vector3 worldPos = transform.position + Vector3.up * heightOffset;
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        // z < 0 means the creature is behind the camera, so do not draw it
        if (screenPos.z < 0.0f)
        {
            return;
        }

        // create the styles the first time
        if (textStyle == null)
        {
            textStyle = new GUIStyle(GUI.skin.label);
            textStyle.fontSize = fontSize;
            textStyle.fontStyle = FontStyle.Bold;
            textStyle.alignment = TextAnchor.MiddleCenter;

            shadowStyle = new GUIStyle(textStyle);
            shadowStyle.normal.textColor = Color.black;
        }

        // colour the text for the current state
        string stateName = creature.CurrentStateName;
        textStyle.normal.textColor = GetColour(stateName);

        // screen y starts at the bottom but GUI y starts at the top, so flip it
        Rect rect = new Rect(screenPos.x - 100.0f, Screen.height - screenPos.y - 15.0f, 200.0f, 30.0f);

        // black shadow first, then the coloured text on top, so it is readable in the dark
        GUI.Label(new Rect(rect.x + 2.0f, rect.y + 2.0f, rect.width, rect.height), stateName, shadowStyle);
        GUI.Label(rect, stateName, textStyle);
    }

    // turns a state name into a colour (same colours as the state diagram)
    private Color GetColour(string stateName)
    {
        switch (stateName)
        {
            case "Patrol":
                return Color.green;
            case "Investigate":
                return Color.yellow;
            case "Chase":
                return new Color(1.0f, 0.5f, 0.0f);
            case "Attack":
                return Color.red;
            case "Stunned":
                return Color.cyan;
            default:
                return Color.white;
        }
    }

#if UNITY_EDITOR
    // draws the label in the Scene view (Editor only, not included in builds)
    void OnDrawGizmos()
    {
        // only while the game is playing
        if (!Application.isPlaying)
        {
            return;
        }

        if (creature == null)
        {
            creature = GetComponent<CreatureController>();
        }

        string stateName = creature.CurrentStateName;
        GUIStyle style = new GUIStyle();
        style.fontSize = 14;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = GetColour(stateName);
        UnityEditor.Handles.Label(transform.position + Vector3.up * heightOffset, stateName, style);
    }
#endif
}
