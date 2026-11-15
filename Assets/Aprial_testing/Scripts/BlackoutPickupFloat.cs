using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlackout.PlayerTest
{
    /// <summary>
    /// Makes a pickup visual spin, float up and down and pulse its glow.
    /// Put it on the visual child (not on the object with the collider).
    /// </summary>
    public class BlackoutPickupFloat : MonoBehaviour
    {
        [Header("Motion")]
        [Tooltip("Degrees per second around the up axis.")]
        public float spinSpeed = 45f;
        [Min(0f)] public float bobHeight = 0.08f;
        [Min(0f)] public float bobSpeed = 2f;

        [Header("Glow (materials with 'glow' in their name)")]
        [ColorUsage(false, true)] public Color glowColor = new Color(0.2f, 0.9f, 1f);
        [Min(0f)] public float minGlow = 1f;
        [Min(0f)] public float maxGlow = 4f;
        [Min(0f)] public float pulseSpeed = 3f;

        [Header("Optional light that pulses with the glow")]
        [SerializeField] private Light glowLight;
        [Min(0f)] public float minLightIntensity = 0.6f;
        [Min(0f)] public float maxLightIntensity = 2f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private readonly List<Material> glowMaterials = new List<Material>();
        private Vector3 startLocalPosition;

        private void Awake()
        {
            startLocalPosition = transform.localPosition;

            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material m in r.materials)
                {
                    if (m != null && m.name.ToLowerInvariant().Contains("glow") && m.HasProperty(EmissionColorId))
                    {
                        m.EnableKeyword("_EMISSION");
                        glowMaterials.Add(m);
                    }
                }
            }
        }

        private void Update()
        {
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            transform.localPosition = startLocalPosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);
            Color c = glowColor * Mathf.Lerp(minGlow, maxGlow, pulse);
            for (int i = 0; i < glowMaterials.Count; i++)
                glowMaterials[i].SetColor(EmissionColorId, c);

            if (glowLight != null)
                glowLight.intensity = Mathf.Lerp(minLightIntensity, maxLightIntensity, pulse);
        }

        private void OnDestroy()
        {
            foreach (Material m in glowMaterials)
                if (m != null) Destroy(m);
        }
    }
}
