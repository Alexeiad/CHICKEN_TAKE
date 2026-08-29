using UnityEngine;

[CreateAssetMenu(fileName = "PlayerData", menuName = "Game Data/Player Data")]
public class PlayerDataSO : GameDataSO
{
    protected override string CashKey => $"{name}_Cash";
    protected override string ChickenKey => $"{name}_Chicken";

    public override void Load()
    {
        _cash = PlayerPrefs.GetInt(CashKey, _cash);
        _chicken = PlayerPrefs.GetInt(ChickenKey, _chicken);
        OnDataChanged?.Invoke();
    }

    public override void Save()
    {
        PlayerPrefs.SetInt(CashKey, _cash);
        PlayerPrefs.SetInt(ChickenKey, _chicken);
        PlayerPrefs.Save();
    }
}