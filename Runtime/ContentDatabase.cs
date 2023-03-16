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

    public const string ContentInfoFileName = "Content.json";

    public static string ContentFolder { get { return Path.Combine(Environment.CurrentDirectory, ContentFolderName); } }
    public static string ContentDBPath { get { return Path.Combine(Environment.CurrentDirectory, ContentFolderName, ContentInfoFileName); } }

    [Serializable]
    public class ContentInfo
    {

        [Serializable]
        public class Group
        {
            public string Name;

            public List<string> Flags = new List<string>();

            public List<AssetInfo> Assets = new List<AssetInfo>();

#if UNITY_EDITOR
            public bool IsSceneGroup()
            {
                int scenes = 0;
                for(int i = 0; i < Assets.Count; i++)
                {
                    if(Assets[i].type == "SceneAsset")
                    {
                        scenes++;
                    }
                }
                return scenes > 0;
            }
#endif
        }

        [HideInInspector]
        public List<Group> Groups = new List<Group>(); 

        public bool TryGetGroup(string name, out Group out_group)
        {
            out_group = null;

            for(var i = 0; i < Groups.Count; i++)
            {
                if (Groups[i] == null)
                    continue;

                var group = Groups[i];

                if (group.Name == name)
                {
                    out_group = group;
                    return true;
                }
            }

            return false;
        }

        public bool AddGroup(string name, out ContentInfo.Group out_group)
        {
            out_group = null;
            if(!TryGetGroup(name, out out_group))
            {
                var group = new Group();
                group.Name = name;
                Groups.Add(group);
                return true;
            }
            return false;
        }

        public Group AddGroup(string name)
        {
            if (!TryGetGroup(name, out var out_group))
            {
                var group = new Group();
                group.Name = name;
                Groups.Add(group);
                return group;
            }
            return null;
        }

        public Group AddGroupOrFind(string name)
        {
            if (!TryGetGroup(name, out var out_group))
            {
                var group = new Group();
                group.Name = name;
                Groups.Add(group);
                return group;
            }
            return out_group;
        }

        public bool TryRemoveGroup(string name)
        {
            for (var i = 0; i < Groups.Count; i++)
            {
                if (Groups[i] == null)
                    continue;

                var group = Groups[i];

                if (group.Name == name)
                {
                    Groups.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public List<BundleInfo> Bundles = new List<BundleInfo>();

        [NonSerialized]
        public string ContentDir = string.Empty;

        public List<string> CustomContentInfoPath = new List<string>();

        [NonSerialized]
        public List<ContentInfo> CustomContentInfo = new List<ContentInfo>();
    }

    [HideInInspector]
    [SerializeField]
    private ContentInfo m_ContentInfo = new ContentInfo();

    public ContentInfo GetContentInfo() { return m_ContentInfo; }

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

            if (EditorApplication.isPlaying)
            {
                db.m_ContentInfo.ContentDir = ContentFolder;
                //load custom content info
                if (db.m_ContentInfo.CustomContentInfoPath.Count > 0)
                {
                    foreach (var path in db.m_ContentInfo.CustomContentInfoPath)
                    {
                        ContentInfo custom_Content = null;
                        try
                        {
                            var customFullPath = Path.Combine(ContentFolder, path);
                            var customDir = Path.GetDirectoryName(path);
                            custom_Content = JsonUtility.FromJson<ContentInfo>(File.ReadAllText(customFullPath));
                            custom_Content.ContentDir = customDir;
                            db.m_ContentInfo.CustomContentInfo.Add(custom_Content);
                            Log($"Detected custom content info {path}");
                        }
                        catch (Exception ex) { LogError($"Detected custom content {path} read failed! [{ex.Message}]"); }
                        finally { Log($"Detected {custom_Content.Groups.Count} groups in custom content {path}"); }
                    }
                }
            }
        }
#endif
#if !UNITY_EDITOR
        if (!db)
        {
            db = CreateInstance<ContentDatabase>();
            db.m_ContentInfo.ContentDir = ContentFolder;
            if (File.Exists(ContentDBPath))
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(ContentDBPath), db.m_ContentInfo);

                //load custom content info
                if (db.m_ContentInfo.CustomContentInfoPath.Count > 0)
                {
                    foreach (var path in db.m_ContentInfo.CustomContentInfoPath)
                    {
                        ContentInfo custom_Content = null;
                        try
                        {
                            var customFullPath = Path.Combine(ContentFolder, path);
                            var customDir = Path.GetDirectoryName(path);
                            custom_Content = JsonUtility.FromJson<ContentInfo>(File.ReadAllText(customFullPath));
                            custom_Content.ContentDir = customDir;
                            db.m_ContentInfo.CustomContentInfo.Add(custom_Content);
                            Log($"Detected custom content info {path}");
                        }
                        catch (Exception ex) { LogError($"Detected custom content {path} read failed! [{ex.Message}]"); }
                        finally { Log($"Detected {custom_Content.Groups.Count} groups in custom content {path}"); }
                    }
                }
            }
            else
            {
                Debug.LogError($"[ContentDatabase] {ContentDBPath} not found!");
            }
        }
        db.Logging = !Bootstraper.Instance.HasCmd("-content_db_nologs");
#endif
        db.hideFlags = HideFlags.DontSaveInBuild;
        return db;
    }

#if UNITY_EDITOR

    public void SelfCheck()
    {
        foreach(var group in m_ContentInfo.Groups)
        {
            if(group != null)
            {
                foreach(var asset in group.Assets)
                {
                    var error = CheckAsset(AssetDatabase.LoadMainAssetAtPath(asset.path), out var blob, out var blob1, out var blob2);

                    if(error != AssetError.NoError)
                    {
                        group.Assets.Remove(asset);
                        Debug.LogWarning($"Asset {asset.path} removed due error {error}");
                    }
                }
            }
        }
    }


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
        GroupOnlyForScenes,
        GroupIsNull,
        IsAlreadyContainsAsset,
        CantIncludeAssetFromScene,
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
                    return AssetError.CantIncludeAssetFromScene;
                }
            }

            if (asset.GetType() == typeof(LightingSettings))
            {
                return AssetError.IsUnsupportedType;
            }

            if (asset.GetType() == typeof(LightingDataAsset))
            {
                return AssetError.IsUnsupportedType;
            }

            if (asset.name == "OcclusionCullingData")
            {
                return AssetError.IsUnsupportedType;
            }

            if (asset.GetType() == typeof(UnityEditor.Presets.Preset))
            {
                return AssetError.IsUnsupportedType;
            }
            
            if (asset.GetType() == typeof(UnityEngine.Rendering.ProbeReferenceVolumeProfile))
            {
                return AssetError.IsUnsupportedType;
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
        if (FindAsset(asset.guid, out var out_group))
        {
            var deps = AssetDatabase.GetDependencies(asset.path, true);
            EditorUtility.DisplayProgressBar($"Adding dependencies for {asset.path}", $"Adding {deps.Length} dependencies for {asset.path}", 0);
            int i = 0;
            foreach (var dep in deps)
            {
                var asset_obj = AssetDatabase.LoadMainAssetAtPath(dep);

                if (asset_obj != null)
                {
                    var dep_error = AddAsset(Get().GetContentInfo().AddGroupOrFind($"{out_group.Name}_shared"), asset_obj);
                    EditorUtility.DisplayProgressBar($"Adding dependencies for {asset.path}", $"Adding {dep} dependency of {asset.path}", (float)i / deps.Length);
                    i++;
                    await Task.Yield();
                }
            }
            EditorUtility.ClearProgressBar();
        }
        onComplete?.Invoke();
    }

    /* Object */
    public static AssetError AddAsset(ContentInfo.Group group, Object asset)
    {
        if(group == null)
        {
            return AssetError.GroupIsNull;
        }
        if (!FindAsset(asset))
        {
            var error = Get().CheckAsset(asset, out string guid, out string path, out Type type);
            var asset_name = Path.GetFileNameWithoutExtension(path);

            if (error == AssetError.NoError)
            {
                if (group.IsSceneGroup() && type != typeof(SceneAsset))
                {
                    return AssetError.GroupOnlyForScenes;
                }
                if (asset is ScriptableObject)
                {
                    type = typeof(ScriptableObject);
                }

                group.Assets.Add(new AssetInfo() 
                { 
                    name = asset_name, 
                    guid = guid, 
                    path = path, 
                    base_type = type.BaseType.Name, 
                    type = type.Name 
                });

                Save();
            }
            else
            {
                LogWarning($"Asset {asset.name} | error {error}");
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
            RemoveAsset(guid);
        }
    }
#endif

    public static void RemoveAsset(string asset_guid)
    {
        for (var a = 0; a < Get().m_ContentInfo.Groups.Count; a++)
        {
            var group = Get().m_ContentInfo.Groups[a];

            if (group != null)
            {
                for (int i = 0; i < group.Assets.Count; i++)
                {
                    if (group.Assets[i].guid == asset_guid)
                    {
                        group.Assets.RemoveAt(i);
                    }
                }
            }
        }

        //find in custom content
        for (var custom_content_index = 0; custom_content_index < Get().m_ContentInfo.CustomContentInfo.Count; custom_content_index++)
        {
            for (var group_index = 0; group_index < Get().m_ContentInfo.CustomContentInfo[custom_content_index].Groups.Count; group_index++)
            {
                var group = Get().m_ContentInfo.CustomContentInfo[custom_content_index].Groups[group_index];

                if (group == null)
                    continue;

                for (int i = 0; i < group.Assets.Count; i++)
                {
                    if (group.Assets[i].guid == asset_guid)
                    {
                        group.Assets.RemoveAt(i);
                    }
                }
            }
        }
    }

#if UNITY_EDITOR
    public static bool FindAsset(Object asset)
    {
        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId))
        {
            for (var a = 0; a < Get().m_ContentInfo.Groups.Count; a++)
            {
                var group = Get().m_ContentInfo.Groups[a];

                if (group == null)
                    continue;

                for (int i = 0; i < group.Assets.Count; i++)
                {
                    if (group.Assets[i].guid == guid)
                    {
                        return true;
                    }
                }
            }
        }

        //find in custom content
        for (var custom_content_index = 0; custom_content_index < Get().m_ContentInfo.CustomContentInfo.Count; custom_content_index++)
        {
            for (var group_index = 0; group_index < Get().m_ContentInfo.CustomContentInfo[custom_content_index].Groups.Count; group_index++)
            {
                var group = Get().m_ContentInfo.CustomContentInfo[custom_content_index].Groups[group_index];

                if (group == null)
                    continue;

                for (int i = 0; i < group.Assets.Count; i++)
                {
                    if (group.Assets[i].guid == guid)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
#endif

    public static bool FindAsset(string guid)
    {
        for (var a = 0; a < Get().m_ContentInfo.Groups.Count; a++)
        {
            var group = Get().m_ContentInfo.Groups[a];

            if (group == null)
                continue;

            for (int i = 0; i < group.Assets.Count; i++)
            {
                if (group.Assets[i].guid == guid)
                {
                    return true;
                }
            }
        }

        //find in custom content
        for (var custom_content_index = 0; custom_content_index < Get().m_ContentInfo.CustomContentInfo.Count; custom_content_index++)
        {
            for (var group_index = 0; group_index < Get().m_ContentInfo.CustomContentInfo[custom_content_index].Groups.Count; group_index++)
            {
                var group = Get().m_ContentInfo.CustomContentInfo[custom_content_index].Groups[group_index];

                if (group == null)
                    continue;

                for (int i = 0; i < group.Assets.Count; i++)
                {
                    if (group.Assets[i].guid == guid)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public static bool FindAsset(string guid, out ContentInfo.Group out_group)
    {
        out_group = null;
        for (var a = 0; a < Get().m_ContentInfo.Groups.Count; a++)
        {
            var group = Get().m_ContentInfo.Groups[a];

            if (group == null)
                continue;

            for (int i = 0; i < group.Assets.Count; i++)
            {
                if (group.Assets[i].guid == guid)
                {
                    out_group = group;
                    return true;
                }
            }
        }

        //find in custom content
        for(var custom_content_index = 0; custom_content_index < Get().m_ContentInfo.CustomContentInfo.Count; custom_content_index++)
        {
            for (var group_index = 0; group_index < Get().m_ContentInfo.CustomContentInfo[custom_content_index].Groups.Count; group_index++)
            {
                var group = Get().m_ContentInfo.CustomContentInfo[custom_content_index].Groups[group_index];

                if (group == null)
                    continue;

                for (int i = 0; i < group.Assets.Count; i++)
                {
                    if (group.Assets[i].guid == guid)
                    {
                        out_group = group;
                        return true;
                    }
                }
            }
        }

        return false;
    }

#if UNITY_EDITOR
    public static bool FindAsset(Object asset, out ContentInfo.Group group, out AssetInfo info)
    {
        info = null;
        group = null;
        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId))
        {
            for (var a = 0; a < Get().m_ContentInfo.Groups.Count; a++)
            {
                group = Get().m_ContentInfo.Groups[a];

                if (group != null)
                {
                    for (int i = 0; i < group.Assets.Count; i++)
                    {
                        if (group.Assets[i].guid == guid)
                        {
                            info = group.Assets[i];
                            return true;
                        }
                    }
                }
            }

            //find in custom content
            for (var custom_content_index = 0; custom_content_index < Get().m_ContentInfo.CustomContentInfo.Count; custom_content_index++)
            {
                for (var group_index = 0; group_index < Get().m_ContentInfo.CustomContentInfo[custom_content_index].Groups.Count; group_index++)
                {
                    group = Get().m_ContentInfo.CustomContentInfo[custom_content_index].Groups[group_index];

                    if (group == null)
                        continue;

                    for (int i = 0; i < group.Assets.Count; i++)
                    {
                        if (group.Assets[i].guid == guid)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }
#endif

    public static bool FindAsset(string guid, out ContentInfo.Group group, out AssetInfo info)
    {
        info = null;
        group = null;
        for (var a = 0; a < Get().m_ContentInfo.Groups.Count; a++)
        {
            group = Get().m_ContentInfo.Groups[a];

            if (group == null)
                continue;

            for (int i = 0; i < group.Assets.Count; i++)
            {
                if (group.Assets[i].guid == guid)
                {
                    info = group.Assets[i];
                    return true;
                }
            }
        }

        //find in custom content
        for (var custom_content_index = 0; custom_content_index < Get().m_ContentInfo.CustomContentInfo.Count; custom_content_index++)
        {
            for (var group_index = 0; group_index < Get().m_ContentInfo.CustomContentInfo[custom_content_index].Groups.Count; group_index++)
            {
                group = Get().m_ContentInfo.CustomContentInfo[custom_content_index].Groups[group_index];

                if (group == null)
                    continue;

                for (int i = 0; i < group.Assets.Count; i++)
                {
                    if (group.Assets[i].guid == guid)
                    {
                        return true;
                    }
                }
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
        for (var a = 0; a < Get().m_ContentInfo.Groups.Count; a++)
        {
            var group = Get().m_ContentInfo.Groups[a];

            if (group == null)
                continue;

            for (int i = 0; i < group.Assets.Count; i++)
            {
                if (group.Assets[i].name == name)
                {
                    info = group.Assets[i];
                    return true;
                }
            }
        }

        //find in custom content
        for (var a = 0; a < Get().m_ContentInfo.CustomContentInfo.Count; a++)
        {
            var customContent = Get().m_ContentInfo.CustomContentInfo[a];

            if (customContent != null)
            {
                for (var b = 0; b < customContent.Groups.Count; b++)
                {
                    var group = customContent.Groups[b];

                    if (group == null)
                        continue;

                    for (int i = 0; i < group.Assets.Count; i++)
                    {
                        if (group.Assets[i].name == name)
                        {
                            info = group.Assets[i];
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    public static bool TryGetAssetInfoByNameAndType<T>(string name, Type type, out AssetInfo info, StringComparison stringComparison = StringComparison.Ordinal)
    {
        info = null;

        for (var a = 0; a < Get().m_ContentInfo.Groups.Count; a++)
        {
            var group = Get().m_ContentInfo.Groups[a];

            if (group == null)
                continue;

            for (int i = 0; i < group.Assets.Count; i++)
            {
                if (group.Assets[i].name == name && info.type == type.Name)
                {
                    info = group.Assets[i];
                    return true;
                }
            }
        }

        //find in custom content
        for (var a = 0; a < Get().m_ContentInfo.CustomContentInfo.Count; a++)
        {
            var customContent = Get().m_ContentInfo.CustomContentInfo[a];

            if (customContent != null)
            {
                for (var b = 0; b < customContent.Groups.Count; b++)
                {
                    var group = customContent.Groups[b];

                    if (group == null)
                        continue;

                    for (int i = 0; i < group.Assets.Count; i++)
                    {
                        if (group.Assets[i].name == name && group.Assets[i].type == type.Name)
                        {
                            info = group.Assets[i];
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    public static bool TryGetAssetInfoByPath(string path, out AssetInfo info, StringComparison stringComparison = StringComparison.Ordinal)
    {
        info = null;
        for (var a = 0; a < Get().m_ContentInfo.Groups.Count; a++)
        {
            var group = Get().m_ContentInfo.Groups[a];

            if (group == null)
                continue;

            for (int i = 0; i < group.Assets.Count; i++)
            {
                if (group.Assets[i].path == path)
                {
                    info = group.Assets[i];
                    return true;
                }
            }
        }

        //find in custom content
        for (var a = 0; a < Get().m_ContentInfo.CustomContentInfo.Count; a++)
        {
            var customContent = Get().m_ContentInfo.CustomContentInfo[a];

            if (customContent != null)
            {
                for (var b = 0; b < customContent.Groups.Count; b++)
                {
                    var group = customContent.Groups[b];

                    if (group == null)
                        continue;

                    for (int i = 0; i < group.Assets.Count; i++)
                    {
                        if (group.Assets[i].path == path)
                        {
                            info = group.Assets[i];
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    public static bool TryGetAssetInfoByGUID(string guid, out AssetInfo info)
    {
        info = null;

        for (var a = 0; a < Get().m_ContentInfo.Groups.Count; a++)
        {
            var group = Get().m_ContentInfo.Groups[a];

            if (group == null)
                continue;

            for (int i = 0; i < group.Assets.Count; i++)
            {
                if (group.Assets[i].guid == guid)
                {
                    info = group.Assets[i];
                    return true;
                }
            }
        }

        //find in custom content
        for (var a = 0; a < Get().m_ContentInfo.CustomContentInfo.Count; a++)
        {
            var customContent = Get().m_ContentInfo.CustomContentInfo[a];

            if (customContent != null)
            {
                for (var b = 0; b < customContent.Groups.Count; b++)
                {
                    var group = customContent.Groups[b];

                    if (group == null)
                        continue;

                    for (int i = 0; i < group.Assets.Count; i++)
                    {
                        if (group.Assets[i].guid == guid)
                        {
                            info = group.Assets[i];
                            return true;
                        }
                    }
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
