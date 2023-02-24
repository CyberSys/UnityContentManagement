using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ContentDatabase))]
[CanEditMultipleObjects]
public class ContentDatabaseInspector : Editor
{
    GUIStyle style;
    private void OnEnable()
    {
        ContentDatabase.Get().SelfCheck();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GUILayout.Space(50);
        if(GUILayout.Button("Build Content", GUILayout.Height(128)))
        {
            ContentDatabase.Get().BuildContent();
        }
    }
}