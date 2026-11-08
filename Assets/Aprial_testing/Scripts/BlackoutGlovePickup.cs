
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectBlackout.PlayerTest
{
    [RequireComponent(typeof(Collider))]
    public class BlackoutGlovePickup : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BlackoutMagnetGlove glove;
        [SerializeField] private Camera playerCamera;

        [Header("Interaction")]
        [Min(0.5f)]
        public float interactRange = 4f;

        private bool canInteract;

        private void Awake()
        {
            if (glove == null)
            {
                glove = FindFirstObjectByType<
                    BlackoutMagnetGlove>();
            }

            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }
        }

        private void Update()
        {
            canInteract = false;

            if (!BlackoutGameMenu.IsGameplayActive)
                return;

            if (glove == null || playerCamera == null)
                return;

            if (glove.HasGlove)
                return;

            Ray ray = playerCamera.ViewportPointToRay(
                new Vector3(0.5f, 0.5f, 0f)
            );

            if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                interactRange,
                ~0,
                QueryTriggerInteraction.Ignore))
                return;

            // Only respond when aiming at this pickup.
            if (hit.collider.gameObject != gameObject &&
                !hit.collider.transform.IsChildOf(transform))
                return;

            canInteract = true;

            if (InteractPressed())
            {
                ClaimGlove();
            }
        }

        private void ClaimGlove()
        {
            glove.UnlockGlove();

            Debug.Log(
                "Player collected the Magnet Glove!"
            );

            gameObject.SetActive(false);
        }

        private void OnGUI()
        {
            if (!canInteract)
                return;

            GUI.Box(
                new Rect(
                    Screen.width / 2f - 150f,
                    Screen.height / 2f + 50f,
                    300f,
                    40f
                ),
                "[E] CLAIM MAGNET GLOVE"
            );
        }

        private static bool InteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null &&
                Keyboard.current.eKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.E);
#else
            return false;
#endif
        }
    }
}
