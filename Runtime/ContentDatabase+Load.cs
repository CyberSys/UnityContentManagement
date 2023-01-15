using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using static AssetInfo;
using Object = UnityEngine.Object;

public partial class ContentDatabase : ScriptableObject
{
    /* Asset */
    public static void LoadAsset<T>(AssetInfo assetInfo, Action<Status, string, float, T> Event) where T : Object
    {
        ContentDatabaseQueue.PushToQueue(Coroutine_LoadBundle(assetInfo,
        delegate (Status status, string name, float progress, AssetBundle bundle, BundleInfo chain)
        {
            if (status != Status.BundleReady)
            {
                try
                {
                    Event(status, name, progress, null);
                }
                catch { }
            }
            else if (status == Status.BundleReady || status == Status.BundleSkipped)
            {
                ContentDatabaseQueue.PushToQueue(Coroutine_LoadAsset(assetInfo, chain, bundle, Event));
            }
        }));
    }

    public static void LoadAsset<T>(string name, Action<Status, string, float, T> Event) where T : Object
    {
        if (TryGetAssetInfoByName(name, out var info))
        {
            LoadAsset<T>(info, Event);
        }
        else
        {
            try
            {
                Event(Status.AssetNotFound, name, 0, null);
            }
            catch { }
        }
    }

    public static void LoadAsset(AssetInfo assetInfo, Action<Status, string, float, Object> Event)
    {
        LoadAsset<Object>(assetInfo, Event);
    }

    public static void Instantiate<T>(AssetInfo info, Action<Status, string, float, T> Event) where T : Object
    {
        LoadAsset<T>(info, delegate (Status status, string asset_name, float progress, T asset)
        {
            if (status != Status.AssetLoaded)
            {
                try
                {
                    Event(status, asset_name, progress, asset);
                }
                catch { }
            }
            else if (status == Status.AssetLoaded)
            {
                try
                {
                    Event(Status.Instantiated, info.name, progress, Instantiate(asset));
                }
                catch { Event(Status.InstantiatingError, info.name, progress, asset); }
            }
        });
    }

    public static void Instantiate<T>(string name, Action<Status, string, float, T> Event) where T : Object
    {
        if (TryGetAssetInfoByName(name, out var assetInfo))
        {
            Instantiate<T>(assetInfo, Event);
        }
    }

    public static void Instantiate(AssetInfo info, Action<Status, string, float, Object> Event)
    {
        Instantiate<Object>(info, Event);
    }

    public static void LoadScene(string name, Action<Status, string, float> Event, LoadSceneMode mode = LoadSceneMode.Single, bool activateOnLoad = true)
    {
        if (TryGetAssetInfoByName(name, out var info))
        {
            if (SceneManager.GetActiveScene().name == name || SceneManager.GetActiveScene().path == info.path || SceneManager.GetActiveScene().name == info.guid)
            {
                try
                {
                    Event(Status.SceneAlreadyLoaded, name, 1);
                }
                catch { }
                return;
            }

            ContentDatabaseQueue.PushToQueue(Coroutine_LoadBundle(info,
            delegate (Status status, string name, float progress, AssetBundle bundle, BundleInfo chain)
            {
                if (status != Status.BundleReady)
                {
                    try
                    {
                        Event(status, name, progress);
                    }
                    catch { }
                }
                else if (status == Status.BundleReady || status == Status.BundleSkipped)
                {
                    ContentDatabaseQueue.PushToQueue(Coroutine_LoadScene(info, chain, bundle, Event, mode, activateOnLoad));
                }
            }));
        }
        else
        {
            try
            {
                Event(Status.SceneNotFound, name, 0);
            }
            catch { }
        }
    }

    static bool IsBundleLoaded(string name, out AssetBundle bundle)
    {
        bundle = null;
        foreach (var i in AssetBundle.GetAllLoadedAssetBundles())
        {
            if (i.name == name)
            {
                bundle = i;
                return true;
            }
        }

        return false;
    }

    public class LoadedAssetBundle
    {
        public string Name { get; private set; }
        public AssetBundle AssetBundle { get; private set; }
        public uint ReferenceCount { get; private set; }

        public void IncreaseReferences()
        {
            ReferenceCount++;
        }

        public void DecreaseReferences()
        {
            ReferenceCount--;
        }

        public LoadedAssetBundle(string name, AssetBundle bundle)
        {
            Name = name;
            AssetBundle = bundle;
            ReferenceCount = 1;
        }
    }

    public Dictionary<BundleInfo, LoadedAssetBundle> loadedAssetBundles = new Dictionary<BundleInfo, LoadedAssetBundle>();

    public static IEnumerator Coroutine_LoadBundle(AssetInfo info, Action<Status, string, float, AssetBundle, BundleInfo> Event)
    {
        //build dependencies order of main bundle
        var dependencies_list = new List<BundleInfo>();

        BundleInfo chain;
        if (Get().TryGetChain(info.guid, out chain))
        {
            dependencies_list.AddRange(chain.Dependencies);
        }
        else
        {
            Log($"Can't find chain {info.guid}");
            yield break;
        }

        //load main bundle
        AssetBundle main_bundle = null;

        var main_bundle_path = Path.Combine(ContentFolder, chain.Name);
        if (!Get().loadedAssetBundles.TryGetValue(chain, out LoadedAssetBundle loadedBundle))
        {
            if (File.Exists(main_bundle_path))
            {
                Log($"Bundle {main_bundle_path} found!");

                var operation = AssetBundle.LoadFromFileAsync(main_bundle_path);

                if (operation != null)
                {
                    while (!operation.isDone)
                    {
                        Log($"Loading {main_bundle_path} | {(int)(operation.progress * 100f)}%");
                        try
                        {
                            Event(Status.BundleLoading, chain.Name, operation.progress, null, chain);
                        }
                        catch { }
                        yield return null;
                    }

                    if (operation.isDone)
                    {
                        if (operation.assetBundle)
                        {
                            Log($"Loading {main_bundle_path} finished");

                            main_bundle = operation.assetBundle;
                            Get().loadedAssetBundles.Add(chain, new LoadedAssetBundle(chain.Name, main_bundle));
                            try
                            {
                                Event(Status.BundleLoaded, chain.Name, operation.progress, main_bundle, chain);
                            }
                            catch { }
                        }
                        else
                        {
                            LogError($"Bundle {main_bundle_path} loading error!");
                            try
                            {
                                Event(Status.BundleLoadingError, chain.Name, 0, null, chain);
                            }
                            catch { }
                            yield break;
                        }
                    }
                }
                else
                {
                    LogError($"Bundle {main_bundle_path} loading error!");
                    try
                    {
                        Event(Status.BundleLoadingError, chain.Name, 0, null, chain);
                    }
                    catch { }
                    yield break;
                }
            }
            else
            {
                LogError($"Bundle {main_bundle_path} not found!");
                try
                {
                    Event(Status.BundleNotFound, chain.Name, 0, null, chain);
                }
                catch { }

                yield break;
            }
        }
        else
        {
            LogWarning($"Bundle {main_bundle_path} of {chain.Name} skipped!");
            try
            {
                Event(Status.BundleSkipped, chain.Name, 0, main_bundle, chain);
            }
            catch { }
        }

        int dependency_missing = 0;
        int dependency_ready = 0;
        int dependency_count = dependencies_list.Count;

        Log($"Tracking {dependency_count} dependencies for {chain.Name}");

        for (int i = 0; i < dependency_count; i++)
        {
            var dependency_chain = dependencies_list[i];
            var dependency_path = Path.Combine(ContentFolder, dependency_chain.Name);

            if (!Get().loadedAssetBundles.TryGetValue(dependency_chain, out LoadedAssetBundle loadedDependencyBundle))
            {
                if (File.Exists(dependency_path))
                {
                    Log($"Dependency {dependency_path} of {chain.Name} found!");
                    var operation = AssetBundle.LoadFromFileAsync(dependency_path);

                    if (operation != null)
                    {
                        while (!operation.isDone)
                        {
                            Log($"Loading {dependency_path} dependency of {chain.Name} | {(int)(operation.progress * 100f)}%");
                            try
                            {
                                Event(Status.DependencyLoading, dependency_chain.Name, ((float)i/(float)dependency_count), null, dependency_chain);
                            }
                            catch { }
                            yield return null;
                        }

                        if (operation.isDone)
                        {
                            if (operation.assetBundle)
                            {
                                dependency_ready++;
                                Log($"Loading {dependency_path} dependency of {chain.Name} finished");

                                Get().loadedAssetBundles.Add(dependency_chain, new LoadedAssetBundle(dependency_chain.Name, operation.assetBundle));

                                try
                                {
                                    Event(Status.DependencyLoaded, dependency_chain.Name, operation.progress, operation.assetBundle, dependency_chain);
                                }
                                catch { }
                            }
                            else
                            {
                                LogError($"Dependency {dependency_path} of {chain.Name} loading error!");
                                try
                                {
                                    Event(Status.DependencyLoadingError, dependency_chain.Name, 0, null, dependency_chain);
                                }
                                catch { }
                            }
                        }
                    }
                    else
                    {
                        LogError($"Dependency {dependency_path} of {chain.Name} loading error!");

                        try
                        {
                            Event(Status.DependencyLoadingError, dependency_chain.Name, 0, null, dependency_chain);
                        }
                        catch { }
                    }
                }
                else
                {
                    dependency_missing++;
                    LogError($"Dependency {dependency_path} of {chain.Name} not found!");

                    try
                    {
                        Event(Status.DependencyNotFound, dependency_chain.Name, 0, null, dependency_chain);
                    }
                    catch { }
                }
            }
            else
            {
                LogWarning($"Dependency {dependency_path} of {chain.Name} skipped!");
                try
                {
                    Event(Status.DependencySkipped, dependency_chain.Name, 0, loadedDependencyBundle.AssetBundle, dependency_chain);
                }
                catch { }
            }
        }

        LogWarning($"Bundle {chain.Name} ready! [Dependencies Ready: {dependency_ready}] [Missing dependencies: {dependency_missing}] [Total dependencies: {dependency_count}]");

        try
        {
            Event(Status.BundleReady, chain.Name, 1, main_bundle, chain);
        }
        catch { }

        yield break;
    }

    static IEnumerator Coroutine_LoadAsset<T>(AssetInfo assetInfo, BundleInfo info, AssetBundle bundle, Action<Status, string, float, T> Event) where T : Object
    {
        if (bundle.isStreamedSceneAssetBundle)
        {
            LogError($"Can't load asset {assetInfo.name}! Bundle {bundle.name} only for load scenes!");
            try
            {
                Event(Status.AssetLoadingError, assetInfo.name, 0, null);
            }
            catch { }
            yield break;
        }
        foreach (var guid in bundle.GetAllAssetNames())
        {
            if (guid == assetInfo.guid)
            {
                var operation = bundle.LoadAssetAsync<T>(guid);

                if (operation != null)
                {
                    while (!operation.isDone)
                    {
                        Log($"Loading asset {assetInfo.name} from bundle {info.Name} | {(int)(operation.progress * 100f)}%");
                        try
                        {
                            Event(Status.AssetLoading, assetInfo.name, operation.progress, null);
                        }
                        catch { }
                        yield return null;
                    }

                    if (operation.isDone)
                    {
                        Log($"Loading asset {assetInfo.name} from bundle {info.Name} finished!");

                        try
                        {
                            Event(Status.AssetLoaded, assetInfo.name, operation.progress, (T)operation.asset);
                        }
                        catch { }
                    }
                }

                yield break;
            }
        }

        Log($"Asset {assetInfo.name} not found in bundle {info.Name}");

        try
        {
            Event(Status.AssetNotFound, assetInfo.name, 0, null);
        }
        catch { }

        yield break;
    }

    static IEnumerator Coroutine_LoadScene(AssetInfo assetInfo, BundleInfo info, AssetBundle bundle, Action<Status, string, float> Event, LoadSceneMode mode = LoadSceneMode.Single, bool activateOnLoad = true)
    {
        if (!bundle.isStreamedSceneAssetBundle)
        {
            LogError($"Can't load scene {assetInfo.name}! Bundle {info.Name} only for load assets!");
            try
            {
                Event(Status.AssetLoadingError, assetInfo.name, 0);
            }
            catch { }
            yield break;
        }
        foreach (var guid in bundle.GetAllScenePaths())
        {
            if (guid == assetInfo.guid)
            {
                var operation = SceneManager.LoadSceneAsync(guid, mode);

                if (operation != null)
                {
                    while (!operation.isDone)
                    {
                        Log($"Loading scene {assetInfo.name} from bundle {info.Name} | {(int)(operation.progress * 100f)}%");
                        try
                        {
                            Event(Status.SceneLoading, assetInfo.name, operation.progress);
                        }
                        catch { }
                        yield return null;
                    }

                    if (operation.isDone)
                    {
                        Log($"Loading scene {assetInfo.name} from bundle {info.Name} finished!");

                        try
                        {
                            Event(Status.SceneLoaded, assetInfo.name, operation.progress);
                        }
                        catch { }
                    }
                }

                yield break;
            }
        }

        Log($"Scene {assetInfo.name} not found in bundle {info.Name}");

        try
        {
            Event(Status.SceneNotFound, assetInfo.name, 0);
        }
        catch { }

        yield break;
    }
}