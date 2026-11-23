#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Редакторские утилиты для TesterSettingsPanel. ПОЛОЖИ ЭТОТ ФАЙЛ В ПАПКУ Editor.
///  - Tools/Tester Panel/Create Prefab — создаёт префаб Canvas с окном
///  - Tools/Tester Panel/Paste Clipboard Into Selected — вставляет JSON из буфера в выделенные SO
///  - то же самое через шестерёнку (⋮) в инспекторе GameSettingsSO / VehiclesSettings
/// </summary>
public static class TesterSettingsEditorTools
{
    private const string PrefabFolder = "Assets/Prefabs";
    private const string PrefabPath = PrefabFolder + "/TesterSettingsPanel.prefab";

    // ------------------------------------------------------------------
    [MenuItem("Tools/Tester Panel/Create Prefab")]
    private static void CreatePrefab()
    {
        var go = new GameObject("TesterSettingsPanel");
        var panel = go.AddComponent<TesterSettingsPanel>(); // RequireComponent добавит Canvas, CanvasScaler, GraphicRaycaster

        var so = new SerializedObject(panel);
        AssignFirst<GameSettingsSO>(so, "gameSettings");
        AssignFirst<VehiclesSettings>(so, "vehicleSettings");
        so.ApplyModifiedPropertiesWithoutUndo();

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (!Directory.Exists(PrefabFolder)) Directory.CreateDirectory(PrefabFolder);
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);

        AssetDatabase.Refresh();
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log("[TesterSettingsPanel] Префаб создан: " + PrefabPath +
                  ". Закинь его на сцену (достаточно одного на всю игру).");
    }

    private static void AssignFirst<T>(SerializedObject so, string propName) where T : Object
    {
        var prop = so.FindProperty(propName);
        if (prop == null) return;
        var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
        if (guids.Length == 0) return;
        prop.objectReferenceValue = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    // ------------------------------------------------------------------
    [MenuItem("Tools/Tester Panel/Paste Clipboard Into Selected")]
    private static void PasteIntoSelected()
    {
        int ok = 0;
        foreach (var obj in Selection.objects)
            if (obj is GameSettingsSO || obj is VehiclesSettings)
                if (PasteInto(obj)) ok++;

        if (ok == 0)
            EditorUtility.DisplayDialog("Tester Panel",
                "Выдели в Project ассет GameSettingsSO и/или VehiclesSettings и скопируй JSON из окна тестера.", "OK");
    }

    [MenuItem("CONTEXT/GameSettingsSO/Paste Tester JSON")]
    private static void PasteContextGame(MenuCommand cmd) { PasteInto(cmd.context); }

    [MenuItem("CONTEXT/VehiclesSettings/Paste Tester JSON")]
    private static void PasteContextVehicles(MenuCommand cmd) { PasteInto(cmd.context); }

    private static bool PasteInto(Object target)
    {
        var text = GUIUtility.systemCopyBuffer;
        if (!TryGetSection(text, target.GetType().Name, out var json))
        {
            EditorUtility.DisplayDialog("Tester Panel",
                "В буфере нет JSON для " + target.GetType().Name + ".\nСкопируй значения кнопкой «Копировать всё» в окне тестера.", "OK");
            return false;
        }

        Undo.RecordObject(target, "Paste Tester JSON");
        JsonUtility.FromJsonOverwrite(json, target);
        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssets();
        Debug.Log("[TesterSettingsPanel] Значения вставлены в " + target.name, target);
        return true;
    }

    private static bool TryGetSection(string text, string typeName, out string json)
    {
        json = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var m = Regex.Match(
            text,
            @"^###\s*" + Regex.Escape(typeName) + @"\s*\r?\n(?<j>.*?)(?=^###\s|\z)",
            RegexOptions.Singleline | RegexOptions.Multiline);

        if (m.Success)
        {
            json = m.Groups["j"].Value.Trim();
            return json.StartsWith("{");
        }

        // в буфере голый JSON без заголовков — берём как есть
        var trimmed = text.Trim();
        if (!trimmed.Contains("###") && trimmed.StartsWith("{"))
        {
            json = trimmed;
            return true;
        }
        return false;
    }
}
#endif