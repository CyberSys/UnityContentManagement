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

        SceneNotFound,
        SceneLoading,
        SceneLoaded,
        SceneAlreadyLoaded,

        InstantiatingError,
        Instantiated
    }

    public void LoadAsset<T>(Action<Status, string, float, T> onState = null) where T : Object
    {
        ContentDatabase.LoadAsset(this, onState);
    }

    public void Instantiate<T>(Action<Status, string, float, T> onState = null) where T : Object
    {
        ContentDatabase.Instantiate(this, onState);
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
