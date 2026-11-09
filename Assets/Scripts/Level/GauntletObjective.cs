using UnityEngine;
using ProjectBlackout.PlayerTest;

/// <summary>Completes the "gauntlet" objective when the player picks up Aprial's magnet glove.</summary>
public class GauntletObjective : MonoBehaviour
{
    [SerializeField] private string objectiveId = "gauntlet";
    private BlackoutMagnetGlove glove;

    private void Start() { glove = FindFirstObjectByType<BlackoutMagnetGlove>(); }

    private void Update()
    {
        if (glove != null && glove.HasGlove)
        {
            if (ObjectiveManager.Instance != null)
                ObjectiveManager.Instance.Complete(objectiveId);
            enabled = false;   // job done, stop checking
        }
    }
}