using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlackout.PlayerTest
{
    /// <summary>
    /// First-person magnet gauntlet, animated in code (the model has no bones).
    /// Put this on the viewmodel root that is a child of the player camera and is the
    /// BlackoutMagnetGlove "Glove Visual" (Tools > Project Blackout > Install Glove Viewmodel does this).
    ///
    /// Idle:  gentle walk bob and look sway.
    /// Aim:   while a crate is held the hand raises toward the centre, trembles and glows brighter.
    /// Charge: hand pulls back and glows more while left-click is held.
    /// Throw: quick forward punch + recoil.
    /// </summary>
    public class BlackoutGloveViewmodel : MonoBehaviour
    {
        [Header("References (auto-found if empty)")]
        [SerializeField] private BlackoutMagnetGlove glove;
        [SerializeField] private BlackoutFPSInput movement;

        [Header("Side")]
        [Tooltip("Show the glove on the LEFT side of the screen (mirrors the positions below).")]
        public bool leftSide = true;
        [Tooltip("Tick if the model looks like the wrong hand (e.g. a right hand on the left side).")]
        public bool mirrorModel = false;

        [Header("Poses (local to the camera, written for the RIGHT side)")]
        public Vector3 idlePosition = new Vector3(0.26f, -0.27f, 0.5f);
        public Vector3 aimPosition = new Vector3(0.14f, -0.19f, 0.58f);
        [Tooltip("Extra rotation in the aim pose (degrees).")]
        public Vector3 aimRotation = new Vector3(-8f, -10f, 0f);
        [Min(0.1f)] public float poseSpeed = 10f;
        [Tooltip("Move the glove to the Aim Position/Rotation while holding a crate. Off = it stays in place (still glows/trembles).")]
        public bool raiseWhenHolding = false;

        [Header("Looking Down")]
        [Tooltip("When looking down more than this (degrees), the screen glove slides away so only the glove on the body's hand is seen.")]
        [Range(0f, 90f)] public float hideLookDownAngle = 35f;
        [Tooltip("How far it slides down (metres).")]
        [Min(0f)] public float hideDrop = 0.45f;

        [Header("Walk Bob / Look Sway")]
        [Min(0f)] public float bobAmount = 0.012f;
        [Min(0f)] public float bobFrequency = 10f;
        [Min(0f)] public float swayAmount = 0.0025f;
        [Min(0f)] public float maxSway = 0.04f;

        [Header("Holding / Charging")]
        [Min(0f)] public float holdTremble = 0.003f;
        [Min(0f)] public float chargePullBack = 0.07f;

        [Header("Throw")]
        [Min(0f)] public float throwPunch = 0.16f;
        [Min(0f)] public float throwRecoilAngle = 14f;
        [Min(0.1f)] public float throwRecovery = 8f;

        [Header("Glow (materials with 'glow' in their name)")]
        [ColorUsage(false, true)] public Color glowColor = new Color(0.2f, 0.9f, 1f);
        [Min(0f)] public float idleGlow = 0.6f;
        [Min(0f)] public float aimGlow = 3f;
        [Min(0f)] public float chargeGlow = 7f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private readonly List<Material> glowMaterials = new List<Material>();
        private CharacterController controller;
        private Quaternion baseRotation;
        private Quaternion lastCameraRotation;
        private Vector3 sway;
        private float aimBlend;
        private float bobPhase;
        private float kick;
        private float glow;
        private float hideBlend;

        private void Awake()
        {
            if (glove == null) glove = GetComponentInParent<BlackoutMagnetGlove>();
            if (glove == null) glove = transform.root.GetComponentInChildren<BlackoutMagnetGlove>(true);
            if (movement == null) movement = GetComponentInParent<BlackoutFPSInput>();
            if (movement != null) controller = movement.GetComponent<CharacterController>();

            baseRotation = transform.localRotation;

            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material m in r.materials) // instanced copies, so shared assets are untouched
                {
                    if (m != null && m.name.ToLowerInvariant().Contains("glow") && m.HasProperty(EmissionColorId))
                    {
                        m.EnableKeyword("_EMISSION");
                        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                        glowMaterials.Add(m);
                    }
                }
            }
        }

        private void OnEnable()
        {
            if (glove != null) glove.Thrown += OnThrown;
            if (transform.parent != null) lastCameraRotation = transform.parent.rotation;
            aimBlend = 0f;
            kick = 0f;
        }

        private void OnDisable()
        {
            if (glove != null) glove.Thrown -= OnThrown;
        }

        private void OnThrown(float charge)
        {
            kick = Mathf.Lerp(0.6f, 1f, charge);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return; // paused

            bool holding = glove != null && glove.IsHolding;
            float charge = glove != null && glove.IsCharging ? glove.Charge01 : 0f;

            // Blend between idle and aim pose.
            aimBlend = Mathf.Lerp(aimBlend, holding ? 1f : 0f, 1f - Mathf.Exp(-poseSpeed * dt));
            float a = Mathf.SmoothStep(0f, 1f, aimBlend);
            float poseBlend = raiseWhenHolding ? a : 0f;
            Vector3 pos = Vector3.Lerp(idlePosition, aimPosition, poseBlend);
            Vector3 euler = Vector3.Lerp(Vector3.zero, aimRotation, poseBlend);

            // Walk bob, scaled by how fast we move.
            float moveFactor = 0f;
            if (controller != null && movement != null)
            {
                Vector3 v = controller.velocity; v.y = 0f;
                moveFactor = Mathf.Clamp01(v.magnitude / Mathf.Max(0.1f, movement.sprintSpeed));
            }
            bobPhase += dt * bobFrequency * Mathf.Lerp(0.6f, 1.4f, moveFactor) * (moveFactor > 0.05f ? 1f : 0f);
            float bobScale = bobAmount * moveFactor * (1f - 0.6f * a); // steadier while aiming
            pos += new Vector3(Mathf.Cos(bobPhase * 0.5f) * bobScale, -Mathf.Abs(Mathf.Sin(bobPhase)) * bobScale, 0f);

            // Sway: the hand lags slightly behind camera turns.
            if (transform.parent != null)
            {
                Quaternion camRot = transform.parent.rotation;
                Vector3 d = (Quaternion.Inverse(lastCameraRotation) * camRot).eulerAngles;
                lastCameraRotation = camRot;
                d.x = Mathf.DeltaAngle(0f, d.x);
                d.y = Mathf.DeltaAngle(0f, d.y);
                Vector3 target = new Vector3(-d.y, d.x, 0f) * swayAmount;
                target = Vector3.ClampMagnitude(target, maxSway);
                sway = Vector3.Lerp(sway, target, 1f - Mathf.Exp(-12f * dt));
                pos += sway;
            }

            // Magnet strain while holding, stronger while charging.
            if (holding)
            {
                float t = Time.time * 25f;
                float amp = holdTremble * (1f + 2f * charge);
                pos += new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f) * (2f * amp);
            }
            pos.z -= chargePullBack * charge;
            euler.x += 6f * charge;

            // Throw punch + recoil, then recover.
            kick = Mathf.MoveTowards(kick, 0f, throwRecovery * dt * Mathf.Max(0.2f, kick));
            float k = kick * kick;
            pos.z += throwPunch * k;
            euler.x -= throwRecoilAngle * k;

            // Slide away when looking down (the body's hand glove is visible then).
            if (transform.parent != null)
            {
                float lookDown = -Mathf.Asin(Mathf.Clamp(transform.parent.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
                float hideTarget = Mathf.InverseLerp(hideLookDownAngle - 10f, hideLookDownAngle + 5f, lookDown);
                hideBlend = Mathf.Lerp(hideBlend, hideTarget, 1f - Mathf.Exp(-12f * dt));
                pos.y -= hideDrop * Mathf.SmoothStep(0f, 1f, hideBlend);
            }

            if (leftSide)
            {
                pos.x = -pos.x;
                euler.y = -euler.y;
                euler.z = -euler.z;
            }

            transform.localPosition = pos;
            transform.localRotation = baseRotation * Quaternion.Euler(euler);

            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (mirrorModel ? -1f : 1f);
            transform.localScale = scale;

            // Glow.
            float targetGlow = Mathf.Lerp(idleGlow, aimGlow, a) + charge * (chargeGlow - aimGlow) + kick * chargeGlow;
            if (holding) targetGlow *= 1f + 0.15f * Mathf.Sin(Time.time * 12f);
            glow = Mathf.Lerp(glow, targetGlow, 1f - Mathf.Exp(-10f * dt));
            Color c = glowColor * glow;
            for (int i = 0; i < glowMaterials.Count; i++)
                glowMaterials[i].SetColor(EmissionColorId, c);
        }

        private void OnDestroy()
        {
            foreach (Material m in glowMaterials)
                if (m != null) Destroy(m);
        }
    }
}
