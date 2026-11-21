using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class RaceResultsView : MonoBehaviour
{
    [Header("Ñîáûòèÿ")]
    [SerializeField] private RaceEventChannelSO _raceEvents;

    [Header("UI")]
    [SerializeField] private TMP_Text _standingsText;
    [SerializeField] private TMP_Text _resultsText;

    private readonly StringBuilder _sb = new StringBuilder(256);

    private void OnEnable()
    {
        if (_raceEvents == null) return;

        _raceEvents.OnStandingsUpdated += HandleStandingsUpdated;
        _raceEvents.OnRaceFinished += HandleRaceFinished;

        if (_raceEvents.LastStandings != null)
            HandleStandingsUpdated(_raceEvents.LastStandings);
    }

    private void OnDisable()
    {
        if (_raceEvents == null) return;

        _raceEvents.OnStandingsUpdated -= HandleStandingsUpdated;
        _raceEvents.OnRaceFinished -= HandleRaceFinished;
    }

    private void HandleStandingsUpdated(IReadOnlyList<RacerState> standings)
    {
        string formatted = BuildStandingsString(standings);

        if (_standingsText != null)
            _standingsText.text = formatted;
    }

    private void HandleRaceFinished(RacerState winner, IReadOnlyList<RacerState> finalStandings)
    {
        string formatted = BuildStandingsString(finalStandings);

        if (_standingsText != null)
            _standingsText.text = formatted;

        if (_resultsText != null)
            _resultsText.text = formatted;
    }

    private string BuildStandingsString(IReadOnlyList<RacerState> standings)
    {
        if (standings == null || standings.Count == 0)
            return string.Empty;

        _sb.Clear();
        _sb.Append("<size=70%><color=#AAAAAA>")
           .Append("<pos=0%>#<pos=8%>Èìÿ<pos=62%>êì/÷<pos=82%>êì")
           .Append("</color></size>\n");

        for (int i = 0; i < standings.Count; i++)
        {
            if (i > 0) _sb.Append('\n');

            RacerState racer = standings[i];
            bool isLeader = i == 0;
            bool isPlayer = racer.IsPlayer;

            string line = $"<pos=0%>{racer.Place}<pos=8%>{racer.Name ?? "—"}{(isPlayer ? " (Âû)" : string.Empty)}" +
                          $"<pos=62%>{racer.SpeedKmh:F0}<pos=82%>{racer.DistanceKm:F2}";

            if (isPlayer)
                _sb.Append("<color=#FFD700>").Append(line).Append("</color>");
            else if (isLeader)
                _sb.Append("<color=#00FF00>").Append(line).Append("</color>");
            else
                _sb.Append(line);
        }

        return _sb.ToString();
    }
}