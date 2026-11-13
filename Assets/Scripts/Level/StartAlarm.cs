using System.Collections;
using UnityEngine;

/// <summary>
/// Plays once when the run starts (in the tutorial room): siren + PA voice announcement +
/// pulsing emergency lights, then fades out. Listens to GameEvents, so it starts when play begins.
/// </summary>
public class StartAlarm : MonoBehaviour
{
    [Header("Siren (2D AudioSource: Loop on, Play On Awake off)")]
    [SerializeField] private AudioSource siren;

    [Header("PA announcement (2D AudioSource: Loop off, Play On Awake off)")]
    [SerializeField] private AudioSource announcement;
    [SerializeField] private float announceDelay = 1f;       // seconds of siren before the voice
    [Range(0f, 1f)]
    [SerializeField] private float sirenWhileTalking = 0.3f;  // siren volume under the voice

    [Header("Ending")]
    [SerializeField] private float sirenAfterVoice = 2f;     // full siren after the voice ends
    [SerializeField] private float fadeOut = 2f;

    [Header("Emergency lights to pulse (optional)")]
    [SerializeField] private Transform lightsParent;
    [SerializeField] private float pulseSpeed = 4f;

    private bool played;
    private Light[] lights;
    private float[] baseIntensity;

    private void Awake()
    {
        if (lightsParent != null)
        {
            lights = lightsParent.GetComponentsInChildren<Light>();
            baseIntensity = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) baseIntensity[i] = lights[i].intensity;
        }
    }

    private void OnEnable() { GameEvents.GameStateChanged += OnStateChanged; }
    private void OnDisable() { GameEvents.GameStateChanged -= OnStateChanged; }

    private void OnStateChanged(GameState state)
    {
        if (state == GameState.Playing && !played)
        {
            played = true;
            StartCoroutine(RunAlarm());
        }
    }

    private IEnumerator RunAlarm()
    {
        float voiceLength = (announcement != null && announcement.clip != null) ? announcement.clip.length : 0f;
        float voiceStart = announceDelay;
        float voiceEnd = voiceStart + voiceLength;
        float fadeStart = voiceEnd + sirenAfterVoice;
        float total = fadeStart + fadeOut;

        if (siren != null) { siren.volume = 1f; siren.Play(); }
        bool voiceStarted = false;
        float t = 0f;

        while (t < total)
        {
            // Pause menu: freeze everything while time is stopped.
            if (Time.timeScale == 0f)
            {
                if (siren != null && siren.isPlaying) siren.Pause();
                if (announcement != null && announcement.isPlaying) announcement.Pause();
                yield return null;
                continue;
            }
            if (siren != null) siren.UnPause();
            if (announcement != null && voiceStarted) announcement.UnPause();

            t += Time.deltaTime;

            // Start the voice after the delay.
            if (!voiceStarted && t >= voiceStart && announcement != null)
            {
                announcement.Play();
                voiceStarted = true;
            }

            // Siren quieter while the voice talks, then fades out at the end.
            bool talking = t >= voiceStart && t < voiceEnd;
            float fade = t < fadeStart ? 1f : 1f - (t - fadeStart) / fadeOut;
            if (siren != null) siren.volume = (talking ? sirenWhileTalking : 1f) * fade;

            // Lights pulse with the alarm, then settle back.
            float pulse = Mathf.Abs(Mathf.Sin(t * pulseSpeed));
            if (lights != null)
                for (int i = 0; i < lights.Length; i++)
                    lights[i].intensity = baseIntensity[i] * Mathf.Lerp(1f, 0.3f + 1.2f * pulse, fade);

            yield return null;
        }

        if (siren != null) siren.Stop();
        if (lights != null)
            for (int i = 0; i < lights.Length; i++) lights[i].intensity = baseIntensity[i];
    }
}