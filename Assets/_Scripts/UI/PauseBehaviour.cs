using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PauseBehaviour : MonoBehaviour
{
    public GameObject pauseObject;

    public bool IsPaused { get; private set; }

    readonly List<MonoBehaviour> disabled = new List<MonoBehaviour>();

    void Start()
    {
        if (pauseObject != null) pauseObject.SetActive(false);
    }

    void Update()
    {
        if (EscPressed())
        {
            if (IsPaused) Resume();
            else Pause();
        }
    }

    bool EscPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    public void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;

        if (pauseObject != null) pauseObject.SetActive(true);

        Camera cam = Camera.main;
        if (cam != null)
        {
            foreach (var script in cam.GetComponents<MonoBehaviour>())
            {
                if (script != this && script.enabled)
                {
                    script.enabled = false;
                    disabled.Add(script);
                }
            }
        }
    }

    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;

        if (pauseObject != null) pauseObject.SetActive(false);

        foreach (var script in disabled)
            if (script != null) script.enabled = true;
        disabled.Clear();
    }
}