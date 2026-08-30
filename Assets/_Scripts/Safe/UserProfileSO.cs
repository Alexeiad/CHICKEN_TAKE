using UnityEngine;

[CreateAssetMenu(fileName = "UserProfile", menuName = "Configs/UserProfile")]
public class UserProfileSO : ScriptableObject
{
    public string UserName;
    public int Level;
    public int Coins;
    public int EggsCollected;
    public bool TutorialCompleted;

    // public AchievementData Achievements;
    // public StatisticsData Statistics;
    // public InventoryData Inventory;
}