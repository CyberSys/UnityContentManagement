using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ContentDatabaseProxy : MonoBehaviour
{
    private static ContentDatabaseProxy instance;
    public static ContentDatabaseProxy Get()
    {
        if (!instance)
        {
            instance = new GameObject(nameof(ContentDatabaseProxy)).AddComponent<ContentDatabaseProxy>();
            DontDestroyOnLoad(instance.gameObject);
        }
        return instance;
    }

    public static void RunCoroutine(IEnumerator e)
    {
        Get().StartCoroutine(e);
    }

    public static void EndCoroutine(IEnumerator e)
    {
        Get().StopCoroutine(e);
    }

    private void OnGUI()
    {
        foreach(var item in AssetBundle.GetAllLoadedAssetBundles())
        {
            GUILayout.Box(item.name);
        }
    }
}
