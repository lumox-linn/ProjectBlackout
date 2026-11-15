using UnityEngine;
using ProjectBlackout.PlayerTest;

/// <summary>Completes the "gauntlet" objective when the player picks up Aprial's magnet glove.</summary>
public class GauntletObjective : MonoBehaviour
{
    [SerializeField] private string objectiveId = "gauntlet";
    [SerializeField] private BlackoutMagnetGlove glove;   // drag the player's glove here



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