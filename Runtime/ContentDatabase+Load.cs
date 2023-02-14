using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using static AssetInfo;
using Object = UnityEngine.Object;

public partial class ContentDatabase : ScriptableObject
{
    /* Asset */
    public static void LoadAsset<T>(AssetInfo assetInfo, Action<Status, string, float, T> Event) where T : Object
    {
#if UNITY_EDITOR
        if (Get().UseEditorDatabase)
        {
            CoroutineQueueExecute.PushToQueue(Coroutine_LoadAsset(assetInfo, null, null, Event));
            return;
        }
#endif

        CoroutineQueueExecute.PushToQueue(Coroutine_LoadBundle(assetInfo,
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
                CoroutineQueueExecute.PushToQueue(Coroutine_LoadAsset(assetInfo, chain, bundle, Event));
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

            CoroutineQueueExecute.PushToQueue(Coroutine_LoadBundle(info,
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
                    CoroutineQueueExecute.PushToQueue(Coroutine_LoadScene(info, chain, bundle, Event, mode, activateOnLoad));
                }
            }));
        }
        else
        {
            for(var i = 0; i < SceneManager.sceneCount; i++)
            {
                if(SceneManager.GetSceneAt(i).name == name || SceneManager.GetSceneAt(i).path == name)
                {
                    CoroutineQueueExecute.PushToQueue(Coroutine_LoadScene(SceneManager.GetSceneAt(i).name, Event, mode, activateOnLoad));
                    return;
                }
            }
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

    public static void UnloadAll(bool unloadObjects = false)
    {
        foreach (var kvp in Get().loadedAssetBundles)
        {
            kvp.Value.AssetBundle?.Unload(unloadObjects);
        }
    }

    public static IEnumerator Coroutine_LoadBundle(AssetBundleManifest manifest, string folder, string bundleName, 
        Action<Status, string, float, AssetBundle> Event)
    {
        //load main bundle
        AssetBundle main_bundle = null;

        var main_bundle_path = Path.Combine(folder, bundleName);

        if (File.Exists(main_bundle_path))
        {
            Log($"Bundle {main_bundle_path} found!");

            var start_load_time = Environment.TickCount;
            var operation = AssetBundle.LoadFromFileAsync(main_bundle_path);

            if (operation != null)
            {
                Log($"Loading {main_bundle_path}");
                while (!operation.isDone)
                {
                    try
                    {
                        Event(Status.BundleLoading, bundleName, operation.progress, null);
                    }
                    catch { }
                    yield return null;
                }

                if (operation.isDone)
                {
                    if (operation.assetBundle)
                    {
                        start_load_time = Environment.TickCount - start_load_time;
                        Log($"Loading {main_bundle_path} finished [Load time: {start_load_time} ms]");

                        main_bundle = operation.assetBundle;

                        try
                        {
                            Event(Status.BundleLoaded, bundleName, operation.progress, main_bundle);
                        }
                        catch { }
                    }
                    else
                    {
                        LogError($"Bundle {main_bundle_path} loading error!");
                        try
                        {
                            Event(Status.BundleLoadingError, bundleName, 0, null);
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
                    Event(Status.BundleLoadingError, bundleName, 0, null);
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
                Event(Status.BundleNotFound, bundleName, 0, null);
            }
            catch { }

            yield break;
        }

        //load deps
        var deps = manifest.GetAllDependencies(bundleName);
        int dependency_missing = 0;
        int dependency_ready = 0;
        int dependency_count = deps.Length;
        Log($"Tracking {dependency_count} dependencies for {bundleName}");
        for (int i = 0; i < dependency_count; i++)
        {
            var dependency_chain = deps[i];
            var dependency_path = Path.Combine(folder, dependency_chain);

            if (File.Exists(dependency_path))
            {
                Log($"Dependency {dependency_path} of {bundleName} found!");
                var start_load_time = DateTime.Now.Millisecond;
                var operation = AssetBundle.LoadFromFileAsync(dependency_path);

                if (operation != null)
                {
                    Log($"Loading {dependency_path} dependency of {bundleName}");
                    while (!operation.isDone)
                    {
                        try
                        {
                            Event(Status.DependencyLoading, dependency_chain, ((float)i / (float)dependency_count), null);
                        }
                        catch { }
                        yield return null;
                    }

                    if (operation.isDone)
                    {
                        if (operation.assetBundle)
                        {
                            dependency_ready++;

                            start_load_time = DateTime.Now.Millisecond - start_load_time;

                            Log($"Loading {dependency_path} dependency of {bundleName} finished [Load time: {start_load_time} ms]");

                            try
                            {
                                Event(Status.DependencyLoaded, dependency_chain, operation.progress, operation.assetBundle);
                            }
                            catch { }
                        }
                        else
                        {
                            LogError($"Dependency {dependency_path} of {bundleName} loading error!");
                            try
                            {
                                Event(Status.DependencyLoadingError, dependency_chain, 0, null);
                            }
                            catch { }
                        }
                    }
                }
                else
                {
                    LogError($"Dependency {dependency_path} of {bundleName} loading error!");

                    try
                    {
                        Event(Status.DependencyLoadingError, dependency_chain, 0, null);
                    }
                    catch { }
                }
            }
            else
            {
                dependency_missing++;
                LogError($"Dependency {dependency_path} of {bundleName} not found!");

                try
                {
                    Event(Status.DependencyNotFound, dependency_chain, 0, null);
                }
                catch { }
            }
        }

        if(main_bundle != null)
        {
            LogWarning($"Bundle {bundleName} ready! [Dependencies Ready: {dependency_ready}] [Missing dependencies: {dependency_missing}] [Total dependencies: {dependency_count}]");

            try
            {
                Event(Status.BundleReady, bundleName, 1, main_bundle);
            }
            catch { }
        }

        yield break;
    }

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

                var start_load_time = Environment.TickCount;
                var operation = AssetBundle.LoadFromFileAsync(main_bundle_path);

                if (operation != null)
                {
                    Log($"Loading {main_bundle_path}");
                    while (!operation.isDone)
                    {
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
                            start_load_time = Environment.TickCount - start_load_time;
                            Log($"Loading {main_bundle_path} finished [Load time: {start_load_time} ms]");

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
            main_bundle = loadedBundle.AssetBundle;
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

        var dependencies_Load_time = Environment.TickCount;
        for (int i = 0; i < dependency_count; i++)
        {
            var dependency_chain = dependencies_list[i];
            
            if(dependency_chain.AssetInfo.type == typeof(Material).Name)
            {
                continue;
            }

            if (dependency_chain.AssetInfo.type == typeof(Shader).Name)
            {
                continue;
            }

            if (dependency_chain.AssetInfo.type == typeof(ComputeShader).Name)
            {
                continue;
            }

            if (dependency_chain.AssetInfo.base_type == typeof(Texture).Name)
            {
                continue;
            }

            if (dependency_chain.AssetInfo.type == typeof(AudioClip).Name)
            {
                continue;
            }

            var dependency_path = Path.Combine(ContentFolder, dependency_chain.Name);

            if (!Get().loadedAssetBundles.TryGetValue(dependency_chain, out LoadedAssetBundle loadedDependencyBundle))
            {
                if (File.Exists(dependency_path))
                {
                    Log($"Dependency {dependency_path} of {chain.Name} found!");
                    var start_load_time = DateTime.Now.Millisecond;
                    var operation = AssetBundle.LoadFromFileAsync(dependency_path);

                    if (operation != null)
                    {
                        Log($"Loading {dependency_path} dependency of {chain.Name}");
                        while (!operation.isDone)
                        {
                            try
                            {
                                Event(Status.DependencyLoading, dependency_chain.Name, ((float)i / (float)dependency_count), null, dependency_chain);
                            }
                            catch { }
                            yield return null;
                        }

                        if (operation.isDone)
                        {
                            if (operation.assetBundle)
                            {
                                dependency_ready++;

                                start_load_time = DateTime.Now.Millisecond - start_load_time;

                                Log($"Loading {dependency_path} dependency of {chain.Name} finished [Load time: {start_load_time} ms]");

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

        dependencies_Load_time = Environment.TickCount - dependencies_Load_time;
        LogWarning($"Bundle {chain.Name} ready! [Dependencies Ready: {dependency_ready}] [Missing dependencies: {dependency_missing}] [Total dependencies: {dependency_count}] [Load time: {dependencies_Load_time} ms]");

        try
        {
            Event(Status.BundleReady, chain.Name, 1, main_bundle, chain);
        }
        catch { }

        yield break;
    }

    static IEnumerator Coroutine_LoadAsset<T>(AssetInfo assetInfo, BundleInfo info, AssetBundle bundle, Action<Status, string, float, T> Event) where T : Object
    {
#if UNITY_EDITOR

        if (Get().UseEditorDatabase)
        {
            var assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(assetInfo.guid);
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(assetPath);

            if (asset)
            {
                Event?.Invoke(Status.AssetLoaded, assetInfo.name, 1, asset);
            }
            else
            {
                Event?.Invoke(Status.AssetNotFound, assetInfo.name, 0, null);
            }

            yield break;
        }
#endif
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
                    Log($"Loading asset {assetInfo.name} from bundle {info.Name}");
                    while (!operation.isDone)
                    {
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

    static IEnumerator Coroutine_LoadScene(string name, Action<Status, string, float> Event, LoadSceneMode mode = LoadSceneMode.Single, bool activateOnLoad = true)
    {
        var operation = SceneManager.LoadSceneAsync(name, mode);

        if (operation != null)
        {
            Log($"Loading scene {name}");
            while (!operation.isDone)
            {
                try
                {
                    Event(Status.SceneLoading, name, operation.progress);
                }
                catch { }
                yield return null;
            }

            if (operation.isDone)
            {
                Log($"Loading scene {name} finished!");

                try
                {
                    Event(Status.SceneLoaded, name, operation.progress);
                }
                catch { }
            }
        }
    }

    static IEnumerator Coroutine_LoadScene(AssetInfo assetInfo, BundleInfo info, AssetBundle bundle, Action<Status, string, float> Event, LoadSceneMode mode = LoadSceneMode.Single, bool activateOnLoad = true)
    {
#if UNITY_EDITOR
        if (Get().UseEditorDatabase)
        {
            var p = Path.ChangeExtension(assetInfo.path, string.Empty);

            p = p.Substring(0, p.Length - 1);
            p = p.Remove(0, 7);
            var operation = SceneManager.LoadSceneAsync(p, mode);

            if (operation != null)
            {
                Log($"Loading scene {assetInfo.name} from bundle {info.Name}");
                while (!operation.isDone)
                {
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
#endif


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
                    Log($"Loading scene {assetInfo.name} from bundle {info.Name}");
                    while (!operation.isDone)
                    {
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