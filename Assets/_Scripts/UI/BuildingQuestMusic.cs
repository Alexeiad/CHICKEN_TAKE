using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

// Own only this cutscene's instance. Preserve and resume existing background instances.
public sealed class BuildingQuestMusic
{
    private EventInstance story;
    private readonly List<EventInstance> paused = new List<EventInstance>();

    public void Play(BuildingMenuConfig config)
    {
        Stop();
        if (string.IsNullOrWhiteSpace(config.alienMusicEvent)) return;
        try
        {
            story = RuntimeManager.CreateInstance(config.alienMusicEvent);
            if (story.start() != FMOD.RESULT.OK) { Stop(); return; }
            if (string.IsNullOrWhiteSpace(config.backgroundMusicEvent)) return;
            EventDescription background = RuntimeManager.GetEventDescription(config.backgroundMusicEvent);
            if (background.getInstanceList(out EventInstance[] instances) != FMOD.RESULT.OK) return;
            foreach (EventInstance instance in instances)
            {
                if (instance.getPaused(out bool wasPaused) == FMOD.RESULT.OK && !wasPaused
                    && instance.setPaused(true) == FMOD.RESULT.OK)
                    paused.Add(instance);
            }
        }
        catch (System.Exception exception)
        {
            Stop();
            Debug.LogWarning("Quest music could not be played: " + exception.Message);
        }
    }

    public void Stop()
    {
        if (story.isValid())
        {
            story.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            story.release();
            story = default;
        }
        foreach (EventInstance instance in paused)
            if (instance.isValid()) instance.setPaused(false);
        paused.Clear();
    }
}
