using UnityEngine;

namespace ProjectBlackout.PlayerTest
{
    /// <summary>
    /// Puts a copy of the magnet gauntlet on the character's LEFT HAND bone so it follows every
    /// body animation (visible when looking down and in the shadow). The first-person screen
    /// glove (BlackoutGloveViewmodel) is separate.
    /// Put this on the Ch11 model (the object with the Animator).
    /// Offsets are applied every frame, so you can tweak them in Play mode, then copy the values.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [DefaultExecutionOrder(10000)] // after other scripts, so the hidden hand stays hidden
    public class BlackoutGloveHandMount : MonoBehaviour
    {
        [Header("Model")]
        [SerializeField] private GameObject gloveModel;
        [Tooltip("Largest size of the glove in metres.")]
        [Min(0.01f)] public float gloveSize = 0.24f;
        [Tooltip("Extra size on top of Glove Size. Can be changed live in Play mode.")]
        [Range(0.5f, 3f)] public float sizeMultiplier = 1.5f;

        [Header("Offset from the hand bone (metres / degrees)")]
        public Vector3 positionOffset = new Vector3(0f, 0.05f, 0f);
        public Vector3 rotationOffset = Vector3.zero;
        [Tooltip("Tick if it looks like the wrong hand.")]
        public bool mirrorModel = false;

        [Header("Glove model pose (inside the mount)")]
        [Tooltip("On = use the position/rotation below for the glove model (lined up along the forearm). Off = auto-centred.")]
        public bool useModelPose = true;
        public Vector3 modelPosition = new Vector3(-0.0128f, -0.0674f, -0.0060f);
        public Vector3 modelRotation = new Vector3(-72.04f, 6.981f, -7.65f);

        [Header("Body")]
        [Tooltip("Hide Ch11's own left hand (palm + fingers) so only the gauntlet is seen.")]
        public bool hideFingers = true;
        [Tooltip("Only show after the gauntlet has been picked up.")]
        public bool onlyWhenUnlocked = true;

        private Animator animator;
        private BlackoutMagnetGlove glove;
        private Transform hand;
        private Transform mount;
        private GameObject instance;
        private Transform[] fingers;
        private float fitScale = 1f;

        private static readonly HumanBodyBones[] FingerBones =
        {
            HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftIndexProximal,
            HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftRingProximal,
            HumanBodyBones.LeftLittleProximal
        };

        private void Start()
        {
            animator = GetComponent<Animator>();
            glove = transform.root.GetComponentInChildren<BlackoutMagnetGlove>(true);

            if (gloveModel == null || animator == null || !animator.isHuman)
            {
                Debug.LogWarning("[GloveHandMount] Needs a glove model and a Humanoid Animator.");
                enabled = false;
                return;
            }

            hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (hand == null)
            {
                Debug.LogWarning("[GloveHandMount] Left hand bone not found.");
                enabled = false;
                return;
            }

            fingers = new Transform[FingerBones.Length];
            for (int i = 0; i < FingerBones.Length; i++)
                fingers[i] = animator.GetBoneTransform(FingerBones[i]);

            // The mount follows the hand bone every frame but is NOT its child,
            // so the hand itself can be shrunk away without shrinking the glove.
            mount = new GameObject("MagnetGlove_HandMount").transform;
            mount.SetParent(transform, false);
            Vector3 ls = transform.lossyScale;
            mount.localScale = new Vector3(1f / Mathf.Max(1e-5f, ls.x), 1f / Mathf.Max(1e-5f, ls.y), 1f / Mathf.Max(1e-5f, ls.z));
            mount.SetPositionAndRotation(hand.position, hand.rotation);

            instance = Instantiate(gloveModel, mount);
            instance.name = "Glove";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            foreach (Collider c in instance.GetComponentsInChildren<Collider>(true))
                Destroy(c);

            // Fit to gloveSize and centre on the mount.
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
                float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
                float scale = size > 1e-5f ? gloveSize / size : 1f;
                Vector3 centreLocal = mount.InverseTransformPoint(b.center);
                fitScale = scale;
                instance.transform.localScale = Vector3.one * scale;
                instance.transform.localPosition = -centreLocal * scale;
            }

            int layer = gameObject.layer;
            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }

        private void LateUpdate()
        {
            if (mount == null) return;

            bool show = !onlyWhenUnlocked || glove == null || glove.HasGlove;
            if (instance.activeSelf != show) instance.SetActive(show);

            // Follow the hand bone (offsets in metres, in the hand's own axes).
            mount.SetPositionAndRotation(
                hand.position + hand.rotation * positionOffset,
                hand.rotation * Quaternion.Euler(rotationOffset));

            instance.transform.localScale = Vector3.one * (fitScale * sizeMultiplier);

            if (useModelPose)
            {
                instance.transform.localPosition = modelPosition;
                instance.transform.localRotation = Quaternion.Euler(modelRotation);
            }

            Vector3 s = mount.localScale;
            s.x = Mathf.Abs(s.x) * (mirrorModel ? -1f : 1f);
            mount.localScale = s;

            // Runs after the Animator: shrink the whole left hand (palm + fingers) away.
            bool hide = hideFingers && show;
            hand.localScale = hide ? Vector3.one * 0.001f : Vector3.one;
            if (fingers != null)
            {
                foreach (Transform f in fingers)
                    if (f != null && f.localScale.x < 0.01f) f.localScale = Vector3.one; // undo the old finger trick
            }
        }
    }
}
