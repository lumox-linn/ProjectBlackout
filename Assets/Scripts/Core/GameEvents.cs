using System;
using UnityEngine;

/// <summary>The states the whole game can be in. Eliza's GameManager (C1) switches between them.</summary>
public enum GameState
{
    Menu,
    Playing,
    Paused,
    Won,
    Lost
}

/// <summary>
/// Shared "notice board" of game-wide events, so scripts can react to each other
/// without holding references. The UI (Eliza) mostly listens; gameplay scripts announce.
///
/// Announce:  GameEvents.RaiseHealthChanged(current, max);
/// Listen:    void OnEnable()  { GameEvents.HealthChanged += UpdateHearts; }
///            void OnDisable() { GameEvents.HealthChanged -= UpdateHearts; }
///
/// Always unsubscribe in OnDisable, or a destroyed object can still get called and throw errors.
/// </summary>
public static class GameEvents
{
    /// <summary>The game state changed (GameManager raises this).</summary>
    public static event Action<GameState> GameStateChanged;

    /// <summary>The player's health changed: (current, max). PlayerHealth raises this.</summary>
    public static event Action<int, int> HealthChanged;

    /// <summary>The current objective text changed. ObjectiveManager raises this.</summary>
    public static event Action<string> ObjectiveChanged;

    /// <summary>
    /// The interaction prompt changed: the text to show, or null to hide it.
    /// PlayerInteractor raises this, and the HUD shows "Press E to ...".
    /// </summary>
    public static event Action<string> InteractPromptChanged;

    /// <summary>The current game state. Anyone can check it, e.g. GameEvents.CurrentState == GameState.Playing</summary>
    public static GameState CurrentState { get; private set; } = GameState.Menu;

    public static void RaiseGameStateChanged(GameState state)
    {
        CurrentState = state;
        GameStateChanged?.Invoke(state);
    }
    public static void RaiseHealthChanged(int current, int max) => HealthChanged?.Invoke(current, max);
    public static void RaiseObjectiveChanged(string text)       => ObjectiveChanged?.Invoke(text);
    public static void RaiseInteractPromptChanged(string text)  => InteractPromptChanged?.Invoke(text);

    // Clears old listeners each time Play is pressed (see NoiseSystem for why).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        GameStateChanged = null;
        HealthChanged = null;
        ObjectiveChanged = null;
        InteractPromptChanged = null;
    }
}
