#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Кнопки для RoadBuilder в инспекторе. Положи файл в папку Assets/Editor.
[CustomEditor(typeof(RoadBuilder))]
public class RoadBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var rb = (RoadBuilder)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Террейн", EditorStyles.boldLabel);

        if (GUILayout.Button("Прокопать холм под дорогой"))
            rb.CarveTerrain();
        if (GUILayout.Button("Вернуть террейн как был"))
            rb.RestoreTerrain();
        if (GUILayout.Button("Притянуть узлы сплайна к земле"))
            rb.SnapSplineToTerrain();

        EditorGUILayout.Space();
        if (GUILayout.Button("Забыть резервную копию рельефа"))
            rb.ForgetTerrainBackup();
        EditorGUILayout.HelpBox(
            "Первая прокопка сохраняет исходный рельеф в Assets/<имя террейна>_heights_backup.bytes. " +
            "Каждая следующая прокопка начинается с этой копии, поэтому после смены трассы можно резать заново. " +
            "Если ты лепил террейн вручную после прокопки, нажми «Забыть резервную копию».",
            MessageType.Info);
    }
}
#endif