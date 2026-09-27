using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildingMenuPrefabBuilder
{
    [MenuItem("Tools/Chicken/Building Menus/Create missing prefabs")]
    public static void CreateMissing()
    {
        var config = AssetDatabase.LoadAssetAtPath<BuildingMenuConfig>("Assets/_Data/BuildingMenus/BuildingMenus.asset");
        if (config == null) throw new System.InvalidOperationException("BuildingMenus.asset not found");
        Directory.CreateDirectory("Assets/_Prefabs/BuildingMenus");
        AssetDatabase.Refresh();
        config.barnMenu = Create(config, BuildingKind.Barn);
        config.hangarMenu = Create(config, BuildingKind.Hangar);
        config.shopMenu = Create(config, BuildingKind.Shop);
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
    }

    private static BuildingMenuView Create(BuildingMenuConfig config, BuildingKind kind)
    {
        string path = "Assets/_Prefabs/BuildingMenus/" + kind + "Menu.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing.GetComponent<BuildingMenuView>();
        var view = BuildingMenuView.BuildPrefab(config, kind);
        try { return PrefabUtility.SaveAsPrefabAsset(view.gameObject, path).GetComponent<BuildingMenuView>(); }
        finally { Object.DestroyImmediate(view.gameObject); }
    }
}
