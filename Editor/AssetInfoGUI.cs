using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class AssetInspectorGUI
{
    static GUIContent s_IsolationText = new GUIContent("Can't include asset in content database in Isolation-mode");

    static GUIContent s_AssetNameText = new GUIContent("Asset Name: ", "Name for load by name");
    static AssetInspectorGUI()
    {
        Editor.finishedDefaultHeaderGUI += OnPostHeaderGUI;
    }

    static void SingleSelectedAsset(Object asset)
    {
        var selectedObject = asset;

        if (!selectedObject)
        {
            return;
        }

        if (selectedObject is ContentDatabase ||
            selectedObject is DefaultAsset ||
            selectedObject is MonoImporter ||
            selectedObject is GameObject
            )
        {
            return;
        }

        bool has_asset = ContentDatabase.Contains(selectedObject, out var group, out var info);

        if ((PrefabStageUtility.GetCurrentPrefabStage() != null && PrefabStageUtility.GetCurrentPrefabStage().mode == PrefabStage.Mode.InIsolation) && info != null && PrefabStageUtility.GetCurrentPrefabStage().assetPath == info.path)
        {
            EditorGUILayout.HelpBox(s_IsolationText.text, MessageType.Warning, true);
            return;
        }

        var asset_name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(selectedObject));

        GUILayout.BeginVertical();

        if (has_asset)
        {
            ContentDatabase.UpdateAsset(selectedObject);
            if (GUILayout.Button($"Exclude {asset_name} from Content Database ({group.name})"))
            {
                ContentDatabase.RemoveAsset(selectedObject);
            }

            GUILayout.BeginHorizontal("box");
            GUILayout.Label(s_AssetNameText);
            info.name = EditorGUILayout.TextField(info.name);
            GUILayout.EndHorizontal();
            EditorGUILayout.HelpBox($"Path: {info.path}\nGUID: {info.guid}\nType: {info.type}", MessageType.None, true);
        }
        else
        {
            if (GUILayout.Button($"Include {asset_name} in Content Database"))
            {
                var error = ContentDatabase.AddAsset(ContentDatabase.Get().GetContentInfo().AddGroup(asset_name), selectedObject);
            }
        }

        GUILayout.EndVertical();
    }

    static void MultipleAddAssets(Object[] selected)
    {
        int added = 0;
        int not_added = 0;

        for (int i = 0; i < selected.Length; i++)
        {
            var selectedObject = selected[i];

            if (selectedObject is ContentDatabase ||
            selectedObject is DefaultAsset ||
            selectedObject is MonoImporter ||
            selectedObject is GameObject
            )
            {
                continue;
            }

            if ((PrefabStageUtility.GetCurrentPrefabStage() != null && PrefabStageUtility.GetCurrentPrefabStage().mode == PrefabStage.Mode.InIsolation))
            {
                EditorGUILayout.HelpBox(s_IsolationText.text, MessageType.Warning, true);
                continue;
            }

            bool has_asset = ContentDatabase.Contains(selectedObject, out var group, out var info);

            if (has_asset)
            {
                added++;
                continue;
            }
            else
            {
                not_added++;
            }
        }

        GUI.enabled = true;
        if (GUILayout.Button($"Include {not_added} assets in Content Database"))
        {
            var group = ContentDatabase.Get().GetContentInfo().AddGroup("SharedAssets_"+ContentDatabase.Get().GetContentInfo().Groups.Count);
            for (int i = 0; i < selected.Length; i++)
            {
                var error = ContentDatabase.AddAsset(group, selected[i]);
                if (error != ContentDatabase.AssetError.NoError)
                {
                    var asset_name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(selected[i]));
                    Debug.LogWarning($"{asset_name} | {error}");
                }
            }
        }

        if (GUILayout.Button($"Exclude {added} from Content Database"))
        {
            for (int i = 0; i < selected.Length; i++)
            {
                ContentDatabase.RemoveAsset(selected[i]);
            }
        }
        GUI.enabled = false;
    }

    static void OnPostHeaderGUI(Editor editor)
    {
        var oldState = GUI.enabled;

        GUI.enabled = true;
        if(editor.targets.Length == 1)
        {
            SingleSelectedAsset(editor.targets[0]);
        }
        else
        {
            MultipleAddAssets(editor.targets);
        }
        GUI.enabled = oldState;
    }
}
