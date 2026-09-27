using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class BuildingMenuButton : MonoBehaviour
{
    public enum Command { Back, Close, Tab, Story }
    public BuildingMenuView menu;
    public Command command;
    public int tab;

    private void OnEnable() => GetComponent<Button>().onClick.AddListener(Execute);
    private void OnDisable() => GetComponent<Button>().onClick.RemoveListener(Execute);

    public void Execute()
    {
        if (menu == null) return;
        switch (command)
        {
            case Command.Back: menu.Back(); break;
            case Command.Close: menu.CloseWindow(); break;
            case Command.Tab: menu.SelectTab(tab); break;
            case Command.Story: menu.OpenStory(); break;
        }
    }
}
