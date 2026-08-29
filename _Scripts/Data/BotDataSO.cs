using UnityEngine;

[CreateAssetMenu(fileName = "BotData", menuName = "Game Data/Bot Data")]
public class BotDataSO : GameDataSO
{
    protected override string CashKey => $"{name}_Cash";
    protected override string ChickenKey => $"{name}_Chicken";

    public override void Load()
    {
        // Ничего не загружаем, данные живут только в памяти
        OnDataChanged?.Invoke();
    }

    public override void Save()
    {
        // Ничего не сохраняем
    }
}