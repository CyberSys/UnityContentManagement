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
using System.Threading.Tasks;
using UnityEngine.Networking;

#if UNITY_EDITOR
public partial class ContentDatabase : ScriptableObject, IPostprocessBuildWithReport
#endif
#if !UNITY_EDITOR
public partial class ContentDatabase : ScriptableObject
#endif
{

    public const string ContentFolderName = "Content";

    public static string ContentFolder { get { return Path.Combine(Environment.CurrentDirectory, ContentFolderName); } }
    public static string ContentDBPath { get { return Path.Combine(Environment.CurrentDirectory, ContentFolderName, "Content.json"); } }

    [Serializable]
    public class ContentInfo
    {
        [SerializeField]
        private List<AssetInfo> m_Assets = new List<AssetInfo>();

        [SerializeField]
        private List<BundleInfo> m_Chains = new List<BundleInfo>();

        public List<AssetInfo> Assets => m_Assets;
        public List<BundleInfo> Chains => m_Chains;
    }

    [HideInInspector]
    [SerializeField]
    private ContentInfo m_ContentInfo = new ContentInfo();

    public ContentInfo GetContentInfo() { return m_ContentInfo; }

    public static int Assets => Get().m_ContentInfo.Assets.Count;

    public AssetInfo this[int index]
    {
        get
        {
            return m_ContentInfo.Assets[index];
        }
    }

#if UNITY_EDITOR
    public enum BundlePlaceMode
    {
        OnlyGUID,
        OnlyGUIDWithoutExtension,
		GUID_Type,
        Name_Type,
        Name,
        RawPath
    }

	[Header("Use Editor Database")]
    public bool UseEditorDatabase = false;

#pragma warning disable CS0414
    [SerializeField]
    private BundlePlaceMode bundleNameMode = BundlePlaceMode.Name_Type;
#pragma warning restore CS0414

    [Header("Bundle File Extension")]
    [SerializeField]
    private string Extension = "bundle";

    [Header("Force Rebuild <color=red>(slow!!)</color>")]
    [SerializeField]
    private bool ForceRebuild = true;

    public enum CompressionType
    {
        Uncompressed,
        LZMA,
        LZ4
    }

    [Header("Compression Mode")]
    [Header("Uncompressed <color=green>(speed++)</color> <color=red>(size++)</color>")]
    [Header("LZ4 <color=yellow>(speed+) (size-)</color>")]
    [Header("LZMA <color=red>(speed--)</color> <color=green>(size--)")]
    [SerializeField]
    private CompressionType Compression = CompressionType.Uncompressed;

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
        for(int i = 0; i < m_ContentInfo.Assets.Count; i++)
        {
            if(m_ContentInfo.Assets[i] == null)
            {
                m_ContentInfo.Assets.RemoveAt(i);
                continue;
            }
            var error = CheckAsset(
                AssetDatabase.LoadMainAssetAtPath(m_ContentInfo.Assets[i].path),
                out var blob1, out var blob2, out var blob3);
            if (error != AssetError.NoError)
            {
                m_ContentInfo.Assets.RemoveAt(i);
                Debug.LogWarning($"Asset {m_ContentInfo.Assets[i].name} excluded from database! | {error}");
            }
        }
#endif
    }

    [ContextMenu("Reset")]
    void blob1()
    {

    }

    [ContextMenu("BuildContent")]
    public async void BuildContent()
    {
#if UNITY_EDITOR

        AssetDatabase.SaveAssets();

        try
        {
            var db = Get();

            var bundles = new List<AssetBundleBuild>();

            Dictionary<string, string> temp_GUID_AssetBundleName = new Dictionary<string, string>();

            EditorUtility.DisplayProgressBar("ContentDatabase -> Preparing...", "...", 0);

            for (var i = 0; i < Assets; i++)
            {
                if (db[i] == null)
                {
                    continue;
                }
                var bundle = new AssetBundleBuild();
                bundle.assetBundleName = db[i].guid;
                {
                    string ext = Extension;

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

                        var s = $"{db[i].name}_{s_t}.{ext}";

                        //check name dubl
                        foreach (var kvp in temp_GUID_AssetBundleName)
                        {
                            if (kvp.Value == s)
                            {
                                s = $"{db[i].name}_{s_t}_{db[i].guid}.{ext}";
                            }
                        }

                        temp_GUID_AssetBundleName.Add(db[i].guid, s);
                    }
					
					if (bundleNameMode == BundlePlaceMode.GUID_Type)
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

                        temp_GUID_AssetBundleName.Add(db[i].guid, $"{db[i].guid}_{s_t}.{ext}");
                    }

                    if (bundleNameMode == BundlePlaceMode.OnlyGUID)
                    {
                        temp_GUID_AssetBundleName.Add(db[i].guid, $"{db[i].guid}.{ext}");
                    }

                    if (bundleNameMode == BundlePlaceMode.OnlyGUIDWithoutExtension)
                    {
                        temp_GUID_AssetBundleName.Add(db[i].guid, db[i].guid);
                    }


                    if (bundleNameMode == BundlePlaceMode.Name)
                    {
                        var s = $"{db[i].name}.{ext}";

                        //check name dubl
                        foreach (var kvp in temp_GUID_AssetBundleName)
                        {
                            if (kvp.Value == s)
                            {
                                s = $"{db[i].name}_{db[i].guid}.{ext}";
                            }
                        }

                        temp_GUID_AssetBundleName.Add(db[i].guid, s);
                    }

                    if (bundleNameMode == BundlePlaceMode.RawPath)
                    {
                        var wo_ext = Path.ChangeExtension(db[i].path, Extension);
                        temp_GUID_AssetBundleName.Add(db[i].guid, wo_ext);
                    }

                    EditorUtility.DisplayProgressBar("ContentDatabase -> Checking by bundle name mode", $"{db[i].path} -> {temp_GUID_AssetBundleName[db[i].guid]}", (float)i / (float)Assets);
                    await Task.Yield();
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

            if (Compression == CompressionType.Uncompressed)
            {
                bopt |= BuildAssetBundleOptions.UncompressedAssetBundle;
            }
            else if (Compression == CompressionType.LZ4)
            {
                bopt |= BuildAssetBundleOptions.ChunkBasedCompression;
            }
            else if (Compression == CompressionType.LZMA)
            {
                bopt |= BuildAssetBundleOptions.None;
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
                EditorUtility.DisplayProgressBar("ContentDatabase -> Generating", "Generating loading chains", 0.5f);
                GenerateChains(manifest);

                //moving files
                if (Get().bundleNameMode == BundlePlaceMode.Name || Get().bundleNameMode == BundlePlaceMode.Name_Type || Get().bundleNameMode == BundlePlaceMode.RawPath || Get().bundleNameMode == BundlePlaceMode.GUID_Type)
                {
                    int i = 0;
                    foreach (var dictionary in temp_GUID_AssetBundleName)
                    {
                        EditorUtility.DisplayProgressBar("ContentDatabase -> Renaming files", $"Renaming file: {dictionary.Key} -> {dictionary.Value}", (float)i / (float)temp_GUID_AssetBundleName.Count);
                        var file_path_with_guid = Path.Combine(ContentFolder, dictionary.Key);
                        if (File.Exists(file_path_with_guid))
                        {
                            string new_file_path = Path.Combine(ContentFolder, dictionary.Value);

                            if (File.Exists(new_file_path))
                            {
                                File.Delete(new_file_path);
                            }

                            if (Get().bundleNameMode == BundlePlaceMode.RawPath)
                            {
                                var dir = Path.GetDirectoryName(new_file_path);
                                if (!Directory.Exists(dir))
                                {
                                    Directory.CreateDirectory(dir);
                                }
                            }

                            File.Move(file_path_with_guid, new_file_path);
                        }
                    }
                }
                EditorUtility.DisplayProgressBar("ContentDatabase -> Renaming files", "Fixing names in chains by file names", 0.7f);
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

            File.WriteAllText(ContentDBPath, JsonUtility.ToJson(m_ContentInfo, true));
            EditorUtility.ClearProgressBar();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.ClearProgressBar();
        }
#endif
    }

    /* IO */
    public const string DBFolder = "Assets/ContentManagement/";
    public static string DBFile { get; private set; } = Path.Combine(DBFolder, "ContentDatabase.asset");

    static ContentDatabase db;
    public static ContentDatabase Get()
    {
#if UNITY_EDITOR
        if (!db)
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
#endif
#if !UNITY_EDITOR
        if (!db)
        {
            db = CreateInstance<ContentDatabase>();
            if (File.Exists(ContentDBPath))
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(ContentDBPath), db.m_ContentInfo);
            }
            else
            {
                Debug.LogError($"[ContentDatabase] {ContentDBPath} not found!");
            }
        }
#endif

        db.hideFlags = HideFlags.DontSaveInBuild;
        return db;
    }

#if UNITY_EDITOR
    public static void Save()
    {
        EditorUtility.SetDirty(Get());
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
        HasHideFlags,
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
            path.Contains("/editor resources/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/editor resources", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("editor resources/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/package resources/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/package resources", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("package resources/", StringComparison.OrdinalIgnoreCase) ||
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
            if (asset.hideFlags.HasFlag(HideFlags.DontSave))
            {
                return AssetError.HasHideFlags;
            }

            if (asset.hideFlags.HasFlag(HideFlags.DontSaveInBuild))
            {
                return AssetError.HasHideFlags;
            }

            if (asset.hideFlags.HasFlag(HideFlags.DontSaveInEditor))
            {
                return AssetError.HasHideFlags;
            }

            if (asset.hideFlags.HasFlag(HideFlags.HideAndDontSave))
            {
                return AssetError.HasHideFlags;
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

    public static async void AddDependenciesForAsset(AssetInfo asset, Action onComplete = null)
    {
        var deps = AssetDatabase.GetDependencies(asset.path, true);
        EditorUtility.DisplayProgressBar($"Adding dependencies for {asset.path}", $"Adding {deps.Length} dependencies for {asset.path}", 0);
        int i = 0;
        foreach (var dep in deps)
        {
            var dep_error = AddAsset(AssetDatabase.LoadMainAssetAtPath(dep));
            EditorUtility.DisplayProgressBar($"Adding dependencies for {asset.path}", $"Adding {dep} dependency of {asset.path}", (float)i/deps.Length);
            i++;
            await Task.Yield();
        }
        EditorUtility.ClearProgressBar();
        onComplete?.Invoke();
    }

    /* Object */
    public static AssetError AddAsset(Object asset)
    {
        if (!Contains(asset))
        {
            var error = Get().CheckAsset(asset, out string guid, out string path, out Type type);
            var asset_name = Path.GetFileNameWithoutExtension(path);

            if (error == AssetError.NoError)
            {
                if (asset is ScriptableObject)
                {
                    type = typeof(ScriptableObject);
                }

                Get().m_ContentInfo.Assets.Add(new AssetInfo() 
                { 
                    name = asset_name, 
                    guid = guid, 
                    path = path, 
                    base_type = type.BaseType.Name, 
                    type = type.Name 
                });

                Save();
            }
            else if(error == AssetError.HasHideFlags)
            {
                LogWarning($"Asset {asset.name} has hide flags -> "+asset.hideFlags);
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
            for (int i = 0; i < Get().m_ContentInfo.Assets.Count; i++)
            {
                if (Get().m_ContentInfo.Assets[i].guid == guid)
                {
                    Get().m_ContentInfo.Assets.RemoveAt(i);
                }
            }
        }
    }
#endif

    public static void RemoveAsset(string asset_guid)
    {
        for (int i = 0; i < Get().m_ContentInfo.Assets.Count; i++)
        {
            if (Get().m_ContentInfo.Assets[i].guid == asset_guid)
            {
                Get().m_ContentInfo.Assets.RemoveAt(i);
            }
        }
    }

#if UNITY_EDITOR
    public static bool Contains(Object asset)
    {
        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId))
        {
            for (int i = 0; i < Get().m_ContentInfo.Assets.Count; i++)
            {
                if (Get().m_ContentInfo.Assets[i].guid == guid)
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
        for (int i = 0; i < Get().m_ContentInfo.Assets.Count; i++)
        {
            if (Get().m_ContentInfo.Assets[i].guid == guid)
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
            for (int i = 0; i < Get().m_ContentInfo.Assets.Count; i++)
            {
                if (Get().m_ContentInfo.Assets[i].guid == guid)
                {
                    info = Get().m_ContentInfo.Assets[i];
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
        for (int i = 0; i < Get().m_ContentInfo.Assets.Count; i++)
        {
            if (Get().m_ContentInfo.Assets[i].guid == guid)
            {
                info = Get().m_ContentInfo.Assets[i];
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
            if (index < Get().m_ContentInfo.Assets.Count)
            {
                info = Get().m_ContentInfo.Assets[index];
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
        for (int i = 0; i < Get().m_ContentInfo.Assets.Count; i++)
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
        for (int i = 0; i < Get().m_ContentInfo.Assets.Count; i++)
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
        for (int i = 0; i < Get().m_ContentInfo.Assets.Count; i++)
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
    int IOrderedCallback.callbackOrder => int.MaxValue;

    void IPostprocessBuildWithReport.OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.result != BuildResult.Failed)
        {
            Log("Copying content data into build");

            var p = Path.Combine(Path.GetDirectoryName(report.summary.outputPath), ContentFolderName);

            if (Directory.Exists(p))
            {
                ClearDirectory(p);
            }

            CopyDirectory(ContentFolder, p);
        }

        void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);

            if (!dir.Exists)
            {
                LogError($"Source directory not found: {dir.FullName}");
                return;
            }

            DirectoryInfo[] dirs = dir.GetDirectories();

            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }
    }
#endif
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
    }
}
#endif
