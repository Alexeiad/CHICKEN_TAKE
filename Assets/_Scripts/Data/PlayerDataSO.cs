using UnityEngine;

[CreateAssetMenu(fileName = "PlayerData", menuName = "Game Data/Player Data")]
public class PlayerDataSO : GameDataSO
{
    public int TotalCollected { get; private set; }
    public int TotalSold { get; private set; }
    public int TotalEarned { get; private set; }
    public int MatchesPlayed { get; private set; }
    public int MatchesWon { get; private set; }
    public bool HasMatchHistory { get; private set; }
    protected override string CashKey => $"{name}_Cash";
    protected override string ChickenKey => $"{name}_Chicken";

    public override void Load()
    {
        _cash = PlayerPrefs.GetInt(CashKey, _cash);
        _chicken = PlayerPrefs.GetInt(ChickenKey, _chicken);
        TotalCollected = PlayerPrefs.GetInt(name + "_TotalCollected", 0);
        TotalSold = PlayerPrefs.GetInt(name + "_TotalSold", 0);
        TotalEarned = PlayerPrefs.GetInt(name + "_TotalEarned", 0);
        MatchesPlayed = PlayerPrefs.GetInt(name + "_MatchesPlayed", 0);
        MatchesWon = PlayerPrefs.GetInt(name + "_MatchesWon", 0);
        HasMatchHistory = PlayerPrefs.GetInt(name + "_HasMatchHistory", 0) != 0;
        OnDataChanged?.Invoke();
    }

    public override void Save()
    {
        PlayerPrefs.SetInt(CashKey, _cash);
        PlayerPrefs.SetInt(ChickenKey, _chicken);
        PlayerPrefs.SetInt(name + "_TotalCollected", TotalCollected);
        PlayerPrefs.SetInt(name + "_TotalSold", TotalSold);
        PlayerPrefs.SetInt(name + "_TotalEarned", TotalEarned);
        PlayerPrefs.SetInt(name + "_MatchesPlayed", MatchesPlayed);
        PlayerPrefs.SetInt(name + "_MatchesWon", MatchesWon);
        PlayerPrefs.SetInt(name + "_HasMatchHistory", HasMatchHistory ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void RecordCollection(int carried)
    {
        TotalCollected++;
        _chicken = Mathf.Max(0, carried);
        Save();
        OnDataChanged?.Invoke();
    }

    public void RecordSale(int coins, int chickens)
    {
        if (coins < 0 || chickens <= 0) return;
        _cash += coins;
        _chicken = 0;
        TotalSold += chickens;
        TotalEarned += coins;
        Save();
        OnDataChanged?.Invoke();
    }

    // Call exactly once from the match-results controller when it is implemented.
    public void RecordMatch(bool won)
    {
        HasMatchHistory = true;
        MatchesPlayed++;
        if (won) MatchesWon++;
        Save();
        OnDataChanged?.Invoke();
    }
}
