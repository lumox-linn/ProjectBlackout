using UnityEngine;

namespace ProjectBlackout.PlayerTest
{
    /// <summary>
    /// The magnet glove pickup. Implements IInteractable, so Linn's PlayerInteractor
    /// shows the "[E] Claim magnet glove" prompt in Eliza's HUD and calls Interact() on E.
    /// (No own E-key check here, otherwise E would fire twice.)
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BlackoutGlovePickup : MonoBehaviour, IInteractable
    {
        [Header("References")]
        [Tooltip("Drag the player's BlackoutMagnetGlove here.")]
        [SerializeField] private BlackoutMagnetGlove glove;

        // IInteractable
        public string Prompt => "Claim magnet glove";
        public bool CanInteract => glove != null && !glove.HasGlove;

        private void Awake()
        {
            if (glove == null)
            {
                Debug.LogWarning(
                    "BlackoutGlovePickup: Glove is not assigned in the Inspector.",
                    this
                );
            }
        }

        public void Interact()
        {
            if (!CanInteract)
                return;

            glove.UnlockGlove();
            Debug.Log("Player collected the Magnet Glove!");
            gameObject.SetActive(false);
        }
    }
}
