using UnityEngine;
using ProjectBlackout.PlayerTest;   // Aprial's namespace, so we can read IsHeld

// Plays a metal clang when a crate hits something hard enough.
// Sits next to Aprial's BlackoutImpactNoise and doesn't change it.
[RequireComponent(typeof(AudioSource))]
public class CrateImpactSound : MonoBehaviour
{
    [SerializeField] private AudioClip[] clips;              // drag 2-3 impact sounds here
    [SerializeField] private float minSpeed = 1.5f;          // softer bumps stay silent
    [SerializeField] private float loudSpeed = 10f;          // full volume at this speed
    [SerializeField] private float cooldown = 0.15f;         // stops machine-gun clangs
    [SerializeField] private Vector2 pitchRange = new Vector2(0.9f, 1.1f);

    private AudioSource source;
    private BlackoutMagneticObject magnetic;
    private float nextTime;
    private float quietUntil;   // ignore hits until the crates have settled

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        magnetic = GetComponent<BlackoutMagneticObject>();
    }
    private void OnEnable()
    {
        quietUntil = Time.time + 1f;   // also covers Restart, which reloads the scene
    }
    private void OnCollisionEnter(Collision collision)
    {
        if (GameEvents.CurrentState != GameState.Playing || Time.time < quietUntil) return;
        if (clips == null || clips.Length == 0) return;
        if (magnetic != null && magnetic.IsHeld) return;     // same rule as Aprial's noise
        if (Time.time < nextTime) return;

        float speed = collision.relativeVelocity.magnitude;
        if (speed < minSpeed) return;

        nextTime = Time.time + cooldown;

        // harder hit = louder, random pitch = less repetitive
        float t = Mathf.InverseLerp(minSpeed, loudSpeed, speed);
        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.PlayOneShot(clips[Random.Range(0, clips.Length)], Mathf.Lerp(0.2f, 1f, t));
    }
}