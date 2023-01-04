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
        BundleLoadingError,
        BundleLoading,
        BundleLoaded,

        AssetLoadingError,
        AssetLoading,
        AssetLoaded,


        InstantiatingError,
        Instantiating,
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
    public T GetAsset<T>() where T : Object
    {
        var assetPath = AssetDatabase.GUIDToAssetPath(guid);
        var asset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(T));
        return (T)asset;
    }
#endif
}
