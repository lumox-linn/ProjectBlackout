using UnityEngine;
using ProjectBlackout.PlayerTest;   // Aprial's namespace (glove)

// Player sounds: footsteps + magnet glove (grab, hold hum, throw, drop).
// Only reads Aprial's scripts, doesn't change them.
public class PlayerAudio : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxSource;   // short sounds: steps, grab, throw, drop
    [SerializeField] private AudioSource humSource;   // looping hum while holding something

    [Header("Footsteps")]
    [SerializeField] private AudioClip[] footsteps;
    [SerializeField] private float stepDistance = 2.0f;        // metres between steps (walk)
    [SerializeField] private float sprintStepDistance = 2.6f;  // metres between steps (sprint)
    [SerializeField] private float sprintSpeed = 5.5f;         // faster than this = sprinting
    [SerializeField] private float walkVolume = 0.6f;
    [SerializeField] private float sprintVolume = 1f;

    [Header("Magnet Glove")]
    [SerializeField] private AudioClip grabClip;
    [SerializeField] private AudioClip throwClip;
    [SerializeField] private AudioClip dropClip;
    [SerializeField] private float humVolume = 0.35f;
    [SerializeField] private float gloveVolume = 0.4f;   // grab / throw / drop loudness

    [Header("Getting Hit")]
    [SerializeField] private AudioClip[] hurtClips;     // e.g. Impact_Flesh_001-003 from the zombie pack
    private int lastHealth = -1;

    private CharacterController controller;
    private BlackoutMagnetGlove glove;
    private float distanceSinceStep;
    private bool wasHolding;
    private bool justThrew;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        glove = GetComponentInChildren<BlackoutMagnetGlove>(true);
    }

    private void OnEnable()
    {
        if (glove != null) glove.Thrown += OnThrown;
        GameEvents.HealthChanged += OnHealthChanged;
    }

    private void OnDisable()
    {
        if (glove != null) glove.Thrown -= OnThrown;
        GameEvents.HealthChanged -= OnHealthChanged;
    }

    private void Update()
    {
        bool playing = GameEvents.CurrentState == GameState.Playing;
        UpdateFootsteps(playing);
        UpdateGlove(playing);
    }

    private void UpdateFootsteps(bool playing)
    {
        if (!playing || controller == null || !controller.isGrounded || footsteps.Length == 0)
            return;

        Vector3 v = controller.velocity;
        v.y = 0f;                                   // ignore falling, only walking speed
        float speed = v.magnitude;

        if (speed < 0.5f)                           // standing still
        {
            distanceSinceStep = stepDistance * 0.5f; // first step comes quickly when you start
            return;
        }

        bool sprinting = speed > sprintSpeed;
        distanceSinceStep += speed * Time.deltaTime;

        if (distanceSinceStep >= (sprinting ? sprintStepDistance : stepDistance))
        {
            distanceSinceStep = 0f;
            sfxSource.pitch = Random.Range(0.9f, 1.1f);   // so steps don't sound identical
            sfxSource.PlayOneShot(footsteps[Random.Range(0, footsteps.Length)],
                                  sprinting ? sprintVolume : walkVolume);
        }
    }

    private void UpdateGlove(bool playing)
    {
        if (glove == null) return;

        bool holding = playing && glove.IsHolding;

        if (holding && !wasHolding) Play(grabClip, gloveVolume);
        if (!holding && wasHolding && !justThrew && playing) Play(dropClip, gloveVolume * 0.7f);
        justThrew = false;
        wasHolding = holding;

        // hum while holding: louder and higher while charging a throw
        if (holding)
        {
            if (!humSource.isPlaying) humSource.Play();
            float charge = glove.IsCharging ? glove.Charge01 : 0f;
            humSource.volume = humVolume * (1f + charge);
            humSource.pitch = 1f + 0.5f * charge;
        }
        else if (humSource.isPlaying)
        {
            humSource.Stop();
        }
    }

    private void OnThrown(float impulse)      // Aprial's glove fires this on a throw
    {
        justThrew = true;
        Play(throwClip, gloveVolume);
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip == null) return;
        sfxSource.pitch = 1f;
        sfxSource.PlayOneShot(clip, volume);
    }

    private void OnHealthChanged(int current, int max)
    {
        if (lastHealth >= 0 && current < lastHealth && hurtClips.Length > 0)
            Play(hurtClips[Random.Range(0, hurtClips.Length)], 1f);   // took damage
        lastHealth = current;
    }
}