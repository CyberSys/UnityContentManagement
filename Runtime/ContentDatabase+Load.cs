using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

public partial class ContentDatabase : ScriptableObject
{
    public static void LoadAsset<T>(string name, Action<T> onLoadAsset) where T : Object
    {
        ContentDatabaseProxy.RunCoroutine(Coroutine_TryLoadAsset(name, onLoadAsset));
    }

    static IEnumerator Coroutine_TryLoadAsset<T>(string name, Action<T> onLoadAsset) where T : Object
    {
        Dictionary<DependencyInfo, AssetBundle> bundles = new Dictionary<DependencyInfo, AssetBundle>();

        IEnumerator _LoadBundle(DependencyInfo info)
        {
            if (bundles.ContainsKey(info))
            {
                Debug.LogWarning($"Skipping bundle {name}");
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
                        yield return null;
                    }

                    bundles.Add(info, operation.assetBundle);
                }
                else
                {
                    Debug.LogWarning($"Loading bundle operation failed! -> {path} | Bundle is corrupted!");
                }
            }
            else
            {
                Debug.LogWarning($"File {path} not found!");
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
            if (Get().m_Chains[i].AssetInfo.name == name &&
                (Get().m_Chains[i].AssetInfo.type == typeof(T).Name ||
                Get().m_Chains[i].AssetInfo.base_type == typeof(T).Name)
                )
            {
                target = Get().m_Chains[i];
                yield return _LoadDependencies(Get().m_Chains[i]);
            }
        }

        if (bundles.ContainsKey(target))
        {
            if (onLoadAsset != null)
            {
                var operation = bundles[target].LoadAssetAsync<T>(target.AssetInfo.guid);
                if (operation != null)
                {
                    while (!operation.isDone)
                    {
                        Debug.Log($"Load asset {name} from bundle {target.BundleName} | {(int)(operation.progress * 100f)}%");
                        yield return null;
                    }

                    onLoadAsset((T)operation.asset);
                }
            }
        }
        else
        {
            if (onLoadAsset != null)
            {
                onLoadAsset(null);
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
}
