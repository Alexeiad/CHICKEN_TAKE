using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RaceEventChannel", menuName = "Game/Events/Race Event Channel")]
public class RaceEventChannelSO : ScriptableObject
{
    public event Action OnRaceStarted;
    public event Action<IReadOnlyList<RacerState>> OnStandingsUpdated;
    public event Action<RacerState, IReadOnlyList<RacerState>> OnRaceFinished;

    public IReadOnlyList<RacerState> LastStandings { get; private set; }

    public void RaiseRaceStarted()
    {
        LastStandings = null;
        OnRaceStarted?.Invoke();
    }

    public void RaiseStandingsUpdated(IReadOnlyList<RacerState> standings)
    {
        LastStandings = standings;
        OnStandingsUpdated?.Invoke(standings);
    }

    public void RaiseRaceFinished(RacerState winner, IReadOnlyList<RacerState> finalStandings)
    {
        LastStandings = finalStandings;
        OnRaceFinished?.Invoke(winner, finalStandings);
    }

    private void OnDisable()
    {
        LastStandings = null;
    }
}