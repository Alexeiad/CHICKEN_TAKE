#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Положи в папку Assets/Editor. Меню: Tools > Race > Create Speedometer Prefab.
public static class SpeedometerPrefabBuilder
{
    private const string Folder = "Assets/Prefabs/UI";
    private const string PrefabPath = Folder + "/Speedometer.prefab";

    private const float Size = 300f;
    private const float Sweep = 0.75f;
    private const float StartAngle = -45f; // дуга начинается снизу слева, зазор внизу

    private static readonly Color PlateColor = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color TrackColor = new Color(1f, 1f, 1f, 0.15f);
    private static readonly Color InnerColor = new Color(0.08f, 0.08f, 0.10f, 1f);
    private static readonly Color MutedText = new Color(1f, 1f, 1f, 0.55f);

    [MenuItem("Tools/Race/Create Speedometer Prefab")]
    public static void Create()
    {
        Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        GameObject root = new GameObject("Speedometer", typeof(RectTransform));
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(Size, Size);

        CreateImage("Plate", root.transform, circle, PlateColor, 300f);

        Image track = CreateImage("Track", root.transform, circle, TrackColor, 280f);
        ConfigureRing(track);

        Image gauge = CreateImage("Gauge", root.transform, circle, Color.green, 280f);
        ConfigureRing(gauge);

        CreateImage("Inner", root.transform, circle, InnerColor, 220f);

        TMP_Text speedText = CreateText("SpeedText", root.transform, "0", 84f, Color.white,
            FontStyles.Bold, new Vector2(0f, 12f), new Vector2(200f, 100f));

        CreateText("UnitText", root.transform, "км/ч", 26f, MutedText,
            FontStyles.Normal, new Vector2(0f, -48f), new Vector2(200f, 40f));

        CreateText("MinLabel", root.transform, "0", 20f, MutedText,
            FontStyles.Normal, new Vector2(-80f, -118f), new Vector2(60f, 30f));

        TMP_Text maxLabel = CreateText("MaxLabel", root.transform, "200", 20f, MutedText,
            FontStyles.Normal, new Vector2(80f, -118f), new Vector2(60f, 30f));

        Speedometer speedometer = root.AddComponent<Speedometer>();
        SerializedObject so = new SerializedObject(speedometer);
        so.FindProperty("_speedText").objectReferenceValue = speedText;
        so.FindProperty("_gauge").objectReferenceValue = gauge;
        so.FindProperty("_maxLabel").objectReferenceValue = maxLabel;
        so.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory(Folder);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.Refresh();
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"Префаб спидометра создан: {PrefabPath}", prefab);
    }

    private static void ConfigureRing(Image image)
    {
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Radial360;
        image.fillOrigin = (int)Image.Origin360.Bottom;
        image.fillClockwise = true;
        image.fillAmount = Sweep;
        image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, StartAngle);
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color, float size)
    {
        RectTransform rect = CreateRect(name, parent, new Vector2(size, size), Vector2.zero);

        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text CreateText(string name, Transform parent, string text, float fontSize,
        Color color, FontStyles style, Vector2 position, Vector2 size)
    {
        RectTransform rect = CreateRect(name, parent, size, position);

        TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }
}
#endif
