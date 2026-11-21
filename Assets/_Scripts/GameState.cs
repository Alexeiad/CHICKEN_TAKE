using System;
using UnityEngine;

public static class GameState
{
    public static bool IsPaused { get; private set; }
    public static event Action<bool> PauseChanged;

    public static void Pause() => SetPaused(true);
    public static void Resume() => SetPaused(false);
    public static void TogglePause() => SetPaused(!IsPaused);

    static void SetPaused(bool value)
    {
        if (IsPaused == value) return;
        IsPaused = value;
        Time.timeScale = value ? 0f : 1f;
        PauseChanged?.Invoke(value);
    }
}