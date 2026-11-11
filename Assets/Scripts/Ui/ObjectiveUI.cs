using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class ObjectiveUI : MonoBehaviour
{
    private TMP_Text objectiveText;

    private void Awake()
    {
        objectiveText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        GameEvents.ObjectiveChanged += UpdateObjective;
        RefreshObjective();
    }

    private void Start()
    {
        RefreshObjective();
    }

    private void OnDisable()
    {
        GameEvents.ObjectiveChanged -= UpdateObjective;
    }

    private void RefreshObjective()
    {
        if (ObjectiveManager.Instance != null)
            UpdateObjective(ObjectiveManager.Instance.CurrentText);
    }

    private void UpdateObjective(string text)
    {
        objectiveText.text = text ?? "";
    }
}