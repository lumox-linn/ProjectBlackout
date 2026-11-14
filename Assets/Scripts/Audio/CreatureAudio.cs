using UnityEngine;

// Creature sounds: breathing loop, idle growls, a growl when it spots you,
// a roar on each attack swing, a shriek when stunned.
// Only reads Sylvia's CreatureController (state name + AttackSwing event).
public class CreatureAudio : MonoBehaviour
{
    [Header("Audio Sources (both 3D)")]
    [SerializeField] private AudioSource voiceSource;    // growls, roars, shrieks
    [SerializeField] private AudioSource breathSource;   // looping breathing

    [Header("Clips")]
    [SerializeField] private AudioClip[] idleGrowls;     // now and then while patrolling
    [SerializeField] private AudioClip spottedClip;      // when it starts chasing you
    [SerializeField] private AudioClip[] attackClips;    // each attack swing
    [SerializeField] private AudioClip stunnedClip;      // hit by a thrown crate

    [Header("Tuning")]
    [SerializeField] private Vector2 idleGrowlGap = new Vector2(6f, 12f); // seconds between idle growls
    [SerializeField] private float calmBreathVolume = 0.5f;
    [SerializeField] private float chaseBreathVolume = 1f;
    [SerializeField] private float chaseBreathPitch = 1.25f;              // faster breathing when chasing

    private CreatureController creature;
    private string lastState = "";
    private float nextIdleGrowl;
    private bool breathStarted;

    private void Awake()
    {
        creature = GetComponent<CreatureController>();
    }

    private void OnEnable()
    {
        if (creature != null) creature.AttackSwing += OnAttackSwing;
        ScheduleIdleGrowl();
    }

    private void OnDisable()
    {
        if (creature != null) creature.AttackSwing -= OnAttackSwing;
    }

    private void Update()
    {
        if (creature == null) return;

        // silent on the menu, while paused and on win/lose
        if (GameEvents.CurrentState != GameState.Playing)
        {
            if (breathSource.isPlaying) breathSource.Pause();
            return;
        }

        if (!breathSource.isPlaying)
        {
            if (breathStarted) breathSource.UnPause();
            else { breathSource.Play(); breathStarted = true; }
        }

        // react when the state changes
        string state = creature.CurrentStateName;
        if (state != lastState)
        {
            if (state == "Chase" && lastState != "Attack") Play(spottedClip); // spotted you (not between attacks)
            if (state == "Stunned") { voiceSource.Stop(); Play(stunnedClip); }
            lastState = state;
        }

        // breathing: calm while patrolling, heavy while chasing, quiet while stunned
        bool angry = state == "Chase" || state == "Attack";
        float targetVolume = state == "Stunned" ? 0.1f : (angry ? chaseBreathVolume : calmBreathVolume);
        breathSource.volume = Mathf.MoveTowards(breathSource.volume, targetVolume, Time.deltaTime);
        breathSource.pitch = Mathf.MoveTowards(breathSource.pitch, angry ? chaseBreathPitch : 1f, Time.deltaTime * 0.5f);

        // idle growls while it's wandering around
        if ((state == "Patrol" || state == "Investigate") && Time.time >= nextIdleGrowl)
        {
            PlayRandom(idleGrowls);
            ScheduleIdleGrowl();
        }
    }

    private void OnAttackSwing()      // Sylvia's controller fires this at the hit moment
    {
        PlayRandom(attackClips);
    }

    private void ScheduleIdleGrowl()
    {
        nextIdleGrowl = Time.time + Random.Range(idleGrowlGap.x, idleGrowlGap.y);
    }

    private void PlayRandom(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;
        Play(clips[Random.Range(0, clips.Length)]);
    }

    private void Play(AudioClip clip)
    {
        if (clip == null) return;
        voiceSource.pitch = Random.Range(0.92f, 1.08f);
        voiceSource.PlayOneShot(clip);
    }
}