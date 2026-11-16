#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.TextCore.LowLevel;
using System.Collections.Generic;

public class EmojiSetupTool
{
    [MenuItem("Tools/TextMeshPro/Настроить Selected Font как Emoji Fallback")]
    public static void SetupSelectedEmojiFont()
    {
        Font sourceFont = Selection.activeObject as Font;
        if (sourceFont == null)
        {
            EditorUtility.DisplayDialog("Ошибка", "Сначала выдели файл шрифта (.ttf) с эмодзи в окне Project!", "ОК");
            return;
        }

        string fontPath = AssetDatabase.GetAssetPath(sourceFont);
        string assetPath = System.IO.Path.ChangeExtension(fontPath, null) + "_EmojiColor.asset";

        // 1. Создаём Color Font Asset (режим Color + шейдер Sprite)
        TMP_FontAsset colorFontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            90, // Sampling Point Size
            9,  // Padding
            GlyphRenderMode.COLOR,
            1024,
            1024,
            AtlasPopulationMode.Dynamic
        );

        if (colorFontAsset == null)
        {
            EditorUtility.DisplayDialog("Ошибка", "Не удалось создать Font Asset. Проверь, что шрифт поддерживает COLRv0.", "ОК");
            return;
        }

        colorFontAsset.name = sourceFont.name + "_EmojiColor";
        AssetDatabase.CreateAsset(colorFontAsset, assetPath);

        // Привязываем внутреннюю текстуру и материал к ассету
        if (colorFontAsset.atlasTexture != null)
        {
            colorFontAsset.atlasTexture.name = colorFontAsset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(colorFontAsset.atlasTexture, colorFontAsset);
        }
        if (colorFontAsset.material != null)
        {
            colorFontAsset.material.name = colorFontAsset.name + " Material";
            AssetDatabase.AddObjectToAsset(colorFontAsset.material, colorFontAsset);
        }

        // 2. Добавляем в TMP Settings -> Fallback Emoji Text Assets
        TMP_Settings settings = TMP_Settings.instance;
        if (settings != null)
        {
            SerializedObject serializedSettings = new SerializedObject(settings);
            SerializedProperty emojiFallbacks = serializedSettings.FindProperty("m_fallbackEmojiTextAssets");

            if (emojiFallbacks != null)
            {
                bool alreadyAdded = false;
                for (int i = 0; i < emojiFallbacks.arraySize; i++)
                {
                    if (emojiFallbacks.GetArrayElementAtIndex(i).objectReferenceValue == colorFontAsset)
                    {
                        alreadyAdded = true;
                        break;
                    }
                }

                if (!alreadyAdded)
                {
                    int index = emojiFallbacks.arraySize;
                    emojiFallbacks.InsertArrayElementAtIndex(index);
                    emojiFallbacks.GetArrayElementAtIndex(index).objectReferenceValue = colorFontAsset;
                    serializedSettings.ApplyModifiedProperties();
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(colorFontAsset);

        EditorUtility.DisplayDialog("Готово!", $"Создан ассет {colorFontAsset.name} и добавлен в Fallback Emoji Text Assets.", "Супер");
    }
}
#endif