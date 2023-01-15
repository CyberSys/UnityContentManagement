#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#endif

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
public partial class ContentDatabase : ScriptableObject, IPostprocessBuildWithReport
#endif
#if !UNITY_EDITOR
public partial class ContentDatabase : ScriptableObject
#endif
{
    public static string ContentFolder { get { return Path.Combine(Environment.CurrentDirectory, "Content"); } }
    public static string ContentDBPath { get { return Path.Combine(Environment.CurrentDirectory, "Content", "ContentDB.json"); } }

    [SerializeField]
    private List<AssetInfo> m_Assets = new List<AssetInfo>();

    public static int Assets => Get().m_Assets.Count;

    public AssetInfo this[int index]
    {
        get
        {
            return m_Assets[index];
        }
    }

    public enum BundlePlaceMode
    {
        OnlyGUID,
        OnlyGUIDWithoutExtension,
        Name_Type,
        Name
    }

#pragma warning disable CS0414
    [SerializeField]
    private BundlePlaceMode bundleNameMode = BundlePlaceMode.Name_Type;
#pragma warning restore CS0414

    [Header("Show loading progress in console/logs")]
    [SerializeField]
    private bool Logging = true;

#if UNITY_EDITOR
    [Header("Force Rebuilding")]
    [SerializeField]
    private bool ForceRebuild = true;

    [Header("Don't use compression")]
    [SerializeField]
    private bool Uncompressed = true;

    [Header("Don't write engine version")]
    [SerializeField]
    private bool StripUnityVersion = false;

    [Header("Remove .manifest")]
    [SerializeField]
    private bool RemoveManifest = false;

    [Header("Clear the content directory before building")]
    [SerializeField]
    private bool ClearContentDirectory = false;

    [Header("Bundles building target")]
    [SerializeField]
    private BuildTarget buildTarget = BuildTarget.StandaloneWindows;
#endif

    void ClearDirectory(string target_dir)
    {
        string[] files = Directory.GetFiles(target_dir);
        string[] dirs = Directory.GetDirectories(target_dir);

        foreach (string file in files)
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }

        foreach (string dir in dirs)
        {
            ClearDirectory(dir);
        }
    }

    [ContextMenu("CheckAndClearAssets")]
    public void CheckAndClearAssets()
    {
#if UNITY_EDITOR
        for(int i = 0; i < m_Assets.Count; i++)
        {
            var error = CheckAsset(
                AssetDatabase.LoadMainAssetAtPath(m_Assets[i].path),
                out var blob1, out var blob2, out var blob3);
            if (error != AssetError.NoError)
            {
                m_Assets.RemoveAt(i);
                Debug.LogWarning($"Asset {m_Assets[i].name} excluded from database! | {error}");
            }
        }
#endif
    }

    [ContextMenu("Reset")]
    void blob1()
    {

    }

    [ContextMenu("BuildContent")]
    public void BuildContent()
    {
#if UNITY_EDITOR
        var db = Get();

        var bundles = new List<AssetBundleBuild>();

        Dictionary<string, string> temp_GUID_AssetBundleName = new Dictionary<string, string>();

        for (var i = 0; i < Assets; i++)
        {
            var bundle = new AssetBundleBuild();
            bundle.assetBundleName = db[i].guid;
            {
                string ext = Path.GetExtension(db[i].path);

                if (bundleNameMode == BundlePlaceMode.Name_Type)
                {
                    string s_t = db[i].type;
                    string b_t = db[i].base_type;

                    if (s_t == nameof(GameObject))
                    {
                        s_t = "Object";
                    }

                    if (b_t == nameof(ScriptableObject))
                    {
                        s_t = "Scriptable";
                    }

                    temp_GUID_AssetBundleName.Add(db[i].guid, $"{db[i].name}{ext}");
                }

                if (bundleNameMode == BundlePlaceMode.OnlyGUID)
                {
                    temp_GUID_AssetBundleName.Add(db[i].guid, $"{db[i].guid}{ext}");
                }

                if (bundleNameMode == BundlePlaceMode.OnlyGUIDWithoutExtension)
                {
                    temp_GUID_AssetBundleName.Add(db[i].guid, db[i].guid);
                }


                if (bundleNameMode == BundlePlaceMode.Name)
                {
                    if (!temp_GUID_AssetBundleName.TryAdd(db[i].guid, $"{db[i].name}{ext}"))
                    {
                        temp_GUID_AssetBundleName.Add(db[i].guid, $"{db[i].name}_{db[i].guid}{ext}");
                    }
                }
            }

            bundle.assetNames = new string[1];
            bundle.assetNames[0] = db[i].path;
            bundle.addressableNames = new string[1];
            bundle.addressableNames[0] = db[i].guid;
            bundles.Add(bundle);
        }

        if (!Directory.Exists(ContentFolder))
        {
            Directory.CreateDirectory(ContentFolder);
        }
        else if (ClearContentDirectory)
        {
            ClearDirectory(ContentFolder);
        }

        var bopt = BuildAssetBundleOptions.None;

        if (Uncompressed)
        {
            bopt |= BuildAssetBundleOptions.UncompressedAssetBundle;
        }
        else
        {
            bopt |= BuildAssetBundleOptions.ChunkBasedCompression;
        }

        if (StripUnityVersion)
        {
            bopt |= BuildAssetBundleOptions.AssetBundleStripUnityVersion;
        }

        if (ForceRebuild)
        {
            bopt |= BuildAssetBundleOptions.ForceRebuildAssetBundle;
        }

        var manifest = CompatibilityBuildPipeline.BuildAssetBundles(ContentFolder, bundles.ToArray(), bopt, buildTarget);

        if (manifest != null)
        {
            //Building chains
            GenerateChains(manifest);

            //moving files
            if (Get().bundleNameMode == BundlePlaceMode.Name || Get().bundleNameMode == BundlePlaceMode.Name_Type)
            {
                foreach (var dictionary in temp_GUID_AssetBundleName)
                {
                    var file_path = Path.Combine(ContentFolder, dictionary.Key);
                    if (File.Exists(file_path))
                    {
                        string new_file_path = Path.Combine(ContentFolder, dictionary.Value);

                        if (File.Exists(new_file_path))
                        {
                            File.Delete(new_file_path);
                        }

                        File.Move(file_path, new_file_path);
                    }
                }
            }

            RenameDependencyChains(temp_GUID_AssetBundleName);
        }
        else
        {
            return;
        }

        if (RemoveManifest)
        {
            var manifests = Directory.GetFiles(ContentFolder, "*.manifest*");

            for (var i = 0; i < manifests.Length; i++)
            {
                File.Delete(manifests[i]);
            }
        }

        File.WriteAllText(ContentDBPath, JsonUtility.ToJson(this, true));
#endif
    }

    /* IO */
    public const string DBFolder = "Assets/ContentManagement/";
    public static string DBFile { get; private set; } = Path.Combine(DBFolder, "ContentDatabase.asset");

#if UNITY_EDITOR
    public static bool LoadFromJsonFile = false;
#endif

    static ContentDatabase db;
    public static ContentDatabase Get()
    {
#if UNITY_EDITOR
        if (!db)
        {
            if (!LoadFromJsonFile)
            {
                db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(DBFile);

                if (!db)
                {
                    db = CreateInstance<ContentDatabase>();

                    if (!Directory.Exists(DBFile))
                    {
                        Directory.CreateDirectory(DBFolder);
                    }

                    AssetDatabase.CreateAsset(db, DBFile);
                    AssetDatabase.SaveAssetIfDirty(db);
                }
            }
            else
            {
                db = CreateInstance<ContentDatabase>();
                if (File.Exists(ContentDBPath))
                {
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(ContentDBPath), db);
                }
                else
                {
                    Debug.LogError($"[ContentDatabase] {ContentDBPath} not found!");
                }
            }
        }
#endif
#if !UNITY_EDITOR
        if (!db)
        {
            db = CreateInstance<ContentDatabase>();
            if (File.Exists(ContentDBPath))
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(ContentDBPath), db);
            }
            else
            {
                Debug.LogError($"[ContentDatabase] {ContentDBPath} not found!");
            }
        }
#endif

        db.hideFlags = HideFlags.None;
        return db;
    }

#if UNITY_EDITOR
    public static void Save()
    {
        AssetDatabase.SaveAssetIfDirty(Get());
    }
#endif


    /* MANAGE */
#if UNITY_EDITOR
    public enum AssetError
    {
        Unknown,
        NoError,
        IsResourceAsset,
        IsUnsupportedType,
        IsEditorAsset,
        IsAlreadyContainsAsset,
        IsSceneAsset,
        MissingAsset
    }

    public static HashSet<Type> supportedTypes = new HashSet<Type>
    {
        typeof(AudioClip),
        typeof(AnimationClip),
        typeof(Shader),
        typeof(GameObject),
        typeof(ComputeShader),
        typeof(Texture),
        typeof(Font),
        typeof(Material),
        typeof(PhysicMaterial),
        typeof(TextAsset),
        typeof(ScriptableObject),
        typeof(SceneAsset),
        typeof(Object)
    };

    public static bool IsSupportedType(Type type)
    {
        return supportedTypes.Contains(type) || supportedTypes.Contains(type.BaseType);
    }

    public static bool IsResource(string path)
    {
        return
            path.Contains("/resources/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/resources", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsEditor(string path)
    {
        return
            path.Contains("/editor/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/editor", StringComparison.OrdinalIgnoreCase);
    }

    public AssetError CheckAsset(Object asset, out string guid, out string path, out Type type)
    {
        type = null;
        guid = string.Empty;
        path = string.Empty;

        if (asset == null)
        {
            return AssetError.MissingAsset;
        }
        else
        {
            if ((asset.hideFlags & HideFlags.DontSave) != 0)
            {
                return AssetError.IsUnsupportedType;
            }

            if (asset is GameObject)
            {
                var go = asset as GameObject;

                if (go.scene.IsValid())
                {
                    return AssetError.IsSceneAsset;
                }
            }

            if (asset.GetType() == typeof(MonoScript))
            {
                return AssetError.IsUnsupportedType;
            }

            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out guid, out long localId))
            {
                path = AssetDatabase.GetAssetPath(asset);
                type = AssetDatabase.GetMainAssetTypeAtPath(path);

                if (!IsSupportedType(type))
                {
                    return AssetError.IsUnsupportedType;
                }

                if (IsResource(path))
                {
                    return AssetError.IsResourceAsset;
                }

                if (IsEditor(path))
                {
                    return AssetError.IsEditorAsset;
                }

                return AssetError.NoError;
            }
        }

        return AssetError.Unknown;
    }

    /* Object */
    public static AssetError AddAsset(Object asset)
    {
        if (!Contains(asset))
        {
            var error = Get().CheckAsset(asset, out string guid, out string path, out Type type);
            if (error == AssetError.NoError)
            {
                if (asset is ScriptableObject)
                {
                    type = typeof(ScriptableObject);
                }
                Get().m_Assets.Add(new AssetInfo() 
                { 
                    name = Path.GetFileNameWithoutExtension(path), 
                    guid = guid, 
                    path = path, 
                    base_type = type.BaseType.Name, 
                    type = type.Name 
                });
                Save();
            }

            return error;
        }
        else
        {
            return AssetError.IsAlreadyContainsAsset;
        }
    }

    public static AssetError UpdateAsset(Object asset)
    {
        var error = Get().CheckAsset(asset, out string guid, out string path, out Type type);
        if (error != AssetError.NoError)
        {
            return error;
        }

        if(TryGetAssetInfo(asset, out var info))
        {
            info.name = Path.GetFileNameWithoutExtension(path);
            info.guid = guid;
            info.path = path;
            info.base_type = type.BaseType.Name;
            info.type = type.Name;
        }

        return AssetError.NoError;
    }

    public static void RemoveAsset(Object asset)
    {
        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId))
        {
            for (int i = 0; i < Get().m_Assets.Count; i++)
            {
                if (Get().m_Assets[i].guid == guid)
                {
                    Get().m_Assets.RemoveAt(i);
                }
            }
        }
    }
#endif

    public static void RemoveAsset(string asset_guid)
    {
        for (int i = 0; i < Get().m_Assets.Count; i++)
        {
            if (Get().m_Assets[i].guid == asset_guid)
            {
                Get().m_Assets.RemoveAt(i);
            }
        }
    }

#if UNITY_EDITOR
    public static bool Contains(Object asset)
    {
        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId))
        {
            for (int i = 0; i < Get().m_Assets.Count; i++)
            {
                if (Get().m_Assets[i].guid == guid)
                {
                    return true;
                }
            }
        }

        return false;
    }
#endif

    public static bool Contains(string guid)
    {
        for (int i = 0; i < Get().m_Assets.Count; i++)
        {
            if (Get().m_Assets[i].guid == guid)
            {
                return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    public static bool Contains(Object asset, out AssetInfo info)
    {
        info = null;
        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId))
        {
            for (int i = 0; i < Get().m_Assets.Count; i++)
            {
                if (Get().m_Assets[i].guid == guid)
                {
                    info = Get().m_Assets[i];
                    return true;
                }
            }
        }

        return false;
    }
#endif

    public static bool Contains(string guid, out AssetInfo info)
    {
        info = null;
        for (int i = 0; i < Get().m_Assets.Count; i++)
        {
            if (Get().m_Assets[i].guid == guid)
            {
                info = Get().m_Assets[i];
                return true;
            }
        }

        return false;
    }

    public static bool TryGetAssetInfo(int index, out AssetInfo info)
    {
        info = null;

        if (index >= 0)
        {
            if (index < Get().m_Assets.Count)
            {
                info = Get().m_Assets[index];
                return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    public static bool TryGetAssetInfo(Object asset, out AssetInfo info)
    {
        info = null;

        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long blob))
        {
            if(TryGetAssetInfoByGUID(guid, out info))
            {
                return true;
            }
        }

        return false;
    }
#endif

    public static bool TryGetAssetInfoByName(string name, out AssetInfo info, StringComparison stringComparison = StringComparison.Ordinal)
    {
        info = null;
        for (int i = 0; i < Get().m_Assets.Count; i++)
        {
            if (TryGetAssetInfo(i, out info))
            {
                if (info.name == name)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public static bool TryGetAssetInfoByNameAndType<T>(string name, Type type, out AssetInfo info, StringComparison stringComparison = StringComparison.Ordinal)
    {
        info = null;
        for (int i = 0; i < Get().m_Assets.Count; i++)
        {
            if (TryGetAssetInfo(i, out info))
            {
                if (info.name == name && info.type == type.Name)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public static bool TryGetAssetInfoByPath(string path, out AssetInfo info, StringComparison stringComparison = StringComparison.Ordinal)
    {
        info = null;
        for (int i = 0; i < Get().m_Assets.Count; i++)
        {
            if (TryGetAssetInfo(i, out info))
            {
                if (info.path == path)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public static bool TryGetAssetInfoByGUID(string guid, out AssetInfo info)
    {
        info = null;
        for (int i = 0; i < Assets; i++)
        {
            if (TryGetAssetInfo(i, out info))
            {
                if (info.guid == guid)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public static bool TryGetAssetInfoByGUID(string guid, out AssetInfo info, out int index)
    {
        index = -1;
        info = null;
        for (int i = 0; i < Assets; i++)
        {
            if (TryGetAssetInfo(i, out info))
            {
                if (info.guid == guid)
                {
                    index = i;
                    return true;
                }
            }
        }
        return false;
    }

#if UNITY_EDITOR
    int IOrderedCallback.callbackOrder => 0;

    void IPostprocessBuildWithReport.OnPostprocessBuild(BuildReport report)
    {

    }
#endif
}


#if UNITY_EDITOR
public partial class ContentDatabase : ScriptableObject, IPostprocessBuildWithReport
#endif
#if !UNITY_EDITOR
public partial class ContentDatabase : ScriptableObject
#endif
{
    public static void Log(string text)
    {
        if (!Get().Logging)
            return;

        Debug.Log($"[{nameof(ContentDatabase)}] {text}");
    }

    public static void LogWarning(string text)
    {
        if (!Get().Logging)
            return;

        Debug.LogWarning($"[{nameof(ContentDatabase)}] {text}");
    }

    public static void LogError(string text)
    {
        if (!Get().Logging)
            return;

        Debug.LogError($"[{nameof(ContentDatabase)}] {text}");
    }
}

#if UNITY_EDITOR
class ContentDatabasePostProcess : AssetPostprocessor
{
    static void MoveAssets(string asset_guid, string oldPath, string newPath)
    {
        if (ContentDatabase.TryGetAssetInfoByGUID(asset_guid, out var info))
        {
            var asset = AssetDatabase.LoadAssetAtPath(newPath, typeof(Object));

            var error = ContentDatabase.Get().CheckAsset(asset, out var guid, out var blob2, out var blob3);
            if (error == ContentDatabase.AssetError.NoError)
            {
                info.path = newPath;
            }
            else
            {
                DeleteAssets(asset_guid);
            }
        }
    }

    static void DeleteAssets(string asset_guid)
    {
        ContentDatabase.RemoveAsset(asset_guid);
    }

    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        for (int i = 0; i < movedAssets.Length; i++)
        {
            MoveAssets(AssetDatabase.AssetPathToGUID(movedAssets[i]), movedFromAssetPaths[i], movedAssets[i]);
        }

        for (int i = 0; i < deletedAssets.Length; i++)
        {
            DeleteAssets(AssetDatabase.AssetPathToGUID(deletedAssets[i]));
        }
        ContentDatabase.Save();
    }
}
#endif
