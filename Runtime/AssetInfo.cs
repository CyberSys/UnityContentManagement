using System;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class AssetInfo
{
    public string name;
    public string path;
    public string guid;
    public string base_type;
    public string type;

    public AssetInfo()
    {
        name = path = guid = base_type = type = string.Empty;
    }

    public override int GetHashCode()
    {
        int hash = 0;

        for (int i = 0; i < path.Length; i++)
        {
            hash += path[i] ^ i;
        }

        return hash;
    }

    public bool IsValid()
    {
        return path != null && path.Length > 0 && guid != null && guid.Length == 32;
    }

    public enum Status
    {
        Unknown,
        BundleNotFound,
        BundleLoadingError,
        BundleLoading,
        BundleDownloadingError,
        BundleDownloading,
        BundleDownloaded,
        BundleLoaded,
        BundleSkipped,
        BundleReady,

        DependencyNotFound,
        DependencyLoadingError,
        DependencyLoading,
        DependencyDownloadingError,
        DependencyDownloading,
        DependencyDownloaded,
        DependencyLoaded,
        DependencySkipped,

        AssetNotFound,
        AssetLoadingError,
        AssetLoading,
        AssetLoaded,
        AssetCaching,
        AssetCached,

        SceneNotFound,
        SceneLoading,
        SceneLoaded,
        SceneAlreadyLoaded,

        InstantiatingError,
        Instantiated
    }

    [NonSerialized]
    private bool StillCaching = false;

    [NonSerialized]
    private Object CachedAsset;

    public void LoadAssetAndCache<T>(Action<Status, string, float, T> onState = null) where T : Object
    {
        if (!IsValid())
        {
            return;
        }

        if (!CachedAsset)
        {
            if (!StillCaching)
            {
                StillCaching = true;

                ContentDatabase.LoadAsset(this, delegate (Status status, string name, float progress, T asset)
                {
                    if (status == Status.AssetLoaded)
                    {
                        CachedAsset = asset;
                        if (onState != null)
                        {
                            onState(Status.AssetLoaded, name, progress, asset);
                        }
                        if (onState != null)
                        {
                            onState(Status.AssetCached, name, progress, asset);
                        }
                    }
                    else
                    {
                        if (onState != null)
                        {
                            onState(status, name, progress, asset);
                        }
                    }
                });
            }
            else
            {
                if (onState != null)
                {
                    onState(Status.AssetCaching, name, 0, null);
                }
            }
        }
        else
        {
            if (onState != null)
            {
                onState(Status.AssetCached, CachedAsset.name, 100, CachedAsset as T);
            }
        }
    }

    public void LoadAsset<T>(Action<Status, string, float, T> onState = null) where T : Object
    {
        if (!IsValid())
        {
            return;
        }
        ContentDatabase.LoadAsset(this, onState);
    }

    public void Instantiate<T>(Action<Status, string, float, T> onState = null) where T : Object
    {
        if (!IsValid())
        {
            return;
        }

        ContentDatabase.Instantiate(this, onState);
    }

    public void InstantiateAndCache<T>(Action<Status, string, float, T> onState = null) where T : Object
    {
        if (!IsValid())
        {
            return;
        }

        LoadAssetAndCache<T>(delegate (Status status, string name, float progress, T asset)
        {
            if (status == Status.AssetCached)
            {
                T obj = null;

                try
                {
                    obj = Object.Instantiate(CachedAsset as T);
                }
                catch
                {
                    if (onState != null) { onState(Status.InstantiatingError, name, 0, null); }
                }

                if (onState != null) { onState(Status.Instantiated, name, progress, obj); }
            }
            else
            {
                if(onState != null) { onState(status, name, progress, asset); }
            }
        });
    }

#if UNITY_EDITOR
    public bool HasAsset()
    {
        return AssetDatabase.GUIDToAssetPath(guid).Length > 0;
    }

    public T GetEditorAsset<T>() where T : Object
    {
        var assetPath = AssetDatabase.GUIDToAssetPath(guid);
        var asset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(T));
        return (T)asset;
    }
#endif
}
