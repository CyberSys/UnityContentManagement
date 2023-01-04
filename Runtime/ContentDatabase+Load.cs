using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static AssetInfo;
using Object = UnityEngine.Object;

public partial class ContentDatabase : ScriptableObject
{
    /* Asset */
    public static void LoadAsset<T>(string name, Action<Status, string, float, T> onState = null) where T : Object
    {
        if (TryGetAssetInfoByName(name, out var info))
        {
            ContentDatabaseProxy.RunCoroutine(Coroutine_TryLoadAsset(info, onState));
        }
    }

    public static void LoadAsset<T>(AssetInfo info, Action<Status, string, float, T> onState = null) where T : Object
    {
        ContentDatabaseProxy.RunCoroutine(Coroutine_TryLoadAsset(info, onState));
    }

    public static void LoadAsset(AssetInfo info, Action<Status, string, float, Object> onState = null)
    {
        ContentDatabaseProxy.RunCoroutine(Coroutine_TryLoadAsset(info, onState));
    }


    /* Instantiate */
    public static void Instantiate<T>(string name, Action<Status, string, float, T> onState = null) where T : Object
    {
        if (TryGetAssetInfoByName(name, out var info))
        {
            ContentDatabaseProxy.RunCoroutine(Coroutine_TryInstantiateAsset(info, onState));
        }
    }

    public static void Instantiate<T>(AssetInfo info, Action<Status, string, float, T> onState = null) where T : Object
    {
        ContentDatabaseProxy.RunCoroutine(Coroutine_TryInstantiateAsset(info, onState));
    }

    public static void Instantiate(AssetInfo info, Action<Status, string, float, Object> onState = null)
    {
        ContentDatabaseProxy.RunCoroutine(Coroutine_TryInstantiateAsset(info, onState));
    }

    static IEnumerator Coroutine_TryLoadAsset<T>(AssetInfo assetInfo, Action<Status, string, float, T> onState = null) where T : Object
    {
        Dictionary<DependencyInfo, AssetBundle> bundles = new Dictionary<DependencyInfo, AssetBundle>();

        IEnumerator _LoadBundle(DependencyInfo info)
        {
            if (bundles.ContainsKey(info))
            {
                Debug.LogWarning($"Skipping bundle {info.BundleName}");
                yield break;
            }

            var path = Path.Combine(ContentFolder, info.BundleName);

            if (File.Exists(path))
            {
                var operation = AssetBundle.LoadFromFileAsync(path);

                if (operation != null)
                {
                    while (!operation.isDone)
                    {
                        Debug.Log($"Load bundle {path} | {(int)(operation.progress * 100f)} %");

                        if (onState != null)
                        {
                            onState(Status.BundleLoading, info.BundleName, operation.progress * 100f, null);
                        }

                        yield return null;
                    }

                    bundles.Add(info, operation.assetBundle);

                    if (onState != null)
                    {
                        onState(Status.BundleLoaded, info.BundleName, operation.progress * 100f, null);
                    }
                }
                else
                {
                    Debug.LogWarning($"Loading bundle operation failed! -> {path} | Bundle is corrupted!");

                    if (onState != null)
                    {
                        onState(Status.BundleLoadingError, info.BundleName, 0, null);
                    }
                }
            }
            else
            {
                Debug.LogWarning($"File {path} not found!");

                if (onState != null)
                {
                    onState(Status.BundleLoadingError, info.BundleName, 0, null);
                }
            }
        }

        IEnumerator _LoadDependencies(DependencyInfo info)
        {
            yield return _LoadBundle(info);
            for (int i = 0; i < info.Dependencies.Count; i++)
            {
                yield return _LoadDependencies(info.Dependencies[i]);
            }
        }

        DependencyInfo target = null;

        for (int i = 0; i < Get().m_Chains.Count; i++)
        {
            if (Get().m_Chains[i].AssetInfo.name == assetInfo.name &&
                Get().m_Chains[i].AssetInfo.guid == assetInfo.guid &&
                (Get().m_Chains[i].AssetInfo.type == assetInfo.type ||
                Get().m_Chains[i].AssetInfo.base_type == assetInfo.base_type)
                )
            {
                target = Get().m_Chains[i];
                yield return _LoadDependencies(Get().m_Chains[i]);
            }
        }

        if (target != null && bundles.ContainsKey(target))
        {
            if (onState != null)
            {
                var operation = bundles[target].LoadAssetAsync<T>(target.AssetInfo.guid);
                if (operation != null)
                {
                    while (!operation.isDone)
                    {
                        Debug.Log($"Load asset {target.AssetInfo.name} from bundle {target.BundleName} | {(int)(operation.progress * 100f)}%");
                        onState(Status.AssetLoading, target.AssetInfo.name, operation.progress * 100f, null);
                        yield return null;
                    }

                    onState(Status.AssetLoaded, target.AssetInfo.name, 100f, (T)operation.asset);
                }
                else
                {
                    onState(Status.AssetLoadingError, target.AssetInfo.name, 0, null);
                }
            }
        }
        else
        {
            if (onState != null)
            {
                onState(Status.AssetLoadingError, target.AssetInfo.name, 0, null);
            }
        }

        //unload all
        foreach (var kvp in bundles)
        {
            var operation = kvp.Value.UnloadAsync(false);
            if (operation != null)
            {
                while (!operation.isDone)
                {
                    Debug.Log($"Unloading bundle {kvp.Key.BundleName}");
                    yield return null;
                }
            }
        }

        yield return null;
    }

    static IEnumerator Coroutine_TryInstantiateAsset<T>(AssetInfo assetInfo, Action<Status, string, float, T> onState = null) where T : Object
    {
        Dictionary<DependencyInfo, AssetBundle> bundles = new Dictionary<DependencyInfo, AssetBundle>();

        IEnumerator _LoadBundle(DependencyInfo info)
        {
            if (bundles.ContainsKey(info))
            {
                Debug.LogWarning($"Skipping bundle {info.BundleName}");
                yield break;
            }

            var path = Path.Combine(ContentFolder, info.BundleName);

            if (File.Exists(path))
            {
                var operation = AssetBundle.LoadFromFileAsync(path);

                if (operation != null)
                {
                    while (!operation.isDone)
                    {
                        Debug.Log($"Load bundle {path} | {(int)(operation.progress * 100f)} %");

                        if (onState != null)
                        {
                            onState(Status.BundleLoading, info.BundleName, operation.progress * 100f, null);
                        }

                        yield return null;
                    }

                    bundles.Add(info, operation.assetBundle);

                    if (onState != null)
                    {
                        onState(Status.BundleLoaded, info.BundleName, operation.progress * 100f, null);
                    }
                }
                else
                {
                    Debug.LogWarning($"Loading bundle operation failed! -> {path} | Bundle is corrupted!");

                    if (onState != null)
                    {
                        onState(Status.BundleLoadingError, info.BundleName, 0, null);
                    }
                }
            }
            else
            {
                Debug.LogWarning($"File {path} not found!");

                if (onState != null)
                {
                    onState(Status.BundleLoadingError, info.BundleName, 0, null);
                }
            }
        }

        IEnumerator _LoadDependencies(DependencyInfo info)
        {
            yield return _LoadBundle(info);
            for (int i = 0; i < info.Dependencies.Count; i++)
            {
                yield return _LoadDependencies(info.Dependencies[i]);
            }
        }

        DependencyInfo target = null;

        for (int i = 0; i < Get().m_Chains.Count; i++)
        {
            if (Get().m_Chains[i].AssetInfo.name == assetInfo.name &&
                Get().m_Chains[i].AssetInfo.guid == assetInfo.guid &&
                (Get().m_Chains[i].AssetInfo.type == assetInfo.type ||
                Get().m_Chains[i].AssetInfo.base_type == assetInfo.base_type)
                )
            {
                target = Get().m_Chains[i];
                yield return _LoadDependencies(Get().m_Chains[i]);
            }
        }

        if (target != null && bundles.ContainsKey(target))
        {
            var operation = bundles[target].LoadAssetAsync<T>(target.AssetInfo.guid);
            if (operation != null)
            {
                while (!operation.isDone)
                {
                    Debug.Log($"Instantiating asset {target.AssetInfo.name} from bundle {target.BundleName} | {(int)(operation.progress * 100f)}%");

                    if (onState != null)
                    {
                        onState(Status.Instantiating, target.AssetInfo.name, operation.progress * 100f, null);
                    }
                    yield return null;
                }

                try
                {
                    T obj = Instantiate((T)operation.asset);

                    if (onState != null)
                    {
                        onState(Status.Instantiated, target.AssetInfo.name, 100f, obj);
                    }
                }
                catch(Exception ex)
                {
                    Debug.LogException(ex);

                    if (onState != null)
                    {
                        onState(Status.InstantiatingError, ex.Message, 0, null);
                    }
                }
            }
            else if (onState != null)
            {
                onState(Status.InstantiatingError, target.AssetInfo.name, 0, null);
            }
        }
        else if (onState != null)
        {
            onState(Status.InstantiatingError, target.AssetInfo.name, 0, null);
        }

        //unload all
        foreach (var kvp in bundles)
        {
            var operation = kvp.Value.UnloadAsync(false);
            if (operation != null)
            {
                while (!operation.isDone)
                {
                    Debug.Log($"Unloading bundle {kvp.Key.BundleName}");
                    yield return null;
                }
            }
        }

        yield return null;
    }
}
