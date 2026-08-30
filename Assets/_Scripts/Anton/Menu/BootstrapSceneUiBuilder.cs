#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class BootstrapSceneUiBuilder
{
    private const string ScenePath = "Assets/_Scripts/Anton/0_BootStrap 1.unity";

    [MenuItem("Tools/Anton/Build Bootstrap Loading UI")]
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var loader = Object.FindObjectOfType<BootstrapLoader>();
        if (loader == null)
            throw new MissingComponentException("BootstrapLoader was not found in " + ScenePath);

        var oldCanvas = GameObject.Find("Bootstrap Canvas");
        if (oldCanvas != null)
            Object.DestroyImmediate(oldCanvas);

        var canvasObject = new GameObject("Bootstrap Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        var backgroundObject = new GameObject("Loading Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(AspectRatioFitter));
        backgroundObject.transform.SetParent(canvasObject.transform, false);
        var backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(1920f, 1080f);
        var background = backgroundObject.GetComponent<Image>();
        background.color = Color.black;
        background.raycastTarget = false;
        var aspect = backgroundObject.GetComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        aspect.aspectRatio = 16f / 9f;

        var barBackgroundObject = new GameObject("Loading Bar Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        barBackgroundObject.transform.SetParent(canvasObject.transform, false);
        var barBackgroundRect = barBackgroundObject.GetComponent<RectTransform>();
        barBackgroundRect.anchorMin = barBackgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        barBackgroundRect.sizeDelta = new Vector2(500f, 30f);
        barBackgroundObject.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

        var fillObject = new GameObject("Loading Bar Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObject.transform.SetParent(barBackgroundObject.transform, false);
        var fill = fillObject.GetComponent<RectTransform>();
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.anchoredPosition = Vector2.zero;
        fill.sizeDelta = Vector2.zero;
        fillObject.GetComponent<Image>().color = new Color(0.2f, 0.8f, 0.3f, 1f);

        var serializedLoader = new SerializedObject(loader);
        serializedLoader.FindProperty("bootstrapCanvas").objectReferenceValue = canvas;
        serializedLoader.FindProperty("backgroundImage").objectReferenceValue = background;
        serializedLoader.FindProperty("fillRect").objectReferenceValue = fill;
        serializedLoader.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Bootstrap loading UI saved to " + ScenePath);
    }
}
#endif
