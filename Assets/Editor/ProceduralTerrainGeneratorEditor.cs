using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ProceduralTerrainGenerator))]
public class ProceduralTerrainGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ProceduralTerrainGenerator generator = (ProceduralTerrainGenerator)target;

        EditorGUILayout.Space(10f);
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.35f, 0.8f, 0.45f);
        if (GUILayout.Button("Generate Terrain", GUILayout.Height(32f)))
        {
            Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Generate Procedural Terrain");
            generator.Generate();
            EditorUtility.SetDirty(generator);
        }

        GUI.backgroundColor = new Color(0.9f, 0.45f, 0.45f);
        if (GUILayout.Button("Clear Generated", GUILayout.Height(32f)))
        {
            Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Clear Procedural Terrain");
            generator.ClearGenerated();
            EditorUtility.SetDirty(generator);
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }
}