using TMPro;
using UnityEngine;

// Serialized bindings survive prefab saves; geometry and styling stay editable in Prefab Mode.
public sealed class BuildingMenuBinding : MonoBehaviour
{
    public enum Value
    {
        Translation, TractorName, TractorDescription, Story,
        Collected, Sold, Carried, Earned, Cash, Matches, Wins,
        CollectionProgress, MatchProgress, WinProgress
    }
    public Value value;
    public BuildingMenuConfig.LocalizedCopy translation = new BuildingMenuConfig.LocalizedCopy();
    public TMP_Text label;
    public int goal = 100;
    public RectTransform progressFill;

    public void Apply(BuildingMenuConfig config, MenuLanguage language)
    {
        if (label == null) return;
        var data = config.playerData;
        string text;
        switch (value)
        {
            case Value.Translation: text = translation.Get(language); break;
            case Value.TractorName: text = config.tractorName.Get(language); break;
            case Value.TractorDescription: text = config.tractorDescription.Get(language); break;
            case Value.Story: text = config.alienStory.Get(language); break;
            default:
                int number = Number(data);
                if (value >= Value.CollectionProgress)
                {
                    float progress = goal > 0 ? Mathf.Clamp01((float)number / goal) : 0;
                    if (progressFill != null) progressFill.anchorMax = new Vector2(progress, 1);
                    text = data == null ? "—" : number >= goal
                        ? language == MenuLanguage.English ? "Done" : language == MenuLanguage.German ? "Erledigt" : "Готово"
                        : $"{number:N0} / {goal:N0}";
                }
                else text = data == null || ((value == Value.Matches || value == Value.Wins) && !data.HasMatchHistory)
                    ? "—" : number.ToString("N0");
                break;
        }
        label.text = text;
        if (value == Value.Story)
        {
            label.ForceMeshUpdate(true);
            float viewportHeight = ((RectTransform)label.transform.parent).rect.height;
            label.rectTransform.sizeDelta = new Vector2(label.rectTransform.sizeDelta.x,
                Mathf.Max(viewportHeight, label.preferredHeight + 15));
        }
    }

    private int Number(PlayerDataSO data)
    {
        if (data == null) return 0;
        switch (value)
        {
            case Value.Collected: case Value.CollectionProgress: return data.TotalCollected;
            case Value.Sold: return data.TotalSold;
            case Value.Carried: return data.Chicken;
            case Value.Earned: return data.TotalEarned;
            case Value.Cash: return data.Cash;
            case Value.Matches: case Value.MatchProgress: return data.MatchesPlayed;
            case Value.Wins: case Value.WinProgress: return data.MatchesWon;
            default: return 0;
        }
    }
}
