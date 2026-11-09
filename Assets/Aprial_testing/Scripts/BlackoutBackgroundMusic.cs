
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BlackoutBackgroundMusic : MonoBehaviour
{
    public static BlackoutBackgroundMusic Instance { get; private set; }

    [Header("Home Screen Music")]
    [SerializeField] private AudioClip musicClip;

    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 0.5f;

    private AudioSource musicSource;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        musicSource = GetComponent<AudioSource>();

        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.ignoreListenerPause = true;
        musicSource.volume = musicVolume;

        if (musicClip != null)
        {
            musicSource.clip = musicClip;
        }

        if (musicSource.clip == null)
        {
            Debug.LogWarning("No home screen music assigned!");
            return;
        }

        // Play immediately if the game starts at the menu.
        GameState startingState = GameManager.Instance != null
            ? GameManager.Instance.CurrentState
            : GameEvents.CurrentState;

        UpdateMusic(startingState);
    }

    private void OnEnable()
    {
        GameEvents.GameStateChanged += UpdateMusic;
    }

    private void OnDisable()
    {
        GameEvents.GameStateChanged -= UpdateMusic;
    }

    private void UpdateMusic(GameState state)
    {
        if (musicSource == null || musicSource.clip == null)
            return;

        // Music only plays on the Home Screen.
        if (state == GameState.Menu)
        {
            if (!musicSource.isPlaying)
            {
                musicSource.Play();
            }
        }
        else
        {
            // Stop music during gameplay, pause,
            // death screen and win screen.
            if (musicSource.isPlaying)
            {
                musicSource.Stop();
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
