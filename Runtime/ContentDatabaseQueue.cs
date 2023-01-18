using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ContentDatabaseQueue : MonoBehaviour
{
    private static ContentDatabaseQueue instance;
    public static ContentDatabaseQueue Get()
    {
        if (!instance)
        {
            instance = new GameObject(nameof(ContentDatabaseQueue)).AddComponent<ContentDatabaseQueue>();
            DontDestroyOnLoad(instance.gameObject);
        }
        return instance;
    }

    public List<IEnumerator> Queue = new List<IEnumerator>();

    IEnumerator Start()
    {
        while (true)
        {
            if (Queue.Count > 0)
            {
                var q = Queue[0];
                yield return Get().StartCoroutine(q);
                Queue.Remove(q);
            }
            yield return null;
        }
    }

    public static void PushToQueue(IEnumerator e)
    {
        Get().Queue.Add(e);
    }

    [SerializeField]
    private bool ShowLoaded;
    Vector2 scroll;
    private void OnGUI()
    {
        if (!ShowLoaded)
            return;

        scroll = GUILayout.BeginScrollView(scroll);
        foreach(var item in ContentDatabase.Get().loadedAssetBundles)
        {
            GUILayout.Box($"{item.Key.Name} -> {(item.Value.AssetBundle ? item.Value.AssetBundle.name : "bundle is null")}");
        }
        GUILayout.EndScrollView();
    }
}
